# The documentation, file by file

Start with the [README](../README.md) at the root of the repo. The files below are grouped by what they are for; the
working notes are kept as they were written during the hackathon (most in Romanian).

## For the jury

| File | What it contains |
| --- | --- |
| [`SUBMISSION.md`](SUBMISSION.md) | What was submitted, how the jury can check it in 5 minutes, the criteria and where the evidence is |
| [`presentation/TRACE.pdf`](presentation/TRACE.pdf), [`PRESENTATION.md`](PRESENTATION.md) | The 10 submitted slides, and their text with the speaker notes and the source of every figure |
| [`PITCH.md`](PITCH.md) | The video script, word for word, with the sources of the figures and the likely questions |
| [`screenshots/`](screenshots/) | The README screenshots, regenerated with `tools/Screenshots` |

## The domain (the source of truth)

| File | What it contains |
| --- | --- |
| [`ghid/`](ghid/) | The ANAF DAC2/CRS/FATCA Guide of 29.12.2025: the PDF and the text of every page (`pagini/p-XXX.md`) |
| [`reguli-verificate.md`](reguli-verificate.md) (Romanian) | Every rule checked against the text of the guide, with its page, and the mapping to the IDs in code |
| [`GOLDEN-CASES.md`](GOLDEN-CASES.md) (Romanian) | The 21 golden cases: the exact input, the expected decision, rules and page, written before the code |
| [`RESEARCH-CRS.md`](RESEARCH-CRS.md) (Romanian) | The facts from the guide and the law, and the questions for the mentor |
| [`surse/`](surse/) | Saved sources (the OECD TIN sheet for Austria, used by golden case 3) |
| [`PRIVACY-TEXT.md`](PRIVACY-TEXT.md) (Romanian) | The text of the `/privacy` page, with the quotes from the law |

## How it works

| File | What it contains |
| --- | --- |
| [`CUM-FUNCTIONEAZA.md`](CUM-FUNCTIONEAZA.md) (Romanian) | The system end to end, Maria's case step by step |
| [`PLAN.md`](PLAN.md) (Romanian) | The implementation plan, written before the code: domain, data model, rules, architecture, roles |
| [`DEPLOY.md`](DEPLOY.md) | Deploying to Render or Azure |
| [`TEMPLATE.md`](TEMPLATE.md), [`CLIENTS.md`](CLIENTS.md) | The team's .NET template, written before the hackathon (README §13): the agent runtime, configuration, the HTTP API, other clients |

## Working notes from the hackathon

| File | What it contains |
| --- | --- |
| [`PLAYBOOK.md`](PLAYBOOK.md) (Romanian) | The team's playbook for the two days; its first version is in the template commit (README §13) |
| [`research-prompt.md`](research-prompt.md) (Romanian) | The research prompt used to choose the challenge, before the event (in the template commit) |
| [`RESEARCH-ANAF-BANCI.md`](RESEARCH-ANAF-BANCI.md), [`anaf-banci/`](anaf-banci/) (Romanian) | Background research on how ANAF works with Romanian banks (28.10.2026) |
| [`roluri/`](roluri/) (Romanian and English) | The work sheets of three roles |
| [`ui/macheta-caz.html`](ui/macheta-caz.html) | The first mockup of the case page |
| [`UNELTE-AI.md`](UNELTE-AI.md) (Romanian) | The AI coding tools the team used |
| [`agents/`](agents/), [`commands/`](commands/) | The source of the team's AI subagents and commands (`tools/agents/sync.py` generates `.claude/` and `.opencode/`) |
