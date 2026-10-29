# R2 phase 1 spec: ChangeService, CaseService, DeadlineWatcher, clock control

Branch `r2/case-workflow` (on top of PR #18). Contracts are in `Features/{Changes,Cases,Deadlines,Clients,Rules}`; do not
change public signatures in `ICaseService` / `IChangeService` / records without a HANDOFF. Sources: `docs/PLAN.md` 7.2, 9,
13.1; `docs/GOLDEN-CASES.md` sections 1, 2, 4 (cases 4, 16, 17a, 17b, 18), 5 (I3, I4, I5).

## General
- Time: inject `ISimClock`, never `TimeProvider` / `DateTime.Now` (ClockTests fails otherwise).
- Data: `IDbContextFactory<AppDbContext>`, one context per operation.
- Roles: `CurrentUser.Require(...)` takes separate role strings: `Require(Roles.Admin, Roles.Member)`. Never pass
  `Roles.Editors` (it is the single string "admin,member" and matches nobody).
  - Operator = `Roles.Operator` (= admin): approve/reject a request, close a case, move the clock.
  - Editors (admin, member): record a change, add evidence/signals, draft a request, register a response, escalate.
  - Viewer: read only (queue, get, lists). Unauthorized = `UnauthorizedAccessException` from `Require`.
- Every state change writes one audit entry through `IAuditLog.AppendAsync(actor, AuditKinds.X, payload, caseId)`.
  Actor: `"user:" + user.Id` for users, `"system"` for the watcher. Payload: codes, rule IDs, jurisdictions, statuses only;
  never names, phones, IBANs, addresses (the audit log masks, but do not rely on it).
- Invalid transition = `InvalidOperationException` with a message naming the current status. Unknown case/client code =
  `KeyNotFoundException`.

## ChangeService.RecordAsync(ChangeRequest)
Require editors. Load the client by code (tracked, with the same includes as `ClientService.Full`); `before =
ClientSnapshot.From(client)`. Apply the change to the entity:
- AddressChanged: mark current addresses of the same `Kind` not current, add the new one (ValidFrom = today from the clock).
  Design choice (golden case 12): a new `InCareOf` or `HoldMail` address marks ALL current addresses not current, since the
  bank then has only that address (`CRS-IND-CO` "no other address", p. 29 f; `FATCA-IND-CO` "the only address", p. 31).
  AddressAdded: add it (it never replaces, a c/o neither). PhoneAdded: add. PhoneChanged: replace the current phones with the new one. PhoneRemoved: mark
  the matching number not current.
- StandingOrderCreated: add to the account with `AccountIban` (first account if null).
- PowerOfAttorneyGranted: add. PersonalDataChanged: set BirthCountry / Citizenships when given.
- SelfCertSubmitted: dacă clientul are un caz în `ResponseReceived`, refuză înainte de supersesiune, cu sau fără
  `request.CaseCode`. Altfel marchează autocertificarea validă curentă `Superseded` și o adaugă pe cea nouă
  (SignedAt din snapshot, ReceivedAt = clock.UtcNow, Channel = request.Channel).
  DocumentUploaded: no data change (the document is already saved by the caller, `DocumentId`).
Save, then `after = ClientSnapshot.From(client)`. Outcome:
- DocumentUploaded: shield the document (`IShieldedDocuments`); set `ClientDocument.ShieldVerdict`; HiddenTextRemoved gives
  `Decision.ShieldAlert` (no indicia; explanation EN/RO from code) and an audit `shield.alert`; otherwise `NoCase`.
- SelfCertSubmitted: `engine.EvaluateSelfCertification(after, selfCert)`.
- everything else: `engine.Evaluate(before, after, request.Kind)`.
Save `ChangeEvent` (ReceivedAt = clock now, Summary = request.Summary or a code-built one without personal data,
Old/NewValueJson masked with `PiiVault.ForStorage().Mask`) and `RuleEvaluation` (ResultJson = the outcome serialized and
masked; EngineVersion; EvaluatedAt). If `outcome.Uncovered` is not empty: `caseService.OpenAsync(change, outcome)`, before
the audit entries. Change, evaluation, case and audit now share one EF context and transaction; an append failure rolls
back all of them, so there is neither an uncovered persisted change nor a partial audit history. Then audit `change.received` and one
`rule.applied` per indicium and per cleared rule. If `request.CaseCode` is set and the kind is SelfCertSubmitted, the case
must be open (otherwise `InvalidOperationException` naming its status, before anything is saved); the self-certification
answers that case, and a new uncovered hit (e.g. SC-TIN-FORMAT) extends it through `OpenAsync`, never a new case.
Rezultatul `ChangeResult` include `SelfCertificationId` ca proprietate nullable, nepozițională, cu `init`;
constructorul public și `Deconstruct` rămân neschimbate. Pentru `SelfCertSubmitted`, conține ID-ul exact al
autocertificării salvate; apelantul îl folosește la `RegisterResponseAsync` fără a căuta ulterior cea mai recentă autocertificare.
`ListAsync(clientCode, take)`: newest first. ChangeService depends on ICaseService; CaseService must NOT depend on
IChangeService.

## CaseService
- `OpenAsync(change, outcome)`: if the client has an open case (status not Closed/Expired), add the uncovered indicia not
  already there (same RuleId + Jurisdiction), widen Regime (Crs + Fatca = Both), keep the deadline (design choice,
  NEVERIFICAT), audit `case.indicium-added`, return it. A case without a deadline (FATCA only, or self-certification checks
  only) gets the CRS deadline from the change that brings its first `CRS-IND-*` indicium. Otherwise create: Code `CRS-{year}-{seq:0000}` (year and seq per year
  from the clock), ClientId/ClientCode, TriggerChangeId, Regime = outcome.Regime, Status Open, OpenedAt = now, AssignedTo =
  "operator". Deadline: only the uncovered indicia of a change of circumstances (`*-IND-*`, p. 29, 31) set it. Any `CRS-IND-*`
  gives the CRS deadline (a Both case too): for each account `IDeadlineCalculator.Compute(new(change.ReceivedAt, Regime.Crs,
  account.OpenedOn))`, the earliest non-null DueAt (I4); no accounts = a new account opened today. Pentru
  `FATCA-IND-*` fără indicii CRS, `DueAt` și `RemindAt` rămân `null`, dar `DeadlineRuleId = "FATCA-SC"` și
  `DeadlineGuidePage = "38"` se păstrează ca metadate. Paragraful despre modificarea circumstanțelor (p. 38)
  nu stabilește un termen (`NEVERIFICAT`); nu preluăm cele 90 de zile CRS sau termenul FATCA pentru încetarea
  exceptării din paragraful separat. Self-certification checks alone (`SC-*`, e.g. `SC-TIN-FORMAT`, golden case 2) give none either
  (decided 28.10, as for FATCA, GOLDEN-CASES I1: the 90 days follow a change of circumstances, p. 29, 35-36): DueAt and RemindAt
  null, DeadlineRuleId = the SC rule, text `CaseTexts.NoDeadlineForSelfCertificationCheck`. Set DueAt, RemindAt, IsPreexisting,
  DeadlineRuleId, DeadlineGuidePage. Indicia from `outcome.Uncovered` (with ExplanationRo, ChangeEventId, AddedAt).
  Audit `case.opened` (code, regime, rule IDs, due).
  `DL-PRE-YEAR`, ruta căutării electronice: „cea mai târzie” dintre sfârșitul anului și +90 de zile este
  **interpretarea noastră** a Codului de procedură fiscală, Anexa nr. 1, secț. III, subsecț. B, pct. 16 („ori”),
  aliniată la Comentariul OCDE, secț. III, pct. 16 („until the later of”). Pe ruta testului adresei de rezidență,
  Anexa nr. 2 pct. 1 spune explicit „oricare dintre acestea este mai recentă” (OCDE pct. 13); ghidul p. 29 spune
  „sau” pe această rută, iar p. 30 nu dă termenul pentru căutarea electronică.
  Sursa: [`reguli-verificate.md`](../reguli-verificate.md), rândul „Cont preexistent, schimbare de circumstanțe”.
- `QueueAsync(filter)`: open first, then by DueAt ascending, cases with DueAt null last (order explicitly by
  `DueAt == null` then `DueAt`), include Indicia. `GetAsync(code)`: everything (indicia, evidence, signals, requests,
  responses, proof), AsSplitQuery.
- `AddEvidenceAsync`: Source Document: load with `IShieldedDocuments`, verify with `IEvidenceVerifier.Verify(quote,
  result, page)`; Verified/Page/VerificationNote from the check; if `FoundOnlyInHiddenText`, audit `shield.alert` too.
  Source BankData: Verified = true, note "bank data". The document must belong to the client. Audit `evidence.recorded`.
- `AddSignalAsync`: the evidence must exist and belong to the client; never touches case status or decision. Audit
  `signal.recorded`. `ListEvidenceAsync` / `ListSignalsAsync` by client code, with or without a case.
- `RequiredDocumentsAsync(caseCode)`: code, from regime and IsPreexisting, in the case's request language later (store keys
  as plain English sentences; the request has a Language and the UI translates). CRS new: "A new self-certification, or a
  reasonable explanation with supporting documents (if applicable)". CRS preexisting: "A new self-certification" and "New
  documentary evidence of residence". FATCA or Both: also "A new FATCA self-certification". Indicia from address: add "Proof of
  the new address". Return distinct, stable order.
