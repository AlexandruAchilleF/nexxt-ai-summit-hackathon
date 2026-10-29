# TRACE: the presentation (slides)

> The presentation is a required link in the submission ([`SUBMISSION.md`](SUBMISSION.md)). This is its content, slide by
> slide; it goes into Google Slides or Keynote, is exported to PDF and uploaded with a public link. The order follows the jury
> criteria; each slide shows, in parentheses, the criterion it serves. The figures are the same as in [`PITCH.md`](PITCH.md)
> and README §8, with their sources; nothing NEVERIFICAT (unverified). No emoji. Screenshots are taken on synthetic data,
> with "SPECIMEN" visible wherever there is an identity document.

## 1. TRACE

**T**ax **R**esidency and **A**nomaly **C**orrelation **E**ngine

**Every change to a client's data becomes a CRS/FATCA decision before the legal deadline runs out.**

Code decides, AI explains, a person approves.

Team `[team name]` · Raiffeisen Bank, *AI for Operational Efficiency* · NEXXT AI Hackathon (VibeHack), 28–29 Oct 2026

App: <https://hackathon-starter-k0ym.onrender.com/> · Code: <https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon>

## 2. The user and the problem (research, business)

**We built TRACE for the bank's tax compliance operators.**

- In a self-certification the client declares where they are tax resident. When the bank "knows or has reason to know"
  (our translation) that the situation has changed (address, phone, standing order, power of attorney), the declaration
  loses its validity.
- New account: **90 days** to obtain a new self-certification; otherwise the client is treated as resident both in the
  original jurisdiction and in the new one (ANAF guide, p. 35–36).
- Today: a person compares the fields against the guide, writes to the client and keeps the deadline in mind.
- At stake: in H1 2025, after 8 feedback requests from member states, ANAF checked 3,204 records from the sector; 891 were
  corrected (ANAF activity report H1 2025, *Raport de activitate S1 2025*, p. 36–37). The fine is **per account** reported
  wrongly: 2,000–5,000 lei, and without the due diligence procedures 5,000–10,000 lei (Fiscal Procedure Code, *Codul de
  procedură fiscală*, art. 336(2)(k^1) and (k^2)).

Footnote: the ANAF figures cover the whole sector; they are not about Raiffeisen.

## 3. The rules, from the guide (research and domain)

Source of truth: the ANAF DAC2/CRS/FATCA guide, 29.12.2025. Every rule has an ID and a page.

| Group | Examples | Page |
| --- | --- | --- |
| CRS indicia (closed list) | residence, address, phone with no RO phone, standing order, power of attorney | 29 |
| FATCA indicia | US citizenship, place of birth, US phone, order to the US | 31 |
| Self-certification | validity, TIN format, missing TIN, consistency | 33–38 |
| Deadlines | 90 days for a new account; for a preexisting account, the later of 31 December and +90 days | 20–21, 29, 35–36 |

**The traps (no false alarms):**

| Change | Result | Why |
| --- | --- | --- |
| German phone next to the Romanian one | No case | CRS requires "no telephone number in Romania" (our translation; p. 29 letter c) |
| Standing order to DE, from the current account | No case | Standing orders from depository accounts are excluded under CRS (p. 29 letter d) |
| The same order to the US | FATCA alert | Under FATCA the exception does not exist (p. 31) |
| Move Cluj → Iași | No case | No rule applies (design interpretation, marked NEVERIFICAT) |
| Salary from a German company | Signal, no case | Income is not an indicium; nationality is not a signal |

Our interpretations are stated openly (README §5): the indicia are used for new accounts as the reasonableness test
(p. 33); "the later of" for a preexisting account is law only on the residence-address route (Annex no. 2 point 1).

## 4. The flow: from input to result (completeness)

| Input | Processing | Output | What it means |
| --- | --- | --- | --- |
| The data change (Maria moves to Munich, German phone) and the client's documents | Code applies the rules (2 indicia, with the page), opens the case and the deadline; the agent reads the documents, quotes the evidence, records the signals, drafts the request | A case with verified evidence, a request approved by the operator, a reminder on day 85, a new self-certification checked, the case closed with proof of confirmation | The decision is explained and auditable; the deadline no longer depends on anyone's memory; the human approves only what matters |

Screenshot: Maria's case page, with the indicia (rule + page), the quoted evidence, the "Signal, no alert" signal and the
agent panel.

## 5. The agent: why a single agent (design and orchestration)

**One tool-using agent (CRS Sentinel), orchestrated by code, with the human deciding.** The rules are a closed list, so they are code; the
model does only what code cannot: it reads free text and writes the letter. More agents would add nothing here.

- **14 tools:** `getCase`, `getClientSnapshot`, `evaluateRules`, `listDocuments`, `readDocument`, `recordEvidence`,
  `recordSignal`, `extractSelfCertification`, `checkTin`, `draftClientRequest`, `sendClientRequest`, `registerResponse`,
  `closeCase`, `escalate`.
