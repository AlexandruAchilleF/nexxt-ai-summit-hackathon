# Frontend handoff: CRS Sentinel UI

For the next agent working on `Components/` and `wwwroot/`. Read `AGENTS.md` first; this file adds what is not
written there: the design taste, the CRS component kit, the patterns, the traps, and what is still open.

State on 2026-10-28: PR #49 (`ui/crs-pages`) is merged into `main` (84122d3), on top of #45 (client page),
#47 and #48 (scan). Branch from `main`, not from `ui/crs-pages`.

## 1. Hard constraints

- **No backend changes from frontend work.** Nothing in `Features/`, `Data/`, `Api/`, `Agents/`. If the UI needs data the
  service does not give, work around it in the page (see `CasesPage` loading drafts per case) and report the backend gap.
- **Pitch labels are frozen.** The team's pitch is built on these; do not rename them (EN key or RO value) without telling the team:
  - `Record and apply the rules` / „Înregistrează și aplică regulile” (button on `/changes/new`)
  - `Demo` / „Demo” (the top-bar menu, `Crs/DemoDate.razor`)
  - `To the reminder day` / „La ziua mementoului” (Demo menu action offered by `CaseDetailPage.OfferDemo`)
  Treat any other label used in `docs/PITCH.md` or the E2E tests the same way.