- `DraftRequestAsync`: editors; case must be Open or ResponseReceived; Draft with RequiredDocuments, DraftedBy, CreatedAt.
  Audit `request.drafted`.
  Follow-up din `ResponseReceived` privește un răspuns acceptat inițial care nu mai trece revalidarea la
  `CloseAsync` (de exemplu, circumstanțe noi ale clientului sau un document modificat/șters).
- `ApproveRequestAsync`: operator; request must be Draft; case Open or ResponseReceived (follow-up) -> AwaitingClient; a
  Reminder request may be approved in Reminded/Escalated and keeps that status. Request -> Sent, ApprovedBy (user name),
  ApprovedAt, Channel. Audit `request.approved`.
  Tranziția de follow-up `ResponseReceived -> AwaitingClient` se păstrează pentru această situație;
  un răspuns insuficient inițial este refuzat la `RegisterResponseAsync`, înainte de `ResponseReceived`.
  `RejectRequestAsync`: operator; Draft -> Rejected with RejectedBy/At/Reason;
  case unchanged; audit `request.rejected`.
- `RegisterResponseAsync`: editors; validează structura și adecvarea semantică înainte de tranziția
  AwaitingClient/Reminded/Escalated -> ResponseReceived; Expired/Closed throw. La înregistrare sunt obligatorii
  condițiile pe regim și reevaluarea descrise la `CloseAsync`, inclusiv formatul TIN și datele; statutul `Valid` singur nu ajunge.
  `SelfCertificationId` trebuie să fie al clientului, `Valid` (nu `Superseded`), cu
  `selfCert.ReceivedAt >= OpenedAt` și `selfCert.ReceivedAt <= clock.UtcNow`. `HasDocumentaryEvidence = true`
  cere un `DocumentId` existent, al aceluiași client. Data declarației se verifică pe zile, conform convenției de mai jos (p. 34).
  Un răspuns incomplet sau neadecvat semantic este refuzat, nu produce `ResponseReceived` și lasă termenul
  existent eligibil pentru memento/expirare.
  La acceptare: ReceivedAt = clock.UtcNow; audit `response.registered`. Cererile `RequestKind.Reminder` ale cazului
  rămase `Draft` sunt revocate și nu mai pot fi aprobate. `ResponseReceived` așteaptă doar închiderea
  de către operator; acceptarea nu închide automat cazul. Verificările se repetă la `CloseAsync`.
