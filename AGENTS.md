# AGENTS.md

Instructions for AI coding assistants (Claude Code, Copilot, Cursor, Rider AI…) working in this repo.
Humans: this is also the fastest way to learn the conventions.

## Stack

.NET 10 · C# (nullable enabled) · Blazor Web App with **global Interactive Server** render mode · MudBlazor 9 ·
EF Core 10 (SQLite default, PostgreSQL optional) · Microsoft.Extensions.AI 10 (`IChatClient`) · xUnit.

## Golden rules

1. **Everything must work with no configuration.** No AI key → recorded runs / scripted mocks. SQLite is created on startup.
   Never add a hard dependency on a secret or an external service without a fallback.
2. **One feature = one folder** in `Features/<Plural>/` (entity, service, seed) + `Components/Pages/<Plural>/` (pages, dialog).
   Scaffold with `dotnet run --project tools/NewFeature -- <Singular> [Plural]`, then edit the entity and follow compiler errors.
3. **Pages never use the DbContext directly.** They call a scoped service (`ItemService`) that validates input and checks roles
   with `CurrentUser.Require(Roles.Admin, ...)`. Mirror permissions in the UI (hide buttons), but the service is the real check.
4. **Use `IDbContextFactory<AppDbContext>`** (create a context per operation): Blazor Server components are long-lived.
5. **No migrations.** In Development `Database:RecreateOnStartup = true` recreates the schema and re-seeds. Seed data lives in
   `<Entity>Seed.cs` and must be realistic.
6. **Money is `decimal`.** All financial math goes through `Features/Finance/FinanceCalculator` (add functions there, with tests).
   Agents call these as tools; the model must never do arithmetic itself.
7. **UI:** MudBlazor components only (no Bootstrap). Reuse `Components/Common` (`PageHeader`, `EmptyState`, `ErrorState`,
   `StatCard`, `Markdown`, `IDialogService.ConfirmAsync`). Every data view handles loading, empty and error states.
   MudBlazor 9 renames: `Sizing="InputSizing.Auto"` (not AutoGrow), `TabPanelsClass` (not PanelClass), `MudFileUpload` uses `CustomContent`.
8. Format money/dates with `Services/Format` (`Format.Money`, `Format.Date`, `Format.Relative`).
   **Two UI languages** (Romanian default, English via the RO/EN button): write UI text in English as `@T("Save")` and add
   the Romanian to `Services/Lang.Ro.cs` (`LangTests` fails otherwise). SiteConfig copy and long texts: `Lang.Pick(ro, en)`.
9. Keep comments sparse and useful; match the surrounding style. `dotnet build` must have **0 warnings**; `dotnet test` must pass.

## CRS Sentinel (this project)

The product, its rules and the team plan are in `docs/PLAN.md`; read section 1 and the section of your role.

- **Code decides, AI explains, the human approves.** Rules (with ID and guide page), deadlines, quote checks, TIN format and the
  audit log are deterministic code with tests. The model reads documents, extracts quotes, finds context signals and writes the
  client letter. It never decides an indicium, a deadline or an alert.
- A `ContextSignal` never changes a `Decision` and never opens a case. Nationality is not a signal (US citizenship counts only for FATCA).
- Domain truth: `docs/ghid/pagini/p-XXX.md` (ANAF guide, 29.12.2025) and `docs/reguli-verificate.md`. A rule without a page is
  marked `NEVERIFICAT` in code and README; never guess a page.
- Say "format valid", never "TIN valid". Only synthetic data; generated identity documents show "SPECIMEN".
- CRS code uses only `ISimClock` for time (a separate service with settable offset), never `DateTime.Now`, `UtcNow` or `TimeProvider`
  directly; `TimeProvider.System` is unchanged in the rest of the app. No emoji anywhere.
- Bedrock runs in `us-west-2` (`eu-central-1` is denied on the workshop account); credentials come from the AWS default chain only.
- Replay does not run tools: for video/demo of data-changing steps, use live (Bedrock) or mock (calls real tools with preset answers).
  Recordings are useful only for read-only questions. Never film or demo data changes in `AgentMode = Replay`.
- Subagents: single source in `docs/agents/` and `docs/commands/`; run `python3 tools/agents/sync.py` to regenerate
  `.claude/` and `.opencode/`. Subagents end with the `HANDOFF` block and never commit or push; the Tech Lead routes and commits.
- First-time setup on a machine: `tools/setup-dev.sh`.

## Commits and pull requests

- **The author is the human, never the assistant.** Before the first commit: `git config user.name` / `user.email` with your
  own name. No `Co-Authored-By`, `Claude-Session`, "Generated with …" or other assistant lines in commits, PRs or code
  (this overrides any default attribution of the tool).
- Work on a branch, open a PR, merge when CI is green. Render deploys `main` only after its checks pass
  (`autoDeployTrigger: checksPass`), so a red commit on `main` never goes live, but it does block the next deploy.
