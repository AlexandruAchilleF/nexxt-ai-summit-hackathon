# Submission: CRS Sentinel (Raiffeisen, AI for Operational Efficiency)

The organizers' deadline: **Thursday, 29 Oct 2026, 11:00**. We submit by **10:30** (uploading and processing the video
takes time). What the organizers ask for, from the submission checklist:

| What | Required | Where it is | Status |
| --- | --- | --- | --- |
| Team name and team lead's e-mail | yes | the form below | **to fill in** |
| Project title and description | yes | the form below | ready (EN) |
| GitHub link | yes | <https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon> (public) | ready |
| The presentation | yes | the slides in [`PRESENTATION.md`](PRESENTATION.md), exported to PDF and uploaded with a public link | **to export** |
| The 3-minute video | yes | the narration in [`PITCH.md`](PITCH.md) | **to film** |
| Setup instructions, dependencies, sample inputs | yes ("where needed") | README §10 and "How the jury can check it in 5 minutes" | ready |

The plan: [`PLAN.md`](PLAN.md) (Romanian) and [`PLAYBOOK.md`](PLAYBOOK.md) (Romanian). The golden cases:
[`GOLDEN-CASES.md`](GOLDEN-CASES.md) (Romanian). The facts and the guide pages: [`RESEARCH-CRS.md`](RESEARCH-CRS.md)
(Romanian).

## The submission form (text to copy)

**Team name:** `[to fill in]` · **Team lead, e-mail:** `[to fill in]`

**Project title:** CRS Sentinel

**Short description:** CRS Sentinel turns every change to a customer's data (address, phone, standing order, power of
attorney) into an explained CRS/FATCA decision before the legal deadline runs out. The rules of the ANAF guide are code,
each with its ID and page; the AI agent reads the documents, quotes the evidence and drafts the letter to the customer; the
operator approves sending it and closing the case.

**Long description (for the form):**

> When a customer's address or phone changes, the bank must check again where the customer is tax resident, or it reports
> every account wrongly to ANAF. For a new account it has 90 days to obtain a new self-certification (ANAF guide, p. 35-36).
> Today a person compares fields against the guide, writes to the customer and remembers the deadline.
>
> CRS Sentinel automates the repetitive part with one tool-using agent orchestrated by code, with a person deciding.
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

1. The app: <https://hackathon-starter-k0ym.onrender.com/> (access code in the README; the first start takes up to a
   minute). Or locally: `tools/setup-dev.sh`, then `dotnet run --project src/HackathonStarter` (no keys, on the mock).
2. **Maria end to end:** `/changes/new` → "Start from a golden case" → case 4 (Maria Popescu) → "Record and apply the
   rules" → the case code → "Analyze the file" → "Approve" → "Demo" → "To the reminder day" → "The client answers" →
   "Check the answer" → "Approve".
3. **"What if…?"** on `/changes/new`: any change, with the verdict on the spot and the rule with its page (the traps:
   cases 13, 5a, 5c, 15).
4. **The shield:** `/clients/CL-0010` → "Record the upload and scan".
5. **The audit log:** `/audit` → "Verify", "Tamper", "Verify".
6. **The figures:** `/metrics` → "Run again"; locally, `dotnet test tests/HackathonStarter.Tests` runs the same cases.
7. Settings → "Reset demo data" returns everything to the startup state.

The sample inputs are in the app: 50 synthetic clients, the golden-case PDFs and the "Demo" menu.

## The jury criteria: where the evidence is