- `CloseAsync(caseCode, channel, evidenceKind, collectionSteps)`: operator; case ResponseReceived. Last response decides:
  - CRS new account: NewSelfCertification (with SelfCertificationId) OR ReasonableExplanation with Category in
    {Student, TeacherOrTrainee, Diplomat, EuOfficial} (p. 35, 37). `Other` or `None`: throw (a person decides outside this
    method; message says so).
  - CRS preexisting: NewSelfCertification AND HasDocumentaryEvidence (p. 29).
  - FATCA or Both: NewSelfCertification (p. 38).
  Re-evaluate: snapshot of the client (`ClientSnapshot.From`), for an explanation add a candidate `AcceptedExplanationSnapshot`
  per uncovered indicium jurisdiction, then `engine.EvaluateCurrent`; refuse to close if `Uncovered` is not empty or the
  decision is Clarification / FatcaAlert / ShieldAlert. Proof built by code: Channel, EvidenceKind, ConfirmedAt = the
  response's ReceivedAt (the moment the client confirmed, guide p. 34; the closing time is `Case.ClosedAt`),
  DeclarationDatedAt = self-cert SignedAt (explanation: response ReceivedAt), DeclarationReceivedAt = self-cert ReceivedAt
  (explanation: response ReceivedAt). Ghidul p. 34 cere „este datată cel mai târziu cu data primirii”: se compară
  zilele calendaristice, nu direct instanții `DatedAt` și `ReceivedAt`. Convenție de implementare, nu regulă
  de fus orar din ghid: ziua primirii este cea din `Europe/Bucharest`, iar instanții se persistă în UTC.
  Datele fără oră extrase își păstrează codificarea la miezul nopții și ziua declarată; un instant real se
  interpretează în ziua locală. Refuză o zi a declarației ulterioară zilei primirii sau o primire în viitor
  față de `ISimClock.UtcNow`, atât la `RegisterResponseAsync`, cât și la `CloseAsync`.
  EvidenceSha256 = SHA-256 hex of the response
  document bytes, or of a canonical JSON of the self-certification when there is no document; SignedBy = client full name;
  SignerCapacity AccountHolder; ApprovedBy = user name; CollectionSteps = given or a default code text (times in the
  invariant culture); TransactionOrDocumentId = "DOC-{id}" or "SC-{id}".
  For an explanation save one `AcceptedExplanation` per jurisdiction (the only write to client data).
  Case -> Closed, ClosedAt, ReportableFor = outcome.ReportableFor, Outcome/OutcomeRo (code text citing the rule), OutcomeRuleId:
  "RESP-EXPLANATION" for an explanation; for a new self-certification "SC-VALID" (p. 34; decided 28.10 after the validated
  card of case 4) on a CRS or Both case, "FATCA-SC" on a FATCA case. Audit `case.closed`.