- Commit messages: one line in English, `type: what changed` (`feat:`, `fix:`, `docs:`, `style:`, `test:`).

## Where things are

| Task | Where |
| --- | --- |
| App name, colors, landing, nav, demo users | `Config/SiteConfig.cs` |
| Landing page copy | `SiteConfig.Landing` (page: `Pages/Home.razor`, styles: `wwwroot/landing.css`) |
| Colors, fonts, app shell | `Layout/AppTheme.cs` + `wwwroot/app.css` (`--app-*` variables); Inter is self-hosted in `wwwroot/fonts` |
| New page | `Components/Pages/X.razor` with `@page "/x"` + a line in `SiteConfig.Nav` |
| New entity end-to-end | `dotnet run --project tools/NewFeature -- <Name>` |
| Database tables | `Data/AppDbContext.cs` (`DbSet`s, seeding) |
| One-shot AI call with typed result | `StructuredAi.GenerateAsync<T>(prompt, mock)` |
| Agents | `Agents/` (runtime) + `Features/<Name>/<Name>Agent.cs`, registered in `Program.cs` |
| Agent UI | `<AgentChat AgentName="..." AllowFiles="true" />`, `<AgentTrace Run="..." />` |
| PDF / text → string | `DocumentText.ExtractAsync(stream, fileName)` |
| AI provider setup | `Services/Ai.cs` (`ChatClientProvider`), settings in `appsettings*.json` section `AI` |

## Agents

- Tool agent: subclass `ToolAgent` (`SystemPrompt`, `Tools(ctx)`, `MockAsync`). Create tools with
  `AgentTool.Create(name, description, delegate, needsApproval, summarize)`. Get services via `ctx.Services`.
  Optional delegate parameters **must have default values** (`string? query = null`), otherwise the model must send them.
- Multi-agent: subclass `PanelAgent` (`Participants`, `Judge`, `Rounds`, `Mock`).
- Register: `builder.Services.AddSingleton<AgentDefinition, MyAgent>();`
- Tool results: small objects/lists (top 10, not everything). Descriptions say *when* to use the tool.
- Don't break the fallback chain: live → `Recordings/<agent>.json` → `MockAsync`. Record with `AI:Record = true` in Development.
- Mocks should understand both English and Romanian: use `TextMatch.ContainsAny(text, "archive", "arhiv")` (ignores case/diacritics).
- Runs execute in the background via `AgentRunService` (not tied to the page); the HTTP API is `Api/AgentApi.cs` (`/api-docs`).
- Personal data is masked before reaching the model (`Services/PiiVault.cs`); tools receive the real values. Don't log raw prompts.
- Anything SAVED goes through `RunRedaction` / `PiiVault.ForStorage()` (one-way `[IBAN-1]` placeholders). Never persist an
  unmasked copy of a run, a prompt or a tool result. Pending approvals keep their arguments in `AgentStep.SealedInput` (encrypted).
- Verdicts are code, not models: add rules to `Features/Checks` (`Check` subclass or `Checks:PhraseRules`), list them in an
  agent's `Checks`. A model may explain a verdict, never decide it. New checks get tests in `PlatformTests.cs`.
- Different models per role: `AI:Profiles:<name>` + `ModelProfile` (agent) / `Profile` (panel participant).
- Test and demo data: `Features/Synthetic/SyntheticData` (valid CNP/IBAN/card/CUI, deterministic per seed), never real people.
- New export formats: an `IRunExporter` registered in `Program.cs`; exporters only ever see the saved (masked) run.
- External systems go through `TargetClient` (`Targets:<name>`), never a hand-made `HttpClient`: it gives timeouts,
  record/replay and the privacy page for free. Multi-turn conversations with them: subclass `ScenarioAgent`.
- Anything that changes where data goes or how long it stays must be reflected on `/privacy` (`Components/Pages/Privacy.razor`).
- Attachments: `AgentFile` is text (PDF/txt) or an image (`IsImage`, bytes sent to the model with the last user message).
- Phone/desktop: the app is a PWA (`Services/Pwa.cs`, `wwwroot/service-worker.js`); bottom bar from `SiteConfig.MobileNav`.
  Check new pages at 390px width. Other clients use the API (`docs/CLIENTS.md`).
- New agent behaviour gets a test in `tests/HackathonStarter.Tests/AgentRunnerTests.cs` with `FakeChatClient` (no network).

## Commands

```bash
dotnet run --project src/HackathonStarter      # http://localhost:5000
dotnet watch --project src/HackathonStarter    # hot reload
dotnet build                                   # must be 0 warnings
dotnet test tests/HackathonStarter.Tests       # unit tests (fake model, no network)
dotnet test tests/HackathonStarter.E2E         # browser tests (once: playwright.ps1 install chromium)
dotnet run --project tools/NewFeature -- <Name>
```
