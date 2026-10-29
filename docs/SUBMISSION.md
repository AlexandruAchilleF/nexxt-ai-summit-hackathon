# Submission: TRACE (Raiffeisen, AI for Operational Efficiency)

The organizers' deadline was **Thursday, 29 Oct 2026, 11:00**. What the organizers asked for, from the submission
checklist, and where it is:

| What | Required | Where it is | Status |
| --- | --- | --- | --- |
| Team name and team lead's e-mail | yes | the submission form (not repeated in this public repo) | sent |
| Project title and description | yes | the form text below | sent |
| GitHub link | yes | <https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon> (public) | sent |
| The presentation | yes | [`docs/presentation/TRACE.pdf`](presentation/TRACE.pdf) (10 slides) | sent |
| The 3-minute video | yes | [Google Drive](https://drive.google.com/drive/folders/1evObAYOK5qisjEUnb0LRrMngxGA4u6jO) (`video-pitch.mp4`); the narration: [`PITCH.md`](PITCH.md) | sent |
| Setup instructions, dependencies, sample inputs | yes ("where needed") | README §10 and "How the jury can check it in 5 minutes" | in the repo |

The plan: [`PLAN.md`](PLAN.md) (Romanian) and [`PLAYBOOK.md`](PLAYBOOK.md) (Romanian). The golden cases:
[`GOLDEN-CASES.md`](GOLDEN-CASES.md) (Romanian). The facts and the guide pages: [`RESEARCH-CRS.md`](RESEARCH-CRS.md)
(Romanian).

## The submission form (the text)

**Project title:** TRACE (Tax Residency Anomaly Correlation Engine)

**Short description:** TRACE turns every change to a customer's data (address, phone, standing order, power of
attorney) into an explained CRS/FATCA decision before the legal deadline runs out. The rules of the ANAF guide are code,
each with its ID and page; the AI agent reads the documents, quotes the evidence and drafts the letter to the customer; the
operator approves sending it and closing the case.

**Long description (for the form):**

> When a customer's address or phone changes, the bank must check again where the customer is tax resident, or it reports
> every account wrongly to ANAF. For a new account it has 90 days to obtain a new self-certification (ANAF guide, p. 35-36).
> Today a person compares fields against the guide, writes to the customer and remembers the deadline.
>
> TRACE automates the repetitive part with one tool-using agent orchestrated by code, with a person deciding.
> Deterministic, tested C# applies the closed list of CRS and FATCA indicia (each rule with its guide page), computes the
> deadline, checks every quote the model returns against the document, checks the TIN format and keeps a SHA-256
> hash-chained audit log. The model (Claude on Amazon Bedrock) only reads documents, records quoted evidence and context
> signals, and drafts the customer letter; it never decides an indicium, a deadline or an alert. A shield removes hidden
> text from PDFs before the model sees it, and personal identifiers are masked before any model call.
>
> The demo follows one case end to end (a customer moving to Munich: case opened, evidence quoted, request approved,
> reminder at day 85 on a simulated clock, new self-certification checked, case closed with proof), plus traps that must
> not raise false alarms. The expected outcome of 21 golden cases and 50 synthetic customers was written before the code;
> the last run caught all 23 expected actions with 0 misses and 0 false alarms. All data is synthetic; core banking,
> message delivery and ANAF are simulated; company accounts are not covered yet. Runs with no configuration (scripted mock
> that calls the real tools); live on Bedrock with AWS credentials.

## How the jury can check it in 5 minutes

For the "Functional completeness and testability" criterion (20%): the jury must be able to check it on their own.

1. The app: <https://hackathon-starter-k0ym.onrender.com/> (open, no access code; the first start takes up to a
   minute). Or locally: `tools/setup-dev.sh`, then `dotnet run --project src/HackathonStarter` (no keys, on the mock).
   The demo shortcuts are in the **Demo menu**: click the date in the top bar.