- `EscalateAsync`: editors; from Open, AwaitingClient or Reminded -> Escalated, EscalatedAt, `Case.EscalationReason` = the
  reason masked with `PiiVault.ForStorage().Mask` (the watcher writes a code text); audit `case.escalated` (no free text).
- `TickAsync(DateTime now)`: API păstrat; fără verificare de rol (actor `system`), idempotent. Refuză
  `now > ISimClock.UtcNow` înainte de orice efect secundar, fără excepție pentru admin; nu modifică ceasul.
  Pentru scenariile demo cu timp avansat, avansează întâi ceasul prin `SimClockControl`, cu autorizarea operatorului.
  - Cases with RemindAt <= now, DueAt > now, RemindedAt null (one reminder per case, also after a follow-up), status Open
    or AwaitingClient: -> Reminded (RemindedAt) and then Escalated
    (EscalatedAt) in the same tick (PLAN 9.4), plus one Draft `RequestKind.Reminder` request with a code-written body (RO)
    and the required documents. Audit `case.reminded` and `case.escalated`. Counts in DeadlineTick.
  - Cases with DueAt <= now and status Open, AwaitingClient, Reminded or Escalated: -> Expired (ExpiredAt). New account:
    OutcomeRuleId DL-EXPIRED-NEW, ReportableFor = the jurisdictions of the client's previous valid self-certification
    (excluding RO) plus the indicia jurisdictions. Preexisting: DL-EXPIRED-PRE, ReportableFor = indicia jurisdictions
    ("at least the new jurisdiction", p. 29). Outcome/OutcomeRo from code. Audit `case.expired`. The consequence is CRS:
    only indicia with Regime != Fatca count. Design choice (the guide is silent on a case with both regimes): a Both case
    expires on its CRS deadline with the CRS consequence, and the same tick opens a FATCA-only case for the client with the
    FATCA indicia (DueAt null, FATCA-SC, p. 38), audit `case.opened` with "fatca follow-up of <code>"; the outcome text says so.
  - DueAt null (FATCA, self-certification checks) never reminds or expires. `ResponseReceived` nu expiră:
    răspunsul a fost admis structural și semantic și așteaptă doar închiderea de către operator.
  - A second tick at the same time changes nothing.

## DeadlineWatcher (Features/Cases/DeadlineWatcher.cs)
`BackgroundService`: every 30 seconds and immediately on `ISimClock.Changed`, create a scope and call
`ICaseService.TickAsync(clock.UtcNow)`; log counts; never crash the host on an exception (log and continue). Registered with
`AddHostedService<DeadlineWatcher>()` in `CasesSetup.AddCases`.

## Clock control (Features/Deadlines/SimClockControl.cs)
Scoped `SimClockControl(ISimClock clock, CurrentUser user, IAuditLog audit)`: `AdvanceDaysAsync(int days)` (1..400) and
`ResetAsync()`; require `Roles.Operator`; audit `clock.changed` (offset before/after). Registered in `AddDeadlines`.