| Criterion | Weight | What we show | Where |
| --- | --- | --- | --- |
| Originality | 15% | The core is after onboarding (changes of circumstances); the guide's rules as code with a page; the hidden-text shield; the audit log that shows where it was tampered with | Video S2, S6, S7; slide 4 |
| Problem and domain research | 15% | The ANAF guide of 29.12.2025, page by page; the verified rules; the traps (depository account, RO phone, move within the country); the design interpretations stated openly; the preexisting-account deadline with the law and the Directive | README §4–5, `docs/reguli-verificate.md` (Romanian), video S5; slide 3 |
| Agent design and orchestration | 25% | 14 tools, two approval stops, quotes checked by code, signals with no effect, fallback live → recording → mock; unexpected situations: hidden text, invented quote, model unavailable | Video S3, S4, S6, S7; README §6; slide 5 |
| Functional completeness and testability | 20% | One end-to-end scenario from the UI; 1414 unit tests (1 live test skipped) and 39 E2E tests green; `/metrics` with the result written beforehand | "How the jury can check it"; video S8; slide 6 |
| Architecture and reproducibility | 15% | Pure modules (`Rules`, `Deadlines`, `Shield`, `Tin`) separate from the services and the agent; starts with no configuration; Docker + Render | README §6 and §10; `/architecture`; slide 7 |
| Business mentoring | 10% | The user, the benefit, the adoption barriers, the next step | `PITCH.md` §5b; slide 8 |

## Video: 3 minutes, 8 segments

The narration, word for word, what is shown in each segment, what we disclose openly and the filming checklist:
[`PITCH.md`](PITCH.md). It is the only narration text; this file keeps only the timings and the tips. Timings follow the
organizers' four blocks (word counts in `PITCH.md` §1).

| Time | Block | Segments | What is shown |
| --- | --- | --- | --- |
| 0:00–0:30 | **The user, the problem, the solution** | S1 | The title, then `/dashboard`: the deadlines that are expiring |
| 0:30–2:00 | **The demo** | S2–S6 | Maria end to end on `/changes/new` and `/cases/{code}` (the agent panel, the approvals, "Demo" → day 85 → "The client answers"); the traps on `/changes/new`; the shield on `/clients/CL-0010` |
| 2:00–2:30 | **Value and differentiation** | S7 | `/architecture`, then `/audit` with "Tamper" |
| 2:30–3:00 | **Implementation, limits, next step** | S8 | `/metrics`, then logo + URL |

Tips:
- **We film live (Bedrock) or on the mock, never in `AgentMode = Replay` on the steps that change data.** Replay does not
  run the tools: the run shows the steps, but no evidence is saved and the case does not change state. The real model is
  tested beforehand (Settings → Connections → "Test connections", on the filming computer); if it is not available, we
  film on the mock, which calls the real tools (the "Mock" label stays visible and we say so). For the mock take we move
  `Recordings/crs-sentinel.json` aside (`PITCH.md` §4).
