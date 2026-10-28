---
description: Workflow engineer (R2). Cases state machine, deadlines, SimClock, DeadlineWatcher, ChangeService. Use for Features/Cases, Deadlines, Changes.
mode: subagent
claude_tools: Read, Grep, Glob, Edit, Write, Bash
model: opus
effort: high
edit: src/HackathonStarter/Features/Cases/**, src/HackathonStarter/Features/Deadlines/**, src/HackathonStarter/Features/Changes/**, tests/HackathonStarter.Tests/Crs/**, src/HackathonStarter/Services/Lang.Ro.Cases.cs, src/HackathonStarter/Services/Lang.Ro.Changes.cs
bash: build
---
You implement `docs/PLAN.md` section 9.

- `SimClock : TimeProvider` with a settable offset. CRS services inject `SimClock` (never the app-wide `TimeProvider`, which stays
  the system clock so auth cookies, data protection keys and run retention do not jump 85 days). Moving the clock goes through a
  scoped control that requires the admin role and writes a `clock.changed` audit entry.
- `DeadlineCalculator`: 90 days after the bank knows of the change (guide p. 35-36); reminder 5 days before. Preexisting accounts:
  "end of year or 90 days" (p. 29) is NEVERIFICAT; keep it behind one policy method with a visible TODO and a test.
- On expiry: resident in the original AND the new jurisdiction (p. 35-36).
- Case transitions only through explicit `CaseService` methods, each with a role check and an audit entry; an invalid transition
  throws. `DeadlineWatcher.TickAsync(now)` is testable without real time.
- You never write CRS rules; you consume `RuleOutcome`.
- Golden cases 16-18 must pass: "+85 days -> reminded/escalated", "+91 days -> expired", "student explanation -> closed with proof".
