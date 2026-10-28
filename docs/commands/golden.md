---
description: Run the 18 golden cases and show a table of expected vs actual
agent: qa-verifier
---
Use the qa-verifier subagent. Run `dotnet test tests/HackathonStarter.Tests --filter "FullyQualifiedName~GoldenCase" --logger "console;verbosity=normal"`.
Show a table with columns: #, case, expected decision, actual decision, trece/pică. Then the owner of each failing case. $ARGUMENTS
