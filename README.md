# TRACE

**Tax Residency Anomaly Correlation Engine.** Every change to a client's data becomes a CRS/FATCA decision, before the
legal deadline runs out.

**Code decides, AI explains, a person approves.**

Raiffeisen Bank, *AI for Operational Efficiency* challenge, NEXXT AI Hackathon (VibeHack), 28–29 October 2026.

![TRACE: the dashboard with the open CRS/FATCA cases and their deadlines](docs/screenshots/dashboard.png)

When a client changes their address, phone number, a standing order or a power of attorney, the bank has to find out
again where the client pays taxes. Otherwise it reports the client wrongly to ANAF, on every account. TRACE catches the
change, applies the rules from the ANAF DAC2/CRS/FATCA Guide **in code** (each rule with an ID and a page), starts the
deadline, gathers the evidence, writes the request to the client and follows the answer until the case is closed, with
proof of the confirmation.

| | |
| --- | --- |
| The app | <https://hackathon-starter-k0ym.onrender.com/> (access code: `crs-nexxt-2026`; the first start takes up to a minute) |
| Video (≤ 3 min) | to be added at submission |
| The presentation | [`docs/presentation/TRACE.pdf`](docs/presentation/TRACE.pdf) (10 slides; notes: [`docs/PRESENTATION.md`](docs/PRESENTATION.md)) |
| How the jury can check it in 5 minutes | [`docs/SUBMISSION.md`](docs/SUBMISSION.md#how-the-jury-can-check-it-in-5-minutes) |
| Run it locally | [§10](#10-quick-start): only the .NET 10 SDK, no keys, no external services |
| The full plan | [`docs/PLAN.md`](docs/PLAN.md) (Romanian) |
| How it works, end to end | [`docs/CUM-FUNCTIONEAZA.md`](docs/CUM-FUNCTIONEAZA.md) (Romanian) |

The UI is in Romanian by default; switch to English with the RO/EN button in the top bar. All data is synthetic.

---

## Contents

1. [The problem](#1-the-problem)
2. [Who does what](#2-who-does-what)
3. [What it does: Maria's case, in screenshots](#3-what-it-does-marias-case-in-screenshots)
4. [The rules](#4-the-rules)
5. [The design interpretations](#5-the-design-interpretations-stated-openly)
6. [Architecture](#6-architecture)
7. [MVP scope](#7-mvp-scope)
8. [The golden cases and the metrics](#8-the-golden-cases-and-the-metrics)
9. [Privacy](#9-privacy)
10. [Quick start](#10-quick-start)
11. [Status](#11-status)
12. [Documentation](#12-documentation)
13. [The template](#13-the-template)

## 1. The problem

In a **self-certification**, the client declares the states where they are tax resident and the TIN for each one. When
the bank "știe sau are motive să știe" ["knows or has reason to know"] that the situation has changed, the
self-certification loses its validity. On a new account, the bank has **90 days** to obtain a new self-certification or a
reasonable explanation; otherwise the client is treated as tax resident **both** in the original jurisdiction **and** in
the new one (ANAF Guide, p. 35–36).

Today, the check means a person who compares fields, searches through documents, writes to the client and keeps a
deadline in mind. Every update has to be evaluated, but only the ones that matter for tax start a deadline. TRACE does
the repetitive part; the person approves what goes out and what gets closed.

**Source of truth:** the ANAF DAC2/CRS/FATCA Guide, version 29.12.2025, in [`docs/ghid/`](docs/ghid/) (the PDF and the
extracted pages, `pagini/p-XXX.md`). The rules verified against the text of the guide:
[`docs/reguli-verificate.md`](docs/reguli-verificate.md).

## 2. Who does what

| Who | What it does | What it never does |
| --- | --- | --- |
| **The code** (deterministic, tested) | Applies the rules from the guide (ID + page), calculates the deadlines, verifies the quotes, checks the TIN format, keeps the audit log | It does not guess: the same input always gives the same result |
| **The AI** (Claude on Amazon Bedrock) | Reads the documents, extracts quotes, finds context signals, writes the text to the client, explains | It does not decide whether something is an indicium, does not calculate deadlines, does not raise alerts |
| **The human** (operator, tax department) | Approves the request to the client and the closing of the case, decides the borderline cases | — |

A **context signal** (for example, a salary from a German company) is shown with its evidence, but it does not change the
decision and does not open any case. Nationality is not a signal; only US citizenship counts, and only for FATCA.

![How TRACE decides: the 8 steps, who owns each one, the 14 tools and the two that wait for a person](docs/screenshots/architecture.png)

## 3. What it does: Maria's case, in screenshots

Maria has a new account (opened after 1 Jan 2016) and moves to Munich. The whole path runs from the UI; the screenshots
below are from `main`, on the mock (no model, the real tools), in English.

**1. The change comes in, and code decides on the spot.** `/changes/new` records the change as core banking would send
it. `CrsRuleEngine` compares "before" with "after" (pure code, no AI): two CRS indicia, each with its rule and page, and
the deadline: 90 days from the date the bank learned of the change, with a reminder on day 85.

![The verdict of the code: Clarification, CRS-IND-ADDR on p. 29, deadline 27 Jan 2027 under DL-NEW-90](docs/screenshots/change-maria.png)

**2. The agent prepares the file, and stops for approval.** On the case page, "Analyze the file" starts one run of the
agent: it reads only the visible text of the documents (after the shield), records the evidence and the signals with
quotes that code checks on the page, and drafts the request; the list of documents comes from code. Sending it is a tool
marked `needsApproval`: the run stops until the operator approves.

![The agent run: getCase, listDocuments, readDocument, recordEvidence, recordSignal, draftClientRequest, then "Approval needed"](docs/screenshots/agent-approval.png)

**3. Indicia and signals, with verified quotes.** The indicia come from code and start the deadline. The salary from
Müller Logistik GmbH is only a signal: shown with its quote, no alert. Every step is in the case log, hash-chained.

![Indicia from the law with "Quote verified", the context signal "Signal, no alert", the correspondence and the case log](docs/screenshots/case-evidence.png)

**4. Day 85, the answer, the closing.** The "Demo" menu moves the simulated date to the reminder day (the case is
escalated to the tax department), then brings Maria's answer: a new self-certification. "Check the answer" extracts the
jurisdictions and the TIN (each field with a verified quote), `checkTin(DE)` gives "format valid", code re-evaluates the
rules (`SC-VALID`), and the closing waits for the operator's approval. The case closes with the proof of confirmation:
channel, time, who signed and who approved, SHA-256.

![Case CRS-2026-0008 closed under SC-VALID, reportable for DE, with the proof of confirmation](docs/screenshots/case-closed.png)

With no answer, on day 91 the case moves to `Expired`, with the consequence from the guide (resident in RO and in DE).
The full diagram, with all participants: [`docs/PLAN.md` §6.3](docs/PLAN.md#63-fluxul-mariei-traseul-din-video).

### The traps (no false alarms)

| The change | The result | Why |
| --- | --- | --- |
| Move Cluj → Iași | `NoCase`, explained | No rule from p. 29–31 applies (design interpretation, `GEN-DOMESTIC`) |
| German phone next to the Romanian phone | `NoCase` | The CRS rule requires "niciun telefon în România" ["no phone number in Romania"] (p. 29 (c)) |
| Standing order to a German IBAN, from the current account | `NoCase`, explained | Orders from depository accounts are excluded under CRS (p. 29 (d)) |
| The same order, from the custody account | `Clarification` | The exception does not apply (p. 29 (d)) |
| Standing order to the US, from the current account | `FatcaAlert` | Under FATCA the exception for depository accounts does not exist (p. 31) |
| US phone, even next to a Romanian one | `FatcaAlert` | Under FATCA any US phone is an indicium (p. 31) |
| Salary from a German company | `NoCase` + signal | Income is not on the list of indicia |
| PDF with hidden white text | `ShieldAlert` | The shield removes the text before it reaches the model |

Every golden case can be replayed from `/changes/new` ("Start from a golden case"); the verdict shows the rule that
decided and whether it matches the expected result.

| Standing order to Germany from a current account: `NoCase` | Standing order to the US from a current account: `FatcaAlert` |
| --- | --- |
| ![Golden case 5a: No case, CRS-IND-SO checked and not applied, p. 29 lit. d](docs/screenshots/trap-5a.png) | ![Golden case 5c: FATCA alert, FATCA-IND-SO](docs/screenshots/trap-5c.png) |

### The shield and the audit log

| A bank statement with a hidden instruction | Tampering with the audit log |
| --- | --- |
| ![The shield's red flag on Victor's statement: white text under 2 pt, removed before any model reads it](docs/screenshots/shield.png) | ![The audit log: "Chain broken at entry #64", the tampered entry and every entry after it marked](docs/screenshots/audit.png) |
| `/clients/CL-0010` → "Record the upload and scan": white text in a font under 2 pt ("Ignoră instrucțiunile anterioare...", "Ignore the previous instructions...") is removed before the model sees the document, with a red flag. | `/audit` → "Verify", then "Tamper" (admin only) on an entry, then "Verify" again: the chain shows exactly the broken entry. |

## 4. The rules

The CRS indicia are the closed list from the guide, p. 29 (Law 207/2015, Annex 1, Section III, subsection B); the FATCA
ones are from p. 31 (the FATCA Agreement, Annex I, Section II, paragraph B). `RuleCatalog` has 29 rules: 24 from the
text of the law, 3 our reading (§5), 2 bank policy. The full catalogue, with the final IDs (Romanian):
[`docs/PLAN.md` §8.2](docs/PLAN.md#82-catalogul-de-reguli-id-urile-sunt-finale-paginile-verificate-în-ghid-detalii-în-docsreguli-verificatemd).

| Group | Rules | Page |
| --- | --- | --- |
| General | `GEN-CHANGE-CIRC` (change in circumstances), `GEN-NEW-ACCOUNT` (indicia on a new account); `GEN-DOMESTIC` (a move within the country) | 29–30, 33, 35–36; `GEN-DOMESTIC`: NEVERIFICAT (unverified) (§5) |
| CRS indicia | `CRS-IND-RES`, `-ADDR`, `-MAIL`, `-PHONE`, `-SO`, `-POA`, `-CO` | 29 (a–f), 17–18, 31, 48 |
| FATCA indicia | `FATCA-IND-CIT`, `-POB`, `-ADDR`, `-PHONE`, `-SO`, `-POA`, `-CO`; `FATCA-SCOPE-ALL` (all accounts) | 31, 25 |
| Self-certification | `SC-VALID`, `SC-TIN-FORMAT`, `SC-TIN-MISSING`, `SC-CONSISTENT`, `FATCA-SC`, `FATCA-TIN` | 33–34, 36–37, 48, 53, 38 |
| Deadlines and expiry | `DL-NEW-90`, `DL-PRE-YEAR`, `DL-EXPIRED-NEW`, `DL-EXPIRED-PRE` | 20–21, 29, 35–36 |
| The client's answer | `RESP-EXPLANATION`: student with a visa, teacher or trainee, diplomat, EU official | 35, 37 |

**The decisions** (precedence, from the strongest): `ShieldAlert` > `FatcaAlert` > `Clarification` > `Compliant` >
`NoCase`. `NoCase` = no indicium rule applied; `Compliant` = there are indicia, but all of them are covered by the
self-certification or by an accepted explanation.

**The deadlines** depend on whether the account is new or preexisting, not on the product. The status is calculated per
regime from the opening date: CRS preexisting = existing on 31.12.2015, FATCA preexisting = existing on 30.06.2014
(p. 20).

| Account | Deadline | On expiry | Page |
| --- | --- | --- | --- |
| New | 90 calendar days from the date the bank knows of the change | Resident both in the original jurisdiction and in the new one | 20–21, 35–36 |
| Preexisting | The last day of the year or 90 days; we implement the later of the two | Resident at least in the new jurisdiction | 29 |

## 5. The design interpretations (stated openly)

- **"Știe sau are motive să știe" ["knows or has reason to know"].** We take the data the bank already holds (addresses,
  phone numbers, standing orders, powers of attorney) to be the data for which the bank knows or has reason to know that
  the self-certification may be unreliable (p. 30, 35).
- **Indicia on new accounts.** In the guide, the list of indicia is the procedure for preexisting accounts (p. 29, 31).
  For new accounts, the guide requires the self-certification and the reasonableness test against the bank's information
  (p. 33). We use the indicia as the criteria by which the self-certification is contradicted.
- **NEVERIFICAT: a move within the country** (`GEN-DOMESTIC`) has no page of its own in the guide (in code:
  `RuleCatalog.NotVerified`): no rule from p. 29–31 applies, so `NoCase`.
- **The deadline on a preexisting account.** The guide (p. 29) says "până în ultima zi a anului calendaristic în cauză
  [...], sau în 90 de zile calendaristice" ["by the last day of the relevant calendar year [...], or within 90 calendar
  days"], in the paragraph about the residence address test (footnote 57). We apply the later of the two dates. For an
  account documented through the residence address, this is what the law says (the Fiscal Procedure Code, Annex no. 2,
  point 1: "oricare dintre acestea este mai recentă" ["whichever of these is later"]), which transposes Directive
  2011/16/EU, Annex II, point 1 (introduced by Directive 2014/107/EU): "by the later of the last day of the relevant
  calendar year or other appropriate reporting period, or 90 calendar days following the notice or discovery of such
  change in circumstances" ([EUR-Lex](https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32014L0107); the quote
  is verified on the [legislation.gov.uk copy](https://www.legislation.gov.uk/eudr/2011/16/annex/II), 28.10.2026), and
  the OECD Commentary on the CRS (Section III, para. 13). The Directive has the same limit: it applies when the bank "has
  relied on the residence address test". For indicia from the electronic search, the law says only "ori" ["or"]
  (Annex no. 1, Section III, subsection B, point 16), the guide (p. 30) gives no deadline, and the OECD Commentary
  (para. 16) says "until the later of". The system does not know through which procedure the account was documented, so
  it applies the same rule and treats all cases as indicia from the electronic search (for example 17b): that part is
  our interpretation.
- **NEVERIFICAT:** for FATCA, the guide (p. 38) gives no deadline for a change in circumstances: the case opens with no
  deadline from the guide, with NEVERIFICAT shown, and we do not borrow the 90 CRS days.

In the app, every rule that rests on our reading carries an "Our reading" badge next to its ID and page.

## 6. Architecture

```mermaid
flowchart LR
  CE[Data change] --> RE[CrsRuleEngine<br/>rules with ID + page]
  RE -->|NoCase / Compliant| AU[(AuditLog<br/>SHA-256 chain)]
  RE -->|indicia| CS[CaseService<br/>states + deadline]
  CLK[SimClock] --> DL[DeadlineCalculator] --> CS
  CLK --> W[DeadlineWatcher<br/>reminder · escalation · expiry]
  DOC[PDFs] --> SH[HiddenTextShield] --> AG
  CS --> AG[TRACE agent<br/>Claude on Bedrock]
  AG -->|quotes| EV[EvidenceVerifier]
  AG -->|TIN format| TIN[TinFormat + TIN on Europa]
  AG -->|evaluateRules| RE
  AG -->|request · closing| OP[The operator approves]
  RE & CS & AG & OP & W --> AU
```

**One agent with 14 tools**, orchestrated by code, with the human making the decision. In code it keeps its working
name: `CrsSentinelAgent` in `Features/Sentinel`, registered as `crs-sentinel`. The tools: `getCase`,
`getClientSnapshot`, `evaluateRules`, `listDocuments`, `readDocument`, `recordEvidence`, `recordSignal`,
`extractSelfCertification`, `checkTin`, `draftClientRequest`, `sendClientRequest`, `registerResponse`, `closeCase`,
`escalate`. The agent is called only when there is a document to read, evidence to extract or a letter to write; it
never gets the last word.

- **Pure code** (`Rules`, `Deadlines`, `Shield`, `Tin`, the hashing in `Audit`): no EF, no AI, no HTTP. It takes an
  immutable `ClientSnapshot` and returns a result, so it is tested in milliseconds.
- **Services** (`ChangeService`, `CaseService`, `ClientService`, `EvaluationService`): validation, roles, transactions,
  audit log. The case transitions live only here, each with a role check and an audit log entry.
- **The agent** does not touch the database directly; who opens the case, the rules and the deadlines are in code. The
  runtime asks for the human's approval for the tools marked `needsApproval` (`sendClientRequest` and `closeCase`) and,
  after approval, runs them without the model. A four-eyes warning appears when the approver also drafted the request.
- **The simulated date** (`SimClock`) is a separate service with a settable offset ("+85 days"). The CRS code does not
  read the time from anywhere else; the rest of the app stays on the real clock.
- **The top bar** always shows the kind of data ("Synthetic data"), the agent's mode (for example "Agent: simulated,
  real tools" on the mock) and the simulated date, marked when it is ahead of the real one.
- The entities, interfaces and folder structure (Romanian): [`docs/PLAN.md` §6–13](docs/PLAN.md#6-arhitectura). The
  live version of this section is the `/architecture` page, built from the registered rules, tools and audit chain.

### Trust

| The piece | What it guarantees |
| --- | --- |
| `HiddenTextShield` (PdfPig, letter by letter) | White, microscopic, off-page or invisible text is removed **before** it reaches the model; the document gets a red flag |
| `EvidenceVerifier` | A quote exists in the visible text of the indicated page. An invented quote shows up as "Quote not found"; one that exists only in the hidden text is rejected with a red flag ("Only in hidden text") |
| `TinFormat` + TIN on Europa | The TIN format per country (DE, IT, AT, ES, FR, US, RO); for EU states, a check with the European Commission's service, which also confirms only the structure and syntax. We say "format valid", never "TIN valid" |
| `AuditLog` | Every step is an entry with `Hash = SHA-256(previous hash + content)`; "Verify" finds exactly the modified entry |
| `PiiVault` | CNP, IBAN, card, e-mail and phone numbers (Romanian and international, with `+` or `00`) are masked before the model; the tools work with the real values; runs, prompts and tool results are saved masked (the quotes from the client's documents are kept with the case, as part of the case file) |
| Checks on the answer (`Features/Checks`) | Two checks run on every answer of the agent: `pii-leak` (no personal data leaked) and `evidence-quoted` (the quotes are among the tool results). `CrsChecks` also has `signals-never-alert`, `rule-cited` and `no-tin-valid-claim`, tested but not active on the agent |

### The models and the fallback

- Amazon Bedrock, region **us-west-2** (the workshop account blocks Bedrock in `eu-central-1`), with synthetic data only.
  In production: an EU region or a model on the bank's servers (OpenAI-compatible endpoint).
- Two profiles: `extract` (Claude Haiku 5.5) for small extractions and classifications; the main one (Claude Sonnet 5.5)
  for the agent and the text to the client.
- **The chain that does not fail:** live → recording (`Recordings/crs-sentinel.json`, read-only live runs on Bedrock) →
  mock. The mock calls the real tools, so the app works even without internet or keys. Replay does **not** run the
  tools: we use it only for read-only questions (for example "summarize the case"), never for steps that change data.

## 7. MVP scope

**In the MVP:** individuals; changes in circumstances after onboarding (the core); validation of a new self-certification
(secondary case).

**What we do not catch yet:**

- company accounts and the persons who control them;
- the F3000 file and the W-8BEN forms;
- text covered by a shape drawn over it, in a PDF; OCR on poor scans;
- whether a TIN exists (we check only the format);
- high-value preexisting accounts (over USD 1,000,000): there the OECD Commentary (Section III, para. 49, p. 125) speaks
  only of the 90 days from the identification of the indicium;
- real integration with the bank's systems, ONRC or ANAF. In the demo, a form simulates each change; in a bank, core
  banking would send every change to the same service, and the same rules would run.

## 8. The golden cases and the metrics

The correct result is written **before** the code: 18 scenarios (21 cases, with variants 5a–5c and 17a–17b), each with
the exact input, the decision, the expected rules and the page from the guide, in
[`docs/GOLDEN-CASES.md`](docs/GOLDEN-CASES.md) (Romanian). Besides them, 50 synthetic clients (same seed → same data),
labelled by the scenario chosen at generation, not by the engine's result.

The metrics come only from real runs (`/metrics` and the tests), presented separately:

1. **Specification coverage:** the cases pass or they do not. The engine is deterministic, so, when all cases pass, we
   say "the rules from the guide are implemented and tested", not "100% accuracy".
2. **Where there is uncertainty:** field-by-field extraction from PDFs, the percentage of quotes verified and rejected,
   the shield on varied PDFs, the signals that would have changed the decision (0 by construction, checked on every run).
3. **Operational efficiency:** the time per case with the agent, only when it can be measured from a run. We do not
   compare it with a manual time: we have no estimate from the mentor.

![The /metrics page: golden cases 13 of 13 expected actions caught, 0 missed, 0 false alarms, 20 of 20 passed; synthetic clients 10 of 10, 50 of 50](docs/screenshots/metrics.png)

### The latest run

The run on commit `9e67e06` (`main`), 29.10.2026, **without a model** (no key, no AI call), from the `/metrics` page
with the app started locally on the mock. All figures below come from deterministic code, not from AI.

**1. Specification coverage** (engine `crs-rules/1.2`): the rules from the guide are implemented and tested, on 20
golden cases with input data and on 50 synthetic clients.

| Set | Evaluated | Pass | Expected actions | Caught | Missed | False alarms |
| --- | --- | --- | --- | --- | --- | --- |
| The golden cases | 20 (case 10 is at the shield) | 20 | 13 | 13 | 0 | 0 |
| The synthetic clients | 50 | 50 | 10 | 10 | 0 | 0 |

**2. Where there is uncertainty**

| What | Result |
| --- | --- |
| The shield, on 12 generated PDFs | 4 with hidden text (5 texts: white 1, microscopic 2, off-page 1, invisible 1): all 5 detected, 0 missed; 8 clean, 0 false alarms |
| The quotes, on the `EvidenceVerifier` test bench (fixed quotes, not the model's) | 10 of 10 true quotes verified; 4 of 4 quotes only from the hidden text rejected; 38 of 38 invented quotes rejected |
| Extraction of the self-certification by the AI | not measured: it is measured only from a live run, never from the mock |
| The signals that would have changed the decision | 0, by construction (the engine does not receive signals), checked at run time |

**3. Operational efficiency:** not measured. The agent's runs are not linked to cases yet, and the manual time per case
has no source.

No figure without a run: on every new run, the table is redone with the commit, the date and the mode.

## 9. Privacy

- **Synthetic data only.** The clients, documents and TINs are generated; identity documents carry "SPECIMEN".
- **The legal basis** is the legal obligation (Art. 6(1)(c) GDPR). The Fiscal Procedure Code (Law 207/2015) requires the
  bank to apply the tax due diligence procedures from annexes no. 1 and 2 (Art. 291(4)) and to report the accounts to
  ANAF every year (Art. 62(1)); for FATCA, the procedures from the FATCA Agreement (Art. 62(3), Law 233/2015). The
  quotes are in [`docs/PRIVACY-TEXT.md`](docs/PRIVACY-TEXT.md#7-surse) (Romanian).
- **Retention:** the agent's runs are deleted automatically after `Privacy:RetentionDays` (default 30 days). No
  retention rule deletes the clients, the cases or the audit log. The law requires the bank to keep the tax due diligence
  and reporting records, and the evidence they relied on, for 10 years from the 15 May reporting deadline
  (Art. 62(11)).
- **Art. 22 GDPR does not apply:** no decision is taken solely by automated means. The code applies the rules, the human
  approves the request and the closing.
- **Masking** of CNPs, IBANs, card numbers, e-mails and phone numbers (Romanian and international, with `+` or `00`)
  before the model serves data minimisation (Art. 5(1)(c) GDPR) and the security of processing, through
  pseudonymisation and encryption (Art. 32(1)(a) GDPR).
- Names, addresses, dates of birth and foreign TINs are not masked before the model (the data is synthetic). The
  `/privacy` page shows, from the live configuration, what reaches which model, what is saved and for how long.

## 10. Quick start

```bash
git clone https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon.git
cd nexxt-ai-summit-hackathon
tools/setup-dev.sh                              # once: checks .NET 10, builds, installs Chromium for the E2E tests
dotnet run --project src/HackathonStarter       # http://localhost:5000, runs on the mock with no key
dotnet test tests/HackathonStarter.Tests        # unit tests, no network
dotnet test tests/HackathonStarter.E2E          # browser tests; they start the app themselves
```

Dependencies: only the .NET 10 SDK (`global.json`); `tools/setup-dev.sh` checks it, runs the build and installs Chromium
for E2E on Linux and macOS (Intel and Apple Silicon). No Docker, no external database, no keys.

Or open `HackathonStarter.slnx` in Visual Studio, Rider or VS Code and start the `HackathonStarter` project. The SQLite
database is created on startup, with the demo data. Demo users (the menu in the top bar): Ana Popa (operator, admin),
Mihai Stan (tax department, member), Vera Ionescu (viewer, cannot approve).

**Sample inputs** are in the app: 50 synthetic clients, the golden-case PDFs, "Start from a golden case" on
`/changes/new` and the "Demo" menu (the simulated date, "The client answers", the demo reset).

**Live Bedrock (optional):** credentials come only from the AWS default chain (environment variables, local profile or
IAM role), never from the repo. In `appsettings.Local.json` (ignored by git): `"AI": { "Provider": "Bedrock", "Region":
"us-west-2", ... }`, following the example in `appsettings.Local.example.json`.

**Deploy:** Render, from `render.yaml` and `Dockerfile`; `main` is published only after CI is green
([`docs/DEPLOY.md`](docs/DEPLOY.md)).

**The screenshots** in this README can be regenerated: start the app on the mock, reset the demo data, then run
`dotnet run tools/Screenshots/screenshots.cs -- http://localhost:5000 docs/screenshots en`. The video route is driven
by [`tools/DemoFilm`](tools/DemoFilm/README.md) (Playwright, at the pace of the voice-over).

It also works on a phone (PWA, bottom navigation, checked at 390 px):

<img src="docs/screenshots/mobile-dashboard.png" alt="The TRACE dashboard on a 390 px phone screen, with the bottom navigation" width="260">

## 11. Status

The state of `main` at commit `9e67e06`, checked locally on 29.10.2026: `dotnet build -warnaserror` with 0 warnings,
unit tests 1602 of 1604 passed (2 skipped: the live tests on Bedrock, which run only with `CRS_LIVE=1`), E2E 52 of 52.
The app starts without `appsettings.Local.json` and without AWS credentials, on the mock, and the full demo route
(`tools/DemoFilm`, S1–S9: Maria end to end, the traps, the shield, the audit log, the metrics) runs on it without an
error. Every PR goes in only with green CI (build with 0 warnings, unit and E2E tests).

![The public landing page of TRACE: "Every data change, a CRS/FATCA decision before the deadline"](docs/screenshots/landing.png)

**The pages:**

| The page | What it shows |
| --- | --- |
| `/` | The public landing page |
| `/dashboard` | The CRS cases: open, with a deadline within 7 days, awaiting approval, escalated; the upcoming deadlines; who the cases wait for; the code's decisions on the latest changes |
| `/changes/new` | Records a change (or a golden case) and shows the rule engine's verdict on the spot, with the decisive rule, its page and the deadline |
| `/cases`, `/cases/{code}` | The case queue and the case record: the stepper, the deadline, the indicia with the rule and the page, the signals, the evidence, the request (RO/EN letter), the correspondence, the case log, the agent panel ("Analyze the file", "Check the answer", the approvals) and the closing with the proof of confirmation |
| `/clients`, `/clients/{code}` | The client record: addresses, phones, accounts (new or preexisting per regime), standing orders, self-certifications with the TIN format, documents with the shield's verdict, cases |
| `/audit` | The hash chain, "Verify" (everyone) and "Tamper" (admin only) |
| `/metrics` | The three kinds of figures from `EvaluationService`, with "Run again", case by case |
| `/runs`, `/runs/{id}` | The agent runs, with the steps, the checks and "Approve" / "Reject" |
| `/architecture` | How TRACE decides, from the live rules, tools and audit chain |
| `/privacy` | What reaches which model, what is saved and for how long, the legal basis |
| `/settings` | "Reset demo data" (admin) brings the CRS data, the audit log and the simulated date back to their state at startup |

## 12. Documentation

| Document | What it contains |
| --- | --- |
| [`docs/presentation/TRACE.pdf`](docs/presentation/TRACE.pdf), [`docs/PRESENTATION.md`](docs/PRESENTATION.md) | The 10 slides and their notes |
| [`docs/PITCH.md`](docs/PITCH.md), [`docs/SUBMISSION.md`](docs/SUBMISSION.md) | The video script, the submission (the form, the links, the jury criteria) |
| [`docs/PLAN.md`](docs/PLAN.md) (Romanian) | The implementation plan: the domain, the data model, the rules, the architecture, the roles, the schedule |
| [`docs/CUM-FUNCTIONEAZA.md`](docs/CUM-FUNCTIONEAZA.md) (Romanian) | The system end to end, understandable by anyone on the team |
| [`docs/reguli-verificate.md`](docs/reguli-verificate.md) (Romanian) | Every rule, verified against the text of the guide, with its page |
| [`docs/GOLDEN-CASES.md`](docs/GOLDEN-CASES.md) (Romanian) | The exact data of the golden cases |
| [`docs/RESEARCH-CRS.md`](docs/RESEARCH-CRS.md) (Romanian) | The facts from the guide and the questions for the mentor |
| [`AGENTS.md`](AGENTS.md) | The code conventions, for people and for AI assistants |
| [`docs/TEMPLATE.md`](docs/TEMPLATE.md) | The template reference: the agent runtime, configuration, the HTTP API, the commands |

## 13. The template

We started from the team's .NET template, written before the hackathon (commit `381d8c2` in the repo history): .NET 10,
Blazor Server, MudBlazor, EF Core, Microsoft.Extensions.AI. From there come the agent runtime (tools, approvals,
background runs, recordings, mock), the masking of personal data, the deterministic checks, `TargetClient`, the
synthetic data, the PWA and the deploy pipeline. Everything related to CRS was written during the hackathon, starting
on 28 October 2026. The [rulebook](https://nexxtsummit.com/rulebook) does not forbid code written beforehand and only
requires that work on the challenge start at the official start (28 Oct, 09:00); the organisers confirmed this on
28 Oct. The template documentation: [`docs/TEMPLATE.md`](docs/TEMPLATE.md).
