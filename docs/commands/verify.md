---
description: Build with -warnaserror, run unit and E2E tests, report the real summary lines
agent: qa-verifier
---
Use the qa-verifier subagent. Run `dotnet build -warnaserror`, `dotnet test tests/HackathonStarter.Tests` and
`dotnet test tests/HackathonStarter.E2E`. Report the real summary lines and, for each failure, the test, expected vs actual and
the owner module. $ARGUMENTS