- **Pages owned by others:** `Pages/Clients/*` and `Lang.Ro.Clients.cs` (PR #45, teammate), `Pages/Metrics/MetricsPage.razor`
  (R5). Link to them, do not restyle them without asking.
- Commits: human author, one line English `type: what`, no assistant/Co-Authored-By lines. PR body in Romanian.
  Run `/verify` before committing. Merge only when CI is green and a human approved.
- Code decides, AI explains, the human approves: the UI never presents a model output as a decision. Say "format valid",
  never "TIN valid". No emoji. Time on CRS pages comes from `ISimClock` only (`Clock.UtcNow`), never `DateTime.Now`.

## 2. Design taste (what "right" looks like here)

**Direction: calm, Apple-leaning compliance instrument.** One authored moment per page, everything else quiet.
The case page is the product; the deadline ruler and the approval checkpoint are the signature pieces.

- **Hierarchy follows the operator's job:** what to do next comes first (CaseNextStep sits *above* the clock), then why
  (indicia, rule, guide page), then evidence (correspondence, case log). Never put demo scaffolding in the operator flow.
- **Plain words lead, codes follow.** Rule titles first, rule IDs secondary (`CrsUi.ShortRule`, ID in `title`). Status is
  always word + icon + tone, never colour alone (`Pill`). Dates follow the UI language (`CrsUi.Date*`, culture from `Lang.IsEn`).
- **Human approval should feel like signing:** inline checkpoint row that restates recipient, channel, documents, deadline
  and approver; a four-eyes warning when approver == drafter. Irreversible actions never happen in one click.
- **Disabled buttons say why** (a hint line under them). Errors go through `CrsUi.Problem(ex)` (friendly prefix + service
  message), never raw exception text.
- **Decluttered:** no decorative stripes, no nested cards, no hero-metric clutter. Dense lists use `app-list` rows with a
  header row, a chevron, and hover. Empty states always offer the next action (usually "Record a change").
- **Motion is meaning, not decoration.** Animations mark a state change the user caused: the ruler fill and cursor slide
  when the date moves, milestones "hit" when passed, the verdict stamps in, cleared rules tick in sequence, the audit chain
  sweeps on Verify and breaks at the tampered row, the stage node pops. Page-load choreography was deliberately reduced;
  do not add entrance animations to every element. Everything respects `prefers-reduced-motion` (block at end of the CRS
  section in `app.css`); add new keyframes inside that coverage.
- Easing: `--crs-out` (`cubic-bezier(.16,1,.3,1)`) for CRS motion; `--app-fast/base/slow` (160/280/480ms) and `--app-spring`
  from the shell. Typical durations 320-640ms; the ruler 1100ms.
- Keyboard and AT: Segmented is a real radiogroup (roving tabindex, arrows), the Demo activator is a focusable
  `role=button`, `/` focuses the search field (`data-hotkey="/"`), milestones carry text, not only `title`.
- Check every page at **390px** (PWA, bottom bar) and in dark mode.

### Tokens (do not hardcode colours)

`Layout/AppTheme.cs` defines the MudBlazor palette; `wwwroot/app.css` derives `--app-*`:

| Token | Use |
| --- | --- |
| `--app-accent` (Info: #0066CC light / #2997FF dark) | accent as text and icons |
| `--app-accent-fill` (Primary #0071E3) | fills behind white text |
| `--app-accent-tint`, `--app-success-tint`, `--app-warning-tint`, `--app-danger-tint` | tinted backgrounds |
| `--app-fill`, `--app-fill-strong`, `--app-separator`, `--app-well`, `--app-ring` | neutrals |
| `--app-radius-sm/md/lg/xl` = 10/14/20/28px | radii |
| `--app-mono` | codes, hashes, case numbers |

Success is teal (#00796B / #4FD6C0) so pass/fail never relies on red vs green. Warning #A14800, Error #D70015.
Font: Inter, self-hosted. Input text weight is forced to 400 in `app.css` (AppTheme Subtitle1 is 600 and leaked into inputs).

## 3. Where things are

### CRS component kit: `Components/Crs/` (imported globally via `_Imports.razor`)

| File | What |
| --- | --- |
| `CrsUi.cs` | Labels/tones/icons for the enums (`Label`, `RuleTitle`, `ShortRule`, `AuditKind`, `Actor`, `Channels`), dates in UI culture (`Date`, `DateShort`, `DateTimeText`), `DaysBetween` (ceiling, rounded to the minute, so day 0 is not "89 days"), `DayNumber`, `Problem(ex)`. Add new enum labels here, not inline. |
| `CrsSignals.cs` | Static `Changed` event + `MoveClockAsync(move, cases, clock)` (advance clock, `TickAsync`, raise). Pages that show case data subscribe in `OnInitialized` and unsubscribe in `Dispose`. |
| `DemoContext.cs` | Per-circuit object cascaded by `MainLayout`. A page calls `Demo.Offer(this, title, [new DemoAction(...)])` and `Demo.Withdraw(this)` in `Dispose`. This is the **only** place demo shortcuts go. |
| `DemoDate.razor` | The top-bar "Demo" menu: page actions, then Demo date (+1/+7/+30 days, back to real date), then Scenarios (Golden cases -> `/changes/new?golden=1`, Measure the rules). |
| `DeadlineClock.razor` | The ruler: `--p` fill, cursor via container units (`100cqw`), glint keyed on the day, milestones, events list, closed ("N days to spare") and expired states. |
| `CaseStages.razor`, `CaseStatusPill.razor`, `DecisionPill.razor`, `DaysLeft.razor` | Status display. |
| `Segmented.razor` | Accessible segmented filter; `Options` = `(value, label, count)`. |
| `CountryPicker.razor` | MudAutocomplete over ISO codes (no free-text countries). |

Shared: `Components/Common/` (`PageHeader`, `EmptyState`, `ErrorState`, `StatCard`, `Pill`, `ActivityRings`, `AreaChart`,
`Markdown`, `IDialogService.ConfirmAsync`).

### Pages

| Route | File | Notes |
| --- | --- | --- |
| `/dashboard` | `Pages/Dashboard.razor` | CRS stats linking to `/cases?view=urgent|approval`, next-deadline bars, decisions parsed from audit `change.received` payloads, empty-state hero to `/changes/new?scenario=4`. |
| `/cases` | `Pages/Cases/CasesPage.razor` | Segments open / approval / urgent / done (`?view=`), search with `/`, header row, rule tags, "Needs approval" pill. Drafts come from `GetAsync` per open case (queue is shallow). |
| `/cases/{code}` | `Pages/Cases/CaseDetailPage.razor` + `CaseNextStep.razor` | Header, stages, next step (Drafting / Approval / Answer / Closing / Outcome with `Checkpoint(...)` confirm rows), clock, indicia with help tooltips, context signals (never a decision), cleared rules from `Engine.EvaluateCurrent`, correspondence, case log. Demo actions: To the reminder day, Past the deadline, Fill in the sample answer. |
| `/changes/new` | `Pages/Changes/NewChangePage.razor` | Golden picker only with `?golden=1` / `?scenario=N` (case 12 excluded, see open issues). Verdict cascade with "Open case" and "See the client". Disabled reason via `MissingReason`. |
| `/audit` | `Pages/Audit/AuditPage.razor` (+ `.razor.css`) | Case links, readable payload (raw JSON in tooltip), steps/everything segments, verify sweep, tamper column only in tamper mode (`?tamper=1` or Demo menu, admin only). |
| `/clients`, `/clients/{code}` | `Pages/Clients/*` | Teammate's (#45). |
| `/metrics` | `Pages/Metrics/MetricsPage.razor` | R5's. |

Shell: `Layout/MainLayout.razor` (DemoContext cascade, `<DemoDate>`), `Components/App.razor` (JS helpers
`revealElement`, `focusChecked`, `/` hotkey), `Config/SiteConfig.cs` (nav, mobile nav).

CSS: everything CRS is in the CRS section of `wwwroot/app.css` (`.crs-*`, `.demo-*`). Scoped `.razor.css` only for the audit table.

### Translations

UI text is English in `@T("...")`; Romanian goes in a `Services/Lang.Ro*.cs` file. CRS page keys live in
`Lang.Ro.CrsUi.cs`. `LangTests` fails on a missing key **and on a key defined in two files**: before adding a key,
grep all `Lang.Ro*.cs`. Literals that are not words (like `"-"`) must stay outside `T()`, or LangTests flags them.

## 4. Patterns to copy

- **Data view states:** `_error` -> `ErrorState` with retry; `null` -> `MudSkeleton` shapes; empty -> `EmptyState` with an
  action; filtered-empty -> "Nothing matches".
- **Live refresh:** subscribe to `CrsSignals.Changed`, reload inside `InvokeAsync`, unsubscribe in `Dispose`.
- **Keep the result visible after an action:** refresh the related data without clearing the result you just showed
  (the verdict on `/changes/new` disappeared once because of this).
- **Replaying an animation:** change an `@key` (e.g. `_sweep++` on audit, the day number on the ruler glint) instead of JS.
- **Staggered rows:** `style="--i:@i"` and CSS `animation-delay: calc(min(var(--i), 14) * 70ms)`; cap the index.
- **JS interop** only in `OnAfterRenderAsync(firstRender)`, and the JS must tolerate missing elements.
- **Role mirroring:** hide admin-only UI (`CurrentUser.GetAsync()`), but the service still checks.

## 5. Traps already hit

- System `dotnet` has no ASP.NET runtime (NETSDK1226): use `$HOME/.dotnet/dotnet`.
- MudBlazor 9 puts `Style` on the inner input, so field widths do not apply: use classes (`crs-field-*`).
- Selects vs text fields have different heights: use `Margin.Dense` on both.
- `@code` vs `out var code` collides in Razor; avoid a local called `code`. Quotes inside Razor attributes: move to a helper method.
- Two siblings with the same `@key` crash the render.
- Ruler cursor overflowed with `%`; container query units (`100cqw`) fixed it.
- `ClientService.ListAsync` and `CaseService.QueueAsync` are shallow (no collections, no requests): load in full when you need them.
- MudBlazor menus do not open on a synthetic click in the automation browser (t3 preview); keyboard (Enter) works. Use an
  incognito profile and `preview_snapshot save=true`, then read the PNG.

## 6. Verification

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet build -warnaserror                 # 0 warnings
dotnet test tests/HackathonStarter.Tests  # last run: 1254/1254
dotnet test tests/HackathonStarter.E2E    # last run: 30/30 (audit tamper test opens /audit?tamper=1)
dotnet run --project src/HackathonStarter # http://localhost:5000
```

Demo route to click through after any change: `/changes/new?golden=1` -> pick Maria (Munich) -> Record and apply the rules
-> Open case -> draft and approve the request (checkpoint) -> Demo > To the reminder day -> Fill in the sample answer ->
close -> `/audit` -> Verify -> Demo > Tamper mode -> tamper a row -> Verify (breaks there).

## 7. Critique and what is still open

Last impeccable critique (`.impeccable/critique/2026-10-28T20-15-18Z__src-hackathonstarter-components-pages.md`, local,
not committed) scored **23/40** before the fixes. Since then: approval checkpoints and four-eyes warning, one Demo menu,
golden picker hidden behind `?golden=1`, rule titles leading, dates localized, "Answer received" stage label, column headers,
approval/urgent filters, friendly errors, disabled reasons, country picker, contrast and input-weight fixes, fewer entrance
animations, readable audit payload. A re-critique was not run after the fixes; expect roughly 28-30.

Still open (frontend):
- No sorting, saved filters or bulk approve on `/cases` (efficiency heuristic was 1/4).
- No glossary for "indicium", "preexisting", rule IDs beyond tooltips.
- Dashboard (stat row + rings) is still generic next to the case page; the shell keeps starter leftovers (the "Agents" nav group).
- Bottom bar on mobile holds Settings instead of Clients/Audit (`SiteConfig.MobileNav`, coordinate with the Clients owner).
- Phone numbers unformatted on the client side (teammate's page).

Backend issues seen from the UI (report, do not fix in frontend work):
- Golden case 12 does not match its expected result, so it is excluded from the picker.
- The audit writes "case opened" before "change received", so the case log reads out of order.
- `ClientService.ListAsync` is shallow (see traps).
