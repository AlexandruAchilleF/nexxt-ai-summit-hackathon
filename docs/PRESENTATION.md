# TRACE: the presentation, slide by slide

> The submitted presentation is [`presentation/TRACE.pdf`](presentation/TRACE.pdf) (10 slides), exported from
> [`presentation/TRACE.pptx`](presentation/TRACE.pptx). The PDF is the submitted version and is not edited after the
> deadline of 29 Oct 2026, 11:00. This file is its companion: for each slide, the text on the slide (transcribed from the
> PDF, condensed into bullets, with no new claims), then where every figure comes from in the repo, then the speaker
> notes as they are in the pptx (only lightly formatted), then our own notes. Our notes say where a figure has no
> source left in the repo, where the slide says less than the source, and where the speaker notes claim more than the
> slide.

## 1. Title: TRACE

On the slide:

- Raiffeisen Bank Romania · AI for Operational Efficiency
- **TRACE**, "Tax Residency and Anomaly Correlation Engine"
- "When a customer's data changes, the bank must find out again where they pay tax. For a new account it has 90 days.
  **Today, it is done by hand.**"
- NEXXT AI Hackathon · 29 October 2026

Sources:

- 90 days for a new account: ANAF guide, p. 35–36 ([`ghid/pagini/p-035.md`](ghid/pagini/p-035.md),
  [`ghid/pagini/p-036.md`](ghid/pagini/p-036.md)); README [§1](../README.md#1-the-problem) and
  [§4](../README.md#4-the-rules) (`DL-NEW-90`, p. 20–21, 35–36).
- "Done by hand": our description of the current process (README [§1](../README.md#1-the-problem)). It is not measured,
  and we did not interview operators ([`PITCH.md` §5b](PITCH.md#5b-business-mentoring-the-user-the-benefit-the-barriers)).

Speaker notes (from the pptx):

> Open with the problem, not the technology.
>
> When a customer changes their address, phone, standing order or power of attorney, the bank must ask one question
> again: where does this customer pay tax now? For a new account, the ANAF guide gives the bank 90 days to get a new
> self-certification. Today, an operator answers that question by hand, against the guide, and keeps the date in mind.
>
> TRACE turns every data change into a decision before the deadline: code decides, AI explains, a person approves.

Notes:

- The speaker notes add the four kinds of change (address, phone, standing order, power of attorney): README
  [§1](../README.md#1-the-problem) lists the same four. The manual process is our description, as above.
- The expansion of the name on the slide differs from the rest of the project. The slide says "Tax Residency **and**
  Anomaly Correlation Engine"; the app (`SiteConfig.FullName`), the landing page test, README and
  [`SUBMISSION.md`](SUBMISSION.md) say "Tax Residency Anomaly Correlation Engine". The PDF stays as submitted; the app's
  form is the reference.
- The hackathon ran on 28–29 October 2026; the date on the slide is the day of the presentation.

## 2. The pressure: "The regulator is already checking the data."

On the slide:

- Records reported by financial institutions, checked by ANAF in H1 2025: **3,204** checked, **891** corrected.
- **28%** of checked records needed a correction, sector-wide.
- **2,000–10,000 lei** fine for each account reported wrongly.
- **2.4 million** Raiffeisen customers whose tax residency must stay current.
- Footer: ANAF activity report H1 2025, p. 36–37 (sector-wide) · Fiscal Procedure Code, art. 336(2)(k^1), (k^2) ·
  Raiffeisen Bank Romania press release, 28.10.2026.

Sources: [`PITCH.md` §3](PITCH.md#3-notes-sources-for-the-figures-and-the-rules), notes 1–3, with the quotes and links;
the same figures in the answers of [`PITCH.md` §6](PITCH.md#6-likely-questions-and-answers).

| Figure | Source | Check |
| --- | --- | --- |
| 3,204 | ANAF activity report H1 2025, p. 36: 3,204 specific checks of the data from 28 reporting financial institutions, after 8 feedback requests from member states | OK |
| 891 | Same report, p. 37: corrections for 891 records | OK |
| 28% | 891 / 3,204 = 27.8%, rounded | OK (computed) |
| 2,000–10,000 lei | Art. 336(2)(k^1): 2,000–5,000 lei per reportable account (late, incorrect or incomplete reporting); (k^2): 5,000–10,000 lei per reportable account when the due diligence procedures were not applied | OK, see note |
| 2.4 million | Raiffeisen press release, 28.10.2026: "aproximativ 2,4 milioane de clienți, persoane fizice și juridice" (about 2.4 million customers, individuals and companies) | OK, see note |

Speaker notes (from the pptx):

> The pressure is real and it is current. In the first half of 2025, after 8 feedback requests from other EU states,
> ANAF checked 3,204 records reported by financial institutions; 891 had to be corrected, about 28%. These are
> sector-wide figures, not Raiffeisen's.
>
> The fine is per account reported wrongly: 2,000 to 5,000 lei, and 5,000 to 10,000 lei without due diligence
> procedures.
>
> Raiffeisen serves about 2.4 million customers whose tax residency has to stay current every time their data changes.

Notes:

- The ANAF figures cover the whole sector; they are not about Raiffeisen.
- "8 feedback requests from other EU states": the report says "8 solicitări de feedback din partea statelor membre,
  conform Directivei 2011/16/UE" (8 feedback requests from member states, under Directive 2011/16/EU). OK.
- ANAF counts 3,204 "verificări" (checks) and 891 corrected "înregistrări" (records). The slide calls both "records"
  and divides one by the other; the 28% is a ratio of two counts from the same report, not a figure ANAF publishes.
- The range 2,000–10,000 lei joins two cases: up to 5,000 lei for a wrong report, 5,000–10,000 lei only when the due
  diligence procedures were not applied.
- The 2.4 million are "about" 2.4 million and include companies; the MVP covers individuals only (README
  [§7](../README.md#7-mvp-scope)).

## 3. The exposure: "The fine is per account, so the risk grows with volume."

On the slide:

- Fine range set by law, by number of accounts reported wrongly (thousand lei); minimum 2,000 lei and maximum 5,000 lei
  per account.
- 10 accounts: 20–50 · 100 accounts: 200–500 · 500 accounts: 1,000–2,500 · 1,000 accounts: 2,000–5,000.
- Footer: an illustration, the fine per account (art. 336(2)(k^1)) multiplied by the number of accounts; without due
  diligence procedures, 5,000–10,000 lei per account.

Sources: art. 336(2)(k^1) and (k^2), [`PITCH.md` §3](PITCH.md#3-notes-sources-for-the-figures-and-the-rules), note 3.

| Figure | Check |
| --- | --- |
| 20 / 50, 200 / 500, 1,000 / 2,500, 2,000 / 5,000 thousand lei | OK: 2,000 and 5,000 lei multiplied by 10, 100, 500 and 1,000 accounts |

Speaker notes (from the pptx):

> Because the fine is per account, the exposure grows with volume. This chart is an illustration, not a forecast: the
> fine set by law, 2,000 to 5,000 lei per account reported wrongly, multiplied by the number of accounts. Without due
> diligence procedures the fine doubles, to 5,000-10,000 lei per account.
>
> We do not claim how many accounts any bank reports wrongly. The point is the shape: a process that depends on memory
> and manual comparison carries a risk that grows with every customer.

Notes:

- "The fine doubles" is approximate: from 2,000–5,000 lei (k^1) to 5,000–10,000 lei (k^2), the maximum doubles and the
  minimum is 2.5 times higher. The ranges themselves are correct.
- This is arithmetic on the legal range, not a forecast: the account counts are examples, not Raiffeisen data. The
  fines actually given so far are much smaller (102,000 lei in 2024, ANAF performance report 2024, p. 45;
  [`PITCH.md` §3](PITCH.md#3-notes-sources-for-the-figures-and-the-rules), note 10).

## 4. The problem, in one customer: "One move. Two indicia. Ninety days."

On the slide:

- Maria Popescu moves to Munich and switches to a German phone.
- Address in Germany: `CRS-IND-ADDR`, guide p. 29 b.
- German phone, no Romanian phone: `CRS-IND-PHONE`, guide p. 29 c.
- Day 0: change received, case opened · Day 85: reminder and escalation · Day 90: legal deadline.
- No valid answer by day 90: she is treated as tax resident in Romania and in Germany.

Sources:

| Figure or claim | Source | Check |
| --- | --- | --- |
| `CRS-IND-ADDR`, p. 29 b | [`ghid/pagini/p-029.md`](ghid/pagini/p-029.md), letter b); `RuleCatalog` ([`RuleCatalog.cs`](../src/HackathonStarter/Features/Rules/RuleCatalog.cs)) | OK |
| `CRS-IND-PHONE`, p. 29 c | p. 29, letter c): "și niciun număr de telefon în România" (and no phone number in Romania) | OK |
| Day 90 | Guide p. 35–36, `DL-NEW-90`; Maria's account was opened on 2019-03-14, so it is a new account ([`PITCH.md` §3](PITCH.md#3-notes-sources-for-the-figures-and-the-rules), note 4) | OK |
| Day 85 | `DeadlineCalculator.ReminderDaysBefore = 5`; `CaseService.ReminderEscalationReason` ("day 85 on a new account, 5 days before the deadline: no answer, escalated automatically") | OK |
| Resident in RO and DE | Guide p. 36 ([`ghid/pagini/p-036.md`](ghid/pagini/p-036.md)): resident in the jurisdiction of the original self-certification and in the one where they may be resident after the change | OK |

Speaker notes (from the pptx):

> Make it concrete with one customer. Maria Popescu, a new account, moves to Munich and replaces her Romanian phone with
> a German one.
>
> Two facts in the bank's data now match the closed list of CRS indicia: a residence address in Germany (CRS-IND-ADDR,
> guide p. 29 letter b) and a German phone with no Romanian phone (CRS-IND-PHONE, p. 29 letter c).
>
> From the day the bank learns of the change, the clock runs. On day 85 TRACE reminds the operator and escalates to the
> tax department. With no valid answer by day 90, Maria is treated as resident in Romania and in Germany.

Notes:

- The speaker notes add no figure beyond the slide; "closed list" is README [§4](../README.md#4-the-rules) (p. 29).
- The 90 days come from the guide; the reminder and the escalation on day 85 are our design, not a requirement of the
  guide.
- In the app the case moves to `Expired` on day 91, once the deadline has passed (README
  [§3](../README.md#3-what-it-does-marias-case-in-screenshots)).
- Maria is a synthetic client; her case is `CRS-2026-0008` in the demo data.

## 5. Why it is hard: "Similar changes. Opposite answers."

On the slide:

| Change | Result | Reference on the slide |
| --- | --- | --- |
| A German phone, next to a Romanian one | No case | guide p. 29 c |
| A standing order to Germany, from a current account | No case | guide p. 29 d |
| The same order, to the United States | FATCA alert | guide p. 31 |
| A move from Cluj to Iasi | No case | domestic |

- "Miss one: a fine per account. Flag one wrongly: wasted work and a confused customer."

Sources: README [§3, the traps](../README.md#the-traps-changes-that-must-not-raise-an-alarm) and [§4](../README.md#4-the-rules);
[`ghid/pagini/p-029.md`](ghid/pagini/p-029.md) letters c) and d) ("instrucțiunile permanente, altele decât cele privind
un cont de depozit", standing instructions other than for a deposit account); [`ghid/pagini/p-031.md`](ghid/pagini/p-031.md)
(standing orders to an account in the United States, with no deposit account exception). Each row is a golden case in
[`GOLDEN-CASES.md`](GOLDEN-CASES.md) (Romanian), replayable from the Demo menu.

Speaker notes (from the pptx):

> The rules are precise, and they cut both ways. Miss an indicium and the account is reported wrongly: a fine per
> account. Raise a false alarm and an operator works a case that should not exist, and the customer gets a letter they
> should never have received.
>
> A German phone next to a Romanian one is not an indicium (p. 29 c). A standing order to Germany from a current account
> is exempt under CRS (p. 29 d). The same order to the US is a FATCA alert (p. 31). A move from Cluj to Iasi opens
> nothing.

Notes:

- The domestic move has no page in the guide. It is `GEN-DOMESTIC`, a design interpretation marked NEVERIFICAT
  (unverified) in code (`RuleCatalog.NotVerified`) and in README [§5](../README.md#5-the-design-interpretations-stated-openly):
  no rule from p. 29–31 applies, so no case.
- The same standing order from a custody account, not a current account, is a `Clarification` (README §3, the traps).

## 6. The principle: "Code decides. AI explains. A person approves."

On the slide:

- Code: applies the rules, cites the page and starts the deadline.
- AI: reads the documents, quotes the evidence, drafts the letter.
- Person: approves every letter and every closing.
- **29 rules**: 24 from the law and the ANAF guide, 3 our documented reading, 2 bank policy.

Sources:

| Figure | Source | Check |
| --- | --- | --- |
| 29 rules | [`RuleCatalog.cs`](../src/HackathonStarter/Features/Rules/RuleCatalog.cs); README [§4](../README.md#4-the-rules) | OK |
| 24 / 3 / 2 | `RuleBasis` on each rule in `RuleCatalog.cs`: 24 `Verified`, 3 `Interpretation`, 2 `Policy` (counted in the file) | OK |

Speaker notes (from the pptx):

> The whole design in one sentence: code decides, AI explains, a person approves.
>
> Code applies 29 rules, each with an ID and a source: 24 come straight from the text of the law and the ANAF guide, 3
> are our documented reading where the guide is silent, and 2 are bank policy. Every decision cites its page.
>
> The AI agent does only what code cannot: it reads documents, quotes evidence and drafts the letter. A person approves
> every letter and every closing. Every step goes into a tamper-evident audit log.

Notes:

- "Where the guide is silent" fits only one of the 3 rules on our reading. `GEN-DOMESTIC` has no page in the guide. For
  `GEN-NEW-ACCOUNT` and `DL-PRE-YEAR` the guide does say something, and we interpret it: the indicia are the procedure
  for preexisting accounts, which we also use on new accounts; and the guide says "or" where we apply "the later of"
  (README [§5](../README.md#5-the-design-interpretations-stated-openly)).
- "Every decision cites its page" has one exception: `GEN-DOMESTIC` has no page (NEVERIFICAT).
- "Tamper-evident audit log": `AuditLog`, SHA-256 chain; "Verify" finds the modified entry (README
  [§6, trust](../README.md#trust)). OK.
- The 3 rules on our reading are explained in README [§5](../README.md#5-the-design-interpretations-stated-openly) and
  carry an "Our reading" badge in the app. One of them, `GEN-DOMESTIC`, has no guide page (NEVERIFICAT).
- The split of work is README [§2](../README.md#2-who-does-what). A context signal never changes a decision and never
  opens a case.

## 7. The agent: "One agent. Two human checkpoints."

On the slide:

- When a case opens: reads the documents, quotes the evidence, drafts the letter, **the person approves the letter**.
- When the customer answers: reads the new declaration, checks the TIN format, code re-runs the rules, **the person
  approves the closing**.
- 14 tools · Claude on Amazon Bedrock · The model never decides an indicium, a deadline or an alert.

Sources:

| Figure or claim | Source | Check |
| --- | --- | --- |
| 14 tools | `SentinelTools.All()` in [`Features/Sentinel/SentinelTools.cs`](../src/HackathonStarter/Features/Sentinel/SentinelTools.cs): `getCase`, `getClientSnapshot`, `evaluateRules`, `listDocuments`, `readDocument`, `recordEvidence`, `recordSignal`, `extractSelfCertification`, `checkTin`, `draftClientRequest`, `sendClientRequest`, `registerResponse`, `closeCase`, `escalate` | OK |
| Two human checkpoints | `sendClientRequest` and `closeCase` are the only tools with `needsApproval: true`; after approval they run without the model (README [§6](../README.md#6-architecture)) | OK |
| Code re-runs the rules | `closeCase` re-evaluates the rules in code before closing (tool description in `SentinelTools.cs`) | OK |
| Claude on Amazon Bedrock | README [§6, the models and the fallback](../README.md#the-models-and-the-fallback): region `us-west-2`, synthetic data only | OK, see note |

Speaker notes (from the pptx):

> One tool-using agent, CRS Sentinel, with 14 tools. The rules are a closed list, so more agents would add nothing.
>
> When a case opens, the agent reads the customer's documents through the shield, records each quote as evidence (code
> checks it), records context signals and drafts the letter; sending waits for a person.
>
> When the customer answers, it extracts the new self-certification, checks the TIN format (format valid, never TIN
> valid), registers the answer and code re-runs the rules; closing waits for a person.
>
> Live on Claude via Amazon Bedrock, us-west-2; with no model, a mock runs the same real tools.

Notes:

- The speaker notes use "CRS Sentinel", the working name. The product is TRACE; the code keeps the working name,
  `CrsSentinelAgent` (`crs-sentinel`).
- The steps in the speaker notes match the tools in `SentinelTools.cs` (`readDocument` after the shield,
  `recordEvidence`, `recordSignal`, `draftClientRequest`, `sendClientRequest`; `extractSelfCertification`, `checkTin`,
  `registerResponse`, `closeCase`). "More agents would add nothing" is our design argument
  ([`PITCH.md` §5](PITCH.md#5-what-sets-us-apart-for-the-jury-and-the-questions), point 6), not a measurement.
- Bedrock is optional. Without a key, the app runs on the mock, which calls the same real tools with preset answers; the
  top bar shows which mode is active. The demo region is `us-west-2` because the workshop account blocks Bedrock in
  `eu-central-1`; in production the model would run in an EU region or at the bank.
- "Checks the TIN format" means a format check ("format valid"), never a check that the TIN exists.

## 8. Trust: "AI output counts only after code checks it."

On the slide:

- Test bench, checks passed out of cases tested: true quotes accepted 10 / 10; invented quotes rejected 38 / 38;
  hidden-text quotes rejected 4 / 4; hidden texts removed by the shield 5 / 5; clean PDFs left untouched 8 / 8.
- **65 / 65** checks passed on the test bench.
- **5** kinds of personal data masked before the model.
- **SHA-256** chain over every step of every case.
- Footer: test bench in the repository (EvidenceVerifier and HiddenTextShield tests), 12 generated PDFs, synthetic data.

Sources:

| Figure | Source | Check |
| --- | --- | --- |
| 10, 38, 4 (quotes) | README [§8, the latest run](../README.md#the-latest-run), "Where there is uncertainty"; `/metrics` | OK |
| 5 of 5 hidden texts, 8 of 8 clean | Same table: 12 generated PDFs, 4 with hidden text (5 texts: white 1, microscopic 2, off-page 1, invisible 1), 8 clean, 0 false alarms | OK |
| 65 / 65 | 10 + 38 + 4 + 5 + 8 = 65 | OK (computed) |
| 5 kinds | `PiiVault`: CNP, IBAN, card, e-mail, phone (README [§6, trust](../README.md#trust) and [§9](../README.md#9-privacy)) | OK |
| SHA-256 chain | `AuditLog`: `Hash = SHA-256(previous hash + content)` (README §6, trust) | OK |

Speaker notes (from the pptx):

> Every output of the model is checked by code before it counts. On our test bench:
>
> 10 of 10 true quotes accepted; 38 of 38 invented quotes rejected; 4 of 4 quotes that exist only in hidden text
> rejected.
>
> The shield, on 12 generated PDFs: all 5 hidden texts found and removed before the model, and none of the 8 clean PDFs
> flagged.
>
> On top of that: national ID, IBAN, card, email and phone numbers are masked before the model, and the audit log is
> chained with SHA-256.

Notes:

- The speaker notes repeat the slide figures; "national ID" is the CNP in `PiiVault`.
- The bench figures are produced by `EvaluationService` on the `/metrics` run, which passes the generated PDFs through
  the registered `HiddenTextShield` and `EvidenceVerifier`; `EvaluationServiceTests` checks the bench logic. The quotes
  are fixed quotes, not the model's. Field-by-field extraction by the model is not measured (README §8).
- Names, addresses, dates of birth and foreign TINs are not masked before the model (README §9); the data is synthetic.

## 9. Results: "23 caught. 0 missed. 0 false alarms."

On the slide:

- Expected actions against caught, last evaluation run: 21 golden cases, 13 expected and 13 caught, 0 false alarms;
  50 synthetic customers, 10 expected and 10 caught, 0 false alarms.
- **0** missed indicia, on both sets.
- **0.03 ms** per rule evaluation, the code only.
- **1,600+** automated tests passing on every build.
- Footer: correct answers written before the code; we do not claim 100% accuracy; time saved per case is not measured
  yet.

Sources:

| Figure | Source | Check |
| --- | --- | --- |
| 13 / 13, 10 / 10, 0 missed, 0 false alarms | README [§8, the latest run](../README.md#the-latest-run): commit `9e67e06`, 29.10.2026, no model, `/metrics` | OK |
| 23 caught | 13 + 10 | OK (computed) |
| 21 golden cases | README [§8](../README.md#8-the-golden-cases-and-the-metrics): 18 scenarios, 21 cases with variants; [`GOLDEN-CASES.md`](GOLDEN-CASES.md) | OK, see note |
| 1,600+ tests | README [§11](../README.md#11-status): unit tests 1602 of 1604 passed (2 skipped: the live tests on Bedrock), E2E 52 of 52, at `5828400` | OK |
| 0.03 ms per rule evaluation | `EvaluationService` on the `/metrics` run; shown on the page until PR #91 | Measured, not recorded in the repo, see note |

Speaker notes (from the pptx):

> We wrote the correct outcome before we wrote the code: 21 golden cases and 50 synthetic customers, each with the
> expected decision, rules and guide page.
>
> Last run, on the metrics page of the app: 23 expected actions, 23 caught (13 on the golden cases, 10 on the synthetic
> customers), 0 missed, 0 false alarms. A rule evaluation takes about 0.03 milliseconds, and more than 1,600 automated
> tests pass on every build.
>
> What we do not claim: 100% accuracy. The engine is deterministic, and one good run does not prove reliability in
> general. Time saved per case is not measured yet; that is what the pilot is for.

Notes:

- "Each with the expected decision, rules and guide page": the domestic-move case has no guide page (`GEN-DOMESTIC`,
  NEVERIFICAT).
- Of the 21 golden cases, 20 are evaluated by the rule engine; case 10 (hidden text) is checked at the shield (README §8).
- The 0 false alarms is the measured result on these two sets, not a general claim. The engine is deterministic, so the
  result means "the rules from the guide are implemented and tested", not "100% accuracy".
- **0.03 ms:** measured by `EvaluationService` on the `/metrics` run (`Stopwatch`): the time of the specification part
  (`Spec.Duration`) divided by the number of evaluated cases. The page showed it as "{0} ms per evaluation (the code
  only)" until PR #91 (commit `d7ffb19`, 29 Oct 2026), which removed the line; only its Romanian string is left, in
  `Lang.Ro.Evaluation.cs`. It is wall-clock time and depends on the machine (an older screenshot of `/metrics` showed
  0.05 ms); no run in the repo records the 0.03 ms value, and no test asserts a timing. It is the time of the code
  alone, not the time of a case with the agent.
- "On every build": every PR goes in only with green CI (build with 0 warnings, unit and E2E tests; README §11).
- Operational efficiency (time per case) is not measured: there is no manual baseline (README §8).

## 10. The ask: "A shadow pilot, then a decision."

On the slide:

1. **Connect:** a read-only copy of the data-change feed; nothing changes in production.
2. **Shadow:** TRACE decides next to the operators on real changes; we compare decisions and time per case.
3. **Decide:** go or no-go with the tax department, on measured results.

- What we need from the bank: a sample of real data changes; a review of our documented interpretations by the tax
  department; a model endpoint in an EU region or inside the bank.
- TRACE: Code decides. AI explains. A person approves.
- hackathon-starter-k0ym.onrender.com · github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon · Demo on synthetic data;
  core banking simulated.

Sources: the next step and the barriers in
[`PITCH.md` §5b](PITCH.md#5b-business-mentoring-the-user-the-benefit-the-barriers); the limits in README
[§7](../README.md#7-mvp-scope) (no integration with core banking, ONRC or ANAF; company accounts not covered); the
interpretations in README [§5](../README.md#5-the-design-interpretations-stated-openly); the region in README
[§6](../README.md#the-models-and-the-fallback).

Speaker notes (from the pptx):

> The ask is concrete: a shadow pilot. First, a read-only copy of the data-change feed. Then TRACE decides next to the
> operators on a sample of real changes, and we compare decisions and time per case. Then a go or no-go with the tax
> department.
>
> What we need from the bank: the sample of real changes, the tax department's review of our documented
> interpretations, and a model endpoint in an EU region or inside the bank.
>
> In the demo the customers and documents are synthetic and core banking is simulated; the .NET template existed before
> the event, everything CRS was built during the hackathon.

Notes:

- The pilot is a proposal, not a result. Time saved per case would be measured for the first time in the shadow step.
- Links: the app <https://hackathon-starter-k0ym.onrender.com/> (open, no access code; the first start takes up to a
  minute) and the code <https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon>.
- The .NET template the project started from (agent runtime, masking, deployment) was written before the hackathon;
  everything CRS-specific was written from 28 October 2026 (README [§13](../README.md#13-the-template)).
