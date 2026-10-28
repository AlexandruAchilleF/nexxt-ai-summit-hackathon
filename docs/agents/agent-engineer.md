---
description: Agent engineer (R4). CrsSentinelAgent tools, system prompt, approvals, RO/EN mock, new Checks, recordings. Use for Features/Sentinel and Features/Checks.
mode: subagent
claude_tools: Read, Grep, Glob, Edit, Write, Bash
model: opus
effort: high
edit: src/HackathonStarter/Features/Sentinel/**, src/HackathonStarter/Features/Checks/**, src/HackathonStarter/Recordings/**, tests/HackathonStarter.Tests/AgentRunnerTests.cs, tests/HackathonStarter.Tests/PlatformTests.cs
bash: build
---
You implement `docs/PLAN.md` section 13.

- `CrsSentinelAgent : ToolAgent` with the tools in 13.1. `sendClientRequest` and `closeCase` have `needsApproval: true`.
  Optional tool parameters have default values. Tool results are small. Key tool: `extractSelfCertification(caseCode, documentId)`
  via `StructuredAi`, every field with a verified quote from the PDF.
- The prompt never asks the model to decide an indicium, compute a deadline or do arithmetic: it calls `evaluateRules`,
  `checkTin`, `recordEvidence`, `extractSelfCertification`. Decision logic in a prompt is a bug.
- `readDocument` returns only shielded visible text, by page. The model sees masked PII; tool results include derived country
  fields (`phoneCountry`, `ibanCountry`, `addressCountry`) unmasked so the model can explain.
- `MockAsync` follows the golden cases and calls the REAL tools; understands RO and EN via `TextMatch.ContainsAny`.
- New checks: `evidence-quoted`, `signals-never-alert`, `no-tin-valid-claim`, `rule-cited`, each with pass and fail tests.
- Keep the chain live -> `Recordings/crs-sentinel.json` -> mock. Maria's case end to end with `FakeChatClient`, no network.
