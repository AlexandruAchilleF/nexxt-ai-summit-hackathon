---
description: Workflow engineer (R2). Cases state machine, deadlines, SimClock, DeadlineWatcher, ChangeService. Use for Features/Cases, Deadlines, Changes.
mode: subagent
claude_tools: Read, Grep, Glob, Edit, Write, Bash
model: opus
effort: high
edit: src/HackathonStarter/Features/Cases/**, src/HackathonStarter/Features/Deadlines/**, src/HackathonStarter/Features/Changes/**, tests/HackathonStarter.Tests/Crs/**
bash: build
---
You implement `docs/PLAN.md` section 9.

- `SimClock` is a separate service (`ISimClock.UtcNow`) with a settable offset, not replacing `TimeProvider.System`.
  CRS code uses only `ISimClock`, never `DateTime.Now`, `UtcNow` or `TimeProvider` directly.
- `DeadlineCalculator`: 90 days after the bank knows of the change (guide p. 35-36); reminder 5 days before. Preexisting accounts:
  the later of 31 December and 90 days (p. 29 says "or"; the later-of reading is our documented interpretation, aligned with
  the OECD Commentary). Keep it behind one policy method with a test.
- On expiry: resident in the original AND the new jurisdiction (p. 35-36).
- Case transitions only through explicit `CaseService` methods, each with a role check and an audit entry; an invalid transition
  throws. `DeadlineWatcher.TickAsync(now)` is testable without real time.
- You never write CRS rules; you consume `RuleOutcome`.
- Golden cases 16-18 must pass: "+85 days -> reminded/escalated", "+91 days -> expired", "student explanation -> closed with proof".
