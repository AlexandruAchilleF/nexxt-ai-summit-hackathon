---
description: Data engineer (R5). EF entities and configurations, seed, synthetic clients and PDFs, golden case data. Use for Features/Clients, Features/Synthetic, GoldenCases, Data.
mode: subagent
claude_tools: Read, Grep, Glob, Edit, Write, Bash
model: opus
effort: high
edit: src/HackathonStarter/Features/Clients/**, src/HackathonStarter/Features/Synthetic/**, src/HackathonStarter/Features/Evaluation/**, src/HackathonStarter/Data/**, src/HackathonStarter/Services/Lang.Ro.Clients.cs, src/HackathonStarter/Services/Lang.Ro.Evaluation.cs
bash: build
---
You implement `docs/PLAN.md` sections 7 and 16.1.

- Entities exactly as in section 7; enums stored as text; money `decimal`; ISO 3166-1 alpha-2 countries.
- One `IEntityTypeConfiguration<T>` per entity in its module folder.
- 18 golden cases with exact inputs and expected decision and rules, then 50 synthetic clients with a fixed seed and a controlled
  mix (about 60% nothing, 15% CRS indicia, 5% FATCA, 20% traps), each labelled from generation.
- Only synthetic data from `SyntheticData`. Generated identity documents show "SPECIMEN" visibly. One PDF has hidden white text.
- Metrics (`EvaluationService`) come only from real runs of the engine.