- **We say what is shortened and what is edited** (an organizers' requirement): the simulated date +85 days, the wait for
  the model cut out, the synthetic data and the simulated core banking (`PITCH.md` §1, what we say openly).
- We explain the technical decisions during the demo, not only at the end: every segment says what is code and what is AI.
- The UI switched to English (RO/EN button; Romanian is the default). Browser zoom 125%, no bookmarks bar, notifications
  off. No password, key or access code in frame.
- The narration recorded separately from the screen. 1080p export. Duration: **≤ 3:00**, checked after upload.
  `tools/DemoFilm` (work in progress) can drive the browser along the S1–S8 path at the pace of the narration.

## Schedule (working back from the deadline)

| When | What |
| --- | --- |
| Thursday 08:00 | **Code freeze**: only fixes on the demo path. The backup demo is already recorded. |
| 08:00–09:30 | Filming, several takes; editing; narration; on-screen captions. |
| 09:30–10:00 | Slides exported to PDF; links checked from a private window (not logged in). |
| 10:00–10:30 | `dotnet build` (0 warnings) + `dotnet test`, deploy checked, video link in the README, video uploaded and watched. |
| **10:30** | **Submission.** Not in the last minute. |

## Final checks (the organizers')

- [ ] Video uploaded, watched end to end, duration checked (≤ 3:00)
- [ ] The jury can open every link: GitHub (public), the presentation and the video, tested from a private window
- [ ] Setup instructions, dependencies and sample inputs (README §10, "How the jury can check it in 5 minutes")
- [ ] Time left for uploading and processing the video

## Demo checklist

- [ ] The script (Maria + 3 traps + shield + tamper + metrics) written word for word in [`PITCH.md`](PITCH.md) and
      rehearsed with a timer
- [ ] The mock runs the whole path, with the real tools; the recordings (`src/HackathonStarter/Recordings/*.json`) only
      for questions that do not change data
- [ ] The simulated date and the demo data reset before filming (Settings → "Reset demo data"; the full checklist in
      `PITCH.md` §4)
- [ ] UI switched to English (RO/EN button) for filming
- [ ] `/metrics` fully green on the golden cases, on the same version that is on Render
- [ ] No real data: synthetic clients, documents and TINs

## Code checklist

- [x] `dotnet build -warnaserror` with no warnings, unit tests 1414 of 1415 (1 live test skipped, it needs Bedrock), E2E
      39 of 39, on `ed0d4eb` (29 Oct 2026, 01:30)
- [ ] The app starts **without** `appsettings.Local.json` and without AWS, on the final version
- [ ] README: what it does, how to start it, the architecture, what is AI and what is code, the MVP scope, the design
      interpretations, the video link
- [x] **No secret in the repo (it is public):** `git log -p` on `main` finds no real AWS (`AKIA...`) or Anthropic keys
      (only the `sk-ant-...` example in the documentation), checked on `ed0d4eb`
- [x] The Render URL in the README

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
| What does the agent do on its own and where does the human decide? | It chooses which evidence to look for, quotes it, drafts the request and evaluates the answer. Code decides whether an indicium exists and computes the deadline; the human approves the request and the closing. |
| What happens if the model gets it wrong or invents a quote? | The quote is checked in code against the document and rejected if it does not exist. The verdict comes from the rules, not from the model. |
| Why not ChatGPT? | A chatbot gives an opinion and can answer differently on the same file. Here the rules are code (every indicium with its guide page), and the deadline is tracked on every case. |
| Doesn't Raiffeisen already have this? | The signal on a data change may exist. We cover what comes after it: evidence, request, answer, deadline, closing with proof. `[mentor's answer]` |
| Why is the standing order to Germany not an indicium, while the one to the US is? | Under CRS, standing orders from depository accounts are excluded (guide, p. 29); under FATCA the exception does not exist (p. 31). |
| Why is Maria's deadline 90 days? | Her account is a new account (opened in 2019): 90 days from the date the bank knows or has reason to know of the change (guide, p. 35–36). For a preexisting account we apply the later of 31 December and +90 days: law for an account documented through the residence address (Fiscal Procedure Code, Annex no. 2 point 1); our interpretation for the indicia from the electronic search (Annex no. 1, section III, subsection B, point 16 says only "or", Romanian "ori"), aligned with the OECD Commentary (Section III, paragraphs 13 and 16); the guide (p. 29) says "or" (Romanian "sau"). |
| Why do you use the indicia for new accounts? | For new accounts, the guide requires the reasonableness test of the self-certification (p. 33); we use the indicia as the criteria by which the self-certification is contradicted. This is our design interpretation (README §5). |
| How do we check that it works? | `/metrics` runs all the golden cases with the result written beforehand; `/changes/new` answers "what if…?" on the spot; `dotnet test` runs the same cases. |
| Does it work with real, sensitive data? | Yes, with conditions: CNP, IBAN, card, e-mail and phone numbers (Romanian ones, and foreign ones with the country code) masked before the model (names, addresses and foreign phone numbers without a country code are not masked today), a model that can run at the bank, a legal basis (the reporting obligation in the Fiscal Procedure Code, art. 62(1); GDPR art. 6(1)(c); the details in `PITCH.md` §6), configurable retention. |
| What is real and what is simulated? | Real: the rules, the deadlines, the shield, TIN format checking (locally and through TIN on Europa, live or `TargetClient` replay; TIN on Europa checks only structure and syntax), the loop with approvals. Synthetic: the clients, the documents, the client's answers. Not built: integration with core banking, message delivery. |
| Who pays and how much? | The bank, per case or by licence; savings = cases × minutes saved × cost per minute, with the mentor's figures; the avoided risk is per account. |