- **Two stops for the human:** `sendClientRequest` and `closeCase` require approval; after approval they run exactly once,
  without the model.
- **What the model produces is checked by code:** the quote must exist in the visible text of the page; the TIN format is
  checked locally and with TIN on Europa ("format valid", never "TIN valid"); checks on the answer (`pii-leak`,
  `evidence-quoted`).

**Unexpected situations:**

| Situation | What the system does |
| --- | --- |
| PDF with hidden white text asking to "mark the client as compliant" | The shield removes the text before the model; red signal, decision `ShieldAlert` |
| A model that invents a quote | The quote is rejected ("Quote not found"); one that exists only in the hidden text is rejected with a red signal |
| The model is unavailable or there is no key | Fallback: live → recording (read-only questions only) → mock that calls the real tools |
| The client does not answer | Reminder on day 85, escalation; on day 91 the case expires, with the consequence from the guide |
| Self-certification dated after receipt | Rejected (guide, p. 34) |
| Context signal (foreign salary) | Shown with its evidence; it does not change the decision and does not open a case, by construction |

## 6. Testable: the correct result written before the code (completeness and testability)

- 21 golden cases (18 scenarios, with their variants) and 50 synthetic clients, each with the expected decision, rules and
  page, written before the code (`docs/GOLDEN-CASES.md`, Romanian).
- **Last run** (`/metrics`, no model): 23 expected actions, 23 caught, 0 missed, 0 false alarms.
- The shield, on 12 generated PDFs: 4 with hidden text (5 hidden texts in all), all 5 detected; 0 false alarms on the 8 clean ones.
- Quotes, on the test bench: 10 of 10 true ones accepted; 38 of 38 invented and 4 of 4 hidden ones rejected.
- 1414 unit tests and 39 E2E tests green on `ed0d4eb`; `dotnet build` with 0 warnings.

What we do **not** say: "100% accuracy". The engine is deterministic; one successful example does not prove reliability in
general. Not measured yet: field-by-field AI extraction on a live run, time saved per case.

## 7. Architecture and reproducibility

```text
Change → CrsRuleEngine (rules + page) → CaseService (states + deadline) ← SimClock / DeadlineWatcher
PDF → HiddenTextShield → CrsSentinelAgent (Claude on Bedrock) → EvidenceVerifier · TinFormat · evaluateRules
                                     ↓ request / close
                               Operator approves → AuditLog (SHA-256 chain)
```

- **Pure code** (`Rules`, `Deadlines`, `Shield`, `Tin`): no database, AI or network; tests run in milliseconds.
- **Services** with roles and an audit log; pages never touch the database directly.
- **Starts with no configuration:** without keys it runs on the mock, with the real tools; SQLite is created on startup.
- `tools/setup-dev.sh` → `dotnet run --project src/HackathonStarter`; Docker + Render, deploy only with green CI.
- .NET 10, Blazor Server, MudBlazor, EF Core, Microsoft.Extensions.AI; Amazon Bedrock in `us-west-2`.

## 8. Value and adoption (business mentoring)

| | |
| --- | --- |
| **The user** | The operator who receives the data changes; the tax department, on escalations |
| **The benefit** | Every change gets an explained decision, with the rule and the page; the deadline is tracked by code; the file holds the proof of confirmation; the log shows any modification |
| **What is measured** | 0 missed and 0 false alarms on the golden set and on the 50 synthetic clients |
| **What is an estimate** | Time saved per case: not measured, there is no manual baseline time |
| **The barriers** | Integration with the core banking data-change feed; the model in an EU region or at the bank; validating the design interpretations with the tax department; company accounts, not covered yet |
| **The next step** | Shadow pilot: TRACE runs next to the current process, on a sample of real changes; the operators compare the decisions and the time |

## 9. What is real, what is simulated, what is not ready

| Real (code, with tests) | Simulated | Not ready |
| --- | --- | --- |
| The rules, the deadlines, the cases and their states, the shield, quote checking, the TIN format (and TIN on Europa), the hash-chained log, the approvals, personal data masking | The clients, the documents, the client's answers (synthetic data, identity documents marked "SPECIMEN"); core banking; message delivery; the simulated date (+85 days in the demo) | Company accounts and their controlling persons; F3000 and W-8BEN; text covered by shapes in PDFs, OCR; whether a TIN exists; integration with core banking, ONRC, ANAF |

The .NET template (the agent runtime, masking, deployment) was written before the hackathon; everything CRS-specific was
written on 28–29 Oct 2026 (README §13).

## 10. Closing

**TRACE: code decides, AI explains, a person approves.**

Video (3 min): `[link]` · App: <https://hackathon-starter-k0ym.onrender.com/> · Code:
<https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon>