2. **Maria end to end:** [`/changes/new?scenario=4`](https://hackathon-starter-k0ym.onrender.com/changes/new?scenario=4)
   (golden case 4, Maria Popescu, filled in) → "Record and apply the rules" → "Open case CRS-2026-…" → "Analyze the
   file" → "Approve" → Demo menu → "To the reminder day" → Demo menu → "The client answers" → "Check the answer" →
   "Approve".
3. **"What if…?"** on `/changes/new`: any change, with the verdict on the spot and the rule with its page. The traps:
   Demo menu → "Golden cases" → cases 13, 5a, 5c, 15 (or `/changes/new?scenario=5a`).
4. **The shield:** `/clients/CL-0010` → "Record the upload and scan" ("Show the removed text" once someone has scanned
   it).
5. **The audit log:** `/audit` → "Verify" → Demo menu → "Tamper mode" → the "Tamper" icon on an entry → "Verify".
6. **The figures:** `/metrics` → "Run again"; locally, `dotnet test tests/HackathonStarter.Tests` runs the same cases.
7. Settings (or the Demo menu) → "Reset demo data" returns everything to the startup state.

The public app is one shared instance: what one visitor changes (Maria's case, the simulated date, a tampered entry)
everyone sees. If a golden case shows "Differs from golden case", someone has already changed that client (for example
5b or 5c before 5a, both on CL-0005): "Reset demo data" replays it from the start, for everyone.

The sample inputs are in the app: 50 synthetic clients, the golden-case PDFs and the "Demo" menu.

## The jury criteria: where the evidence is

| Criterion | Weight | What we show | Where |
| --- | --- | --- | --- |
| Originality | 15% | The core is after onboarding (changes of circumstances); the guide's rules as code with a page; the hidden-text shield; the audit log that shows where it was tampered with | Video S2, S6, S7; slides 4–6 and 8 |
| Problem and domain research | 15% | The ANAF guide of 29.12.2025, page by page; the verified rules; the traps (depository account, RO phone, move within the country); the design interpretations stated openly; the preexisting-account deadline with the law and the Directive | README §4–5, `docs/reguli-verificate.md` (Romanian), video S5; slides 2–5 |
| Agent design and orchestration | 25% | 14 tools, two approval stops, quotes checked by code, signals with no effect, fallback live → recording → mock; unexpected situations: hidden text, invented quote, model unavailable | Video S3, S4, S6, S7; README §6; slides 7–8 |
| Functional completeness and testability | 20% | One end-to-end scenario from the UI; 1602 unit tests (2 live tests skipped) and 52 E2E tests green; `/metrics` with the result written beforehand | "How the jury can check it"; video S8; slide 9 |
| Architecture and reproducibility | 15% | Pure modules (`Rules`, `Deadlines`, `Shield`, `Tin`) separate from the services and the agent; starts with no configuration; Docker + Render | README §6 and §10; `/architecture` |
| Business mentoring | 10% | The user, the benefit, the adoption barriers, the next step | `PITCH.md` §5b; slide 10 |

## Video: 3 minutes, 8 segments

The narration, word for word, what is shown in each segment, what we disclose openly and the filming checklist:
[`PITCH.md`](PITCH.md). It is the only narration text; this file keeps only the timings and how the video was made.
Timings follow the organizers' four blocks (word counts in `PITCH.md` §1).

| Time | Block | Segments | What is shown |
| --- | --- | --- | --- |
| 0:00–0:30 | **The user, the problem, the solution** | S1 | The title, then `/dashboard`: the deadlines that are expiring |
| 0:30–2:00 | **The demo** | S2–S6 | Maria end to end on `/changes/new` and `/cases/{code}` (the agent panel, the approvals, "Demo" → day 85 → "The client answers"); the traps on `/changes/new`; the shield on `/clients/CL-0010` |
| 2:00–2:30 | **Value and differentiation** | S7 | `/architecture`, then `/audit` with "Tamper" |
| 2:30–3:00 | **Implementation, limits, next step** | S8 | `/metrics`, then logo + URL |

How the video was made:
- **Live (Bedrock) or on the mock, never in `AgentMode = Replay` on the steps that change data.** Replay does not run the
  tools: the run shows the steps, but no evidence is saved and the case does not change state. The rule was to test the
  real model beforehand (Settings → Connections → "Test connections", on the filming computer) and, if it was not
  available, to film on the mock, which calls the real tools, with the "Mock" label visible and said in the narration;
  for a mock take, `Recordings/crs-sentinel.json` is moved aside (`PITCH.md` §4).
- **What is shortened and what is edited is said** (an organizers' requirement): the simulated date +85 days, the wait
  for the model cut out, the synthetic data and the simulated core banking (`PITCH.md` §1, what we disclose).
- The technical decisions are explained during the demo, not only at the end: every segment says what is code and what
  is AI.
- The UI in English (RO/EN button; Romanian is the default). Browser zoom 125%, no bookmarks bar, notifications off. No
  password or key in frame.
- The narration recorded separately from the screen; 1080p export; duration ≤ 3:00. `tools/DemoFilm` drives the browser
  along the same route (S1–S9: PITCH's S8 split into the metrics and the closing line) at the pace of the narration.

## The state after submission (checked on 29 Oct 2026)

- CI green on `5828400` (the last commit on `main` before the deadline): build with 0 warnings, unit tests 1602 of
  1604 passed (the 2 live Bedrock tests skipped), E2E 52 of 52. A local run on `5828400` gives the same figures.
- The public links open without logging in: the GitHub repo, the video folder on Google Drive and the app.
- The app on Render runs live on Bedrock in `us-west-2` (the top bar shows "Agent: live (Bedrock us-west-2)"), with
  synthetic data only.
- No AWS, Anthropic or GitHub key and no private key in the full git history (scanned on `5828400`).
- The changes made after the deadline (the documentation, the screenshots and small UI texts) go through pull
  requests with the same checks; the submitted state stays `5828400`.

## What we never say

| We do not say | We say |
| --- | --- |
| "Banks have been fined hundreds of thousands of euros." | "The risk is per account." The 102,000 lei figure (ANAF, 2024) only when asked, with its context (`PITCH.md` §6). |
| "Raiffeisen has problems with CRS." | "The ANAF figures cover the whole sector." |
| "Nobody has done this before." | "A known method, applied to the flow after onboarding, with the rules from the ANAF guide and a human making the decision." |
| "Income from abroad means residence there." | "It is not an official indicium; it is a context signal, with no effect." |
| "TIN valid." | "Format valid." |
| "Every update starts a deadline." | "Every update must be evaluated; only the tax-relevant ones start the deadline." |
| "The deadline is always 90 days." | "90 days for new accounts; for a preexisting account documented through the residence address, the later of 31 December and 90 days from learning of the change (Fiscal Procedure Code, Annex no. 2 point 1; the ANAF guide, p. 29, says 'or', Romanian 'sau')." For indicia from the electronic search, "the later of" is our interpretation (`PITCH.md` §6). |
| The 30% FATCA withholding in the pitch | Only when asked: "Romania has a Model 1 FATCA agreement: a compliant bank does not bear the 30% withholding. Withholding applies only if significant non-compliance is not resolved within 18 months of notification. Until then, the fines in Romanian law apply." (the agreement, art. 4(1) and 5(2)(b)) |

## Likely jury questions (with the answer)

GDPR, FATCA, the fines, the ANAF figures and the F3000 deadline, with their sources: [`PITCH.md`](PITCH.md) §6. The rows
the two files share use the same wording.

| Question | Short answer |
| --- | --- |
| What does the agent do on its own and where does the human decide? | It chooses which evidence to look for, quotes it, drafts the request and evaluates the answer. Code decides whether an indicium exists and computes the deadline; the operator (Ana) approves the request and the closing. |
| What happens if the model gets it wrong or invents a quote? | The quote is checked in code against the document and rejected if it does not exist. The verdict comes from the rules, not from the model. |
| Why not ChatGPT? | A chatbot gives an opinion and can answer differently on the same file. Here the rules are code (every indicium with its guide page), and the deadline is tracked on every case. |
| Doesn't Raiffeisen already have this? | The signal on a data change may exist. We cover what comes after it: evidence, request, answer, deadline, closing with proof. |
| Why is the standing order to Germany not an indicium, while the one to the US is? | Under CRS, standing orders from depository accounts are excluded (guide, p. 29); under FATCA the exception does not exist (p. 31). |
| Why is Maria's deadline 90 days? | Her account is a new account (opened in 2019): 90 days from the date the bank knows or has reason to know of the change (guide, p. 35–36). For a preexisting account we apply the later of 31 December and +90 days: law for an account documented through the residence address (Fiscal Procedure Code, Annex no. 2 point 1); our interpretation for the indicia from the electronic search (Annex no. 1, section III, subsection B, point 16 says only "or", Romanian "ori"), aligned with the OECD Commentary (Section III, paragraphs 13 and 16); the guide (p. 29) says "or" (Romanian "sau"). |
| Why do you use the indicia for new accounts? | For new accounts, the guide requires the reasonableness test of the self-certification (p. 33); we use the indicia as the criteria by which the self-certification is contradicted. This is our design interpretation (README §5). |
| How do we check that it works? | `/metrics` runs all the golden cases with the result written beforehand; `/changes/new` answers "what if…?" on the spot; `dotnet test` runs the same cases. |
| Does it work with real, sensitive data? | Yes, with conditions: CNP, IBAN, card, e-mail and phone numbers (Romanian ones, and foreign ones with the country code) masked before the model (names, addresses and foreign phone numbers without a country code are not masked today), a model that can run at the bank, a legal basis (the reporting obligation in the Fiscal Procedure Code, art. 62(1); GDPR art. 6(1)(c); the details in `PITCH.md` §6), configurable retention. |
| What is real and what is simulated? | Real: the rules, the deadlines, the shield, TIN format checking (locally and through TIN on Europa, live or `TargetClient` replay; TIN on Europa checks only structure and syntax), the loop with approvals. Synthetic: the clients, the documents, the client's answers. Not built: integration with core banking, message delivery. |
| Who pays and how much? | The bank, per case or by licence; savings = cases × minutes saved × cost per minute, with the mentor's figures; the avoided risk is per account. |
