---
description: Tech Lead reads open HANDOFF blocks and dispatches them
---
Act as the Tech Lead (`docs/agents/tech-lead.md`). Collect the open `HANDOFF` blocks from the subagent answers in this session
(and any pasted here: $ARGUMENTS). For each: forward it with full context to the named subagent, or reject it with a reason.
Independent handoffs on different folders run in parallel. Finish with qa-verifier, then code-reviewer.
