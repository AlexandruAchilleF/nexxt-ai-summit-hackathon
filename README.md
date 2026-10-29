# CRS Sentinel

**Every change to a client's data becomes a CRS/FATCA decision, before the legal deadline runs out.**

The app UI is in Romanian by default; switch to English with the RO/EN button in the top bar.

When a client changes their address, phone number, a standing order or a power of attorney, the bank has to find out
again where the client pays taxes. Otherwise it reports the client wrongly to ANAF, on every account. CRS Sentinel catches
the change, applies the rules from the ANAF DAC2/CRS/FATCA Guide **in code** (each rule with an ID and a page), starts the
deadline, gathers the evidence, writes the request to the client and follows the answer until the case is closed, with
proof of the confirmation.

**Code decides, AI explains, a person approves.**

Raiffeisen Bank, *AI for Operational Efficiency* challenge, NEXXT AI Hackathon (VibeHack), 28–29 October 2026.

| | |
| --- | --- |
| The app | <https://hackathon-starter-k0ym.onrender.com/> (access code: `crs-nexxt-2026`; the first start takes up to a minute) |
| Video (≤ 3 min) | to be added at submission |
| The presentation | [`docs/presentation/TRACE.pdf`](docs/presentation/TRACE.pdf) (deck notes: [`docs/PRESENTATION.md`](docs/PRESENTATION.md)) |
| How the jury can check it in 5 minutes | [`docs/SUBMISSION.md`](docs/SUBMISSION.md#how-the-jury-can-check-it-in-5-minutes) |
| The full plan | [`docs/PLAN.md`](docs/PLAN.md) (Romanian) |
| How it works, end to end | [`docs/CUM-FUNCTIONEAZA.md`](docs/CUM-FUNCTIONEAZA.md) (Romanian) |

---

## 1. The problem

In a **self-certification**, the client declares the states where they are tax resident and the TIN for each one. When
the bank "știe sau are motive să știe" ["knows or has reason to know"] that the situation has changed, the
self-certification loses its validity. On a new account, the bank has **90 days** to obtain a new self-certification or a
reasonable explanation; otherwise the client is treated as tax resident **both** in the original jurisdiction **and** in
the new one (ANAF Guide, p. 35–36).

Today, the check means a person who compares fields, searches through documents, writes to the client and keeps a
deadline in mind. CRS Sentinel does the repetitive part; the person approves what goes out and what gets closed.

**Source of truth:** the ANAF DAC2/CRS/FATCA Guide, version 29.12.2025, in [`docs/ghid/`](docs/ghid/) (the PDF and the
extracted pages, `pagini/p-XXX.md`). The rules verified against the text of the guide:
[`docs/reguli-verificate.md`](docs/reguli-verificate.md).

## 2. Who does what

| Who | What it does | What it never does |
| --- | --- | --- |
| **The code** (deterministic, tested) | Applies the rules from the guide (ID + page), calculates the deadlines, verifies the quotes, checks the TIN format, keeps the audit log | It does not guess: the same input always gives the same result |
| **The AI** (Claude on Amazon Bedrock) | Reads the documents, extracts quotes, finds context signals, writes the text to the client, explains | It does not decide whether something is an indicium, does not calculate deadlines, does not raise alerts |
| **The human** (operator, tax department) | Approves the request to the client and the closing of the case, decides the borderline cases | — |

The system uses **one agent with 14 tools** (`CrsSentinelAgent`), orchestrated by code. The agent is called only when
there is a document to read, evidence to extract or a letter to write; it never gets the last word.

## 3. The 12 design interpretations

The rules in the guide are clear, but some real-life situations need an operational interpretation. We documented every
one in [`docs/reguli-verificate.md`](docs/reguli-verificate.md) and covered them in tests:

1. **Reasonableness test on new accounts:** The guide mentions the reasonableness test for the self-certification on
   account opening (p. 33). We apply the same indicia as signals that contradict the self-certification.
2. **Pre-existing vs. new account:** Computed per regime from the opening date (guide p. 20): CRS pre-existing = held
   on 31 Dec 2015, FATCA pre-existing = held on 30 Jun 2014.
3. **The deadline for pre-existing accounts:** Fiscal Procedure Code, Annex 2, item 1: the latest of 31 December and
   90 days from the change. (Guide p. 29 writes "sau" ["or"]; we chose the latest date, in line with the OECD
   Commentary).
4. **Standing orders:** A standing order to transfer funds to an account in a reportable jurisdiction is an indicium only
   for other than depository accounts (guide p. 29). For depository accounts (current accounts), it is not a CRS
   indicium; for FATCA it is always an indicium (p. 31).
5. **Phone number next to a Romanian one:** A foreign phone number is an indicium only if there is no Romanian phone
   number on file (guide p. 29). If both exist, no CRS indicium.
6. **In-care-of / hold-mail address:** An in-care-of or hold-mail address is an indicium only if it is the sole address
   on file (guide p. 29).
7. **Documentary evidence:** The list of acceptable documents follows the guide strictly (p. 27–28): certificate of
   tax residence, valid government-issued ID, utility bill (not older than 3 months) for residence address.
8. **TIN format vs. TIN existence:** We verify the syntax and format of the TIN (using European Commission rules and local
   rules), not that the person exists in the foreign tax registry (which banks cannot query directly).
9. **Dual residence:** If the client declares dual residence (e.g. RO + DE), both must be supported by valid TINs and
   self-certifications; reporting applies to both.
10. **Prompt injection defence (Hidden Text Shield):** White text, zero-point fonts and hidden instructions in uploaded
    PDFs are stripped before the document reaches the model.
11. **Evidence quoting:** Every fact quoted by the AI is substring-matched against the visible document text. If the
    quote is not found verbatim, the tool call is rejected.
12. **Tamper-evident audit log:** Every state change, rule evaluation, draft, approval and document scan is hashed into
    a SHA-256 chain. Changing any previous entry breaks the hash of all subsequent entries.

---

## 4. How to run it locally

Requires **.NET 10 SDK**. No external services, no cloud accounts and no API keys required to run in simulation/mock
mode (all 14 tools run with the deterministic local mock).

```bash
# Clone the repository
git clone https://github.com/AlexandruAchilleF/nexxt-ai-summit-hackathon.git
cd nexxt-ai-summit-hackathon

# Run the app (starts on http://localhost:5000)
dotnet run --project src/HackathonStarter
```

Open <http://localhost:5000> in your browser. Log in as **Ana Popa** (operator/admin, no password needed in demo mode).

### Run with Amazon Bedrock (optional)

Set the environment variables or add them to `appsettings.Local.json` (do not commit):

```json
{
  "AI": {
    "Provider": "Bedrock",
    "BedrockRegion": "us-west-2",
    "AgentMode": "Live"
  }
}
```

### Run the tests

```bash
dotnet test
```

All 1,466 unit and integration tests pass with 0 failures.

---

## 5. Repository structure

```
nexxt-ai-summit-hackathon/
├── src/HackathonStarter/          # The .NET 10 Blazor Server application
│   ├── Components/Pages/          # Blazor UI pages (Cases, Changes, Clients, Audit, Metrics)
│   ├── Features/Cases/            # Case management, states, workflow
│   ├── Features/Changes/          # Customer data changes, event handling
│   ├── Features/Deadlines/        # Legal deadline calculator (DL-NEW-90, DL-PRE-YEAR)
│   ├── Features/Evaluation/       # Golden cases suite (21 cases), metrics
│   ├── Features/Rules/            # Deterministic rule engine (29 ANAF rules)
│   ├── Features/Sentinel/         # CrsSentinelAgent (14 tools, system prompt, mock)
│   ├── Features/Synthetic/        # Synthetic customer & PDF generators
│   └── Services/                  # Shield, audit log, PII masking, i18n (RO/EN)
├── tests/
│   ├── HackathonStarter.Tests/    # 1,426 unit & integration tests
│   └── HackathonStarter.E2E/      # 40 Playwright end-to-end tests
├── docs/
│   ├── presentation/TRACE.pdf     # Official 10-slide presentation deck
│   ├── PRESENTATION.md            # Slide deck notes & speaker transcript
│   ├── PITCH.md                   # Video pitch script (3-minute timed narration)
│   ├── SUBMISSION.md              # Hackathon submission checklist & text
│   ├── PLAN.md                    # Complete technical and domain plan (RO)
│   ├── GOLDEN-CASES.md            # The 21 golden test cases
│   └── reguli-verificate.md       # 29 rules mapped to ANAF guide pages
└── tools/DemoFilm/                # Automated Playwright demo runner for video
```

---

## 6. Architecture summary

```
                +---------------------------------------+
                |           Customer Data Change        |
                |  (address, phone, standing order, POA)|
                +-------------------+-------------------+
                                    |
                                    v
+-----------------------------------------------------------------------+
| CODE DECIDES (Deterministic Engine)                                   |
| • 29 ANAF DAC2/CRS/FATCA rules with exact IDs and guide pages        |
| • Automatic case opening & legal deadline calculation                 |
| • PII masking (CNP, IBAN, card, phone, email)                         |
| • Hidden Text Shield (prompt injection defense)                       |
+-----------------------------------+-----------------------------------+
                                    |
                                    v
+-----------------------------------------------------------------------+
| AI EXPLAINS (CrsSentinelAgent - Claude on Bedrock)                    |
| • 14 tools: read documents, extract evidence quotes, detect signals  |
| • Draft request letter in customer's language (RO / EN)               |
| • Verbatim quote verification against document visible text           |
+-----------------------------------+-----------------------------------+
                                    |
                                    v
+-----------------------------------------------------------------------+
| A PERSON APPROVES (Four-Eyes Banking Compliance)                     |
| • Operator reviews quoted evidence and approves customer letter       |
| • Operator approves case resolution / closure with proof             |
| • Every step recorded in SHA-256 hash-chained audit log               |
+-----------------------------------------------------------------------+
```

---

Raiffeisen Bank Hackathon • VibeHack @ NEXXT AI Summit • 28–29 October 2026.
