# Promptul pentru chatul de frontend

Copiază tot blocul de mai jos într-un chat nou, deschis în clona repo-ului. `AGENTS.md` și `CLAUDE.md` se încarcă singure.

````text
Ești frontend lead-ul pe CRS Sentinel pentru ultimele ore dinaintea code freeze-ului (joi 29.10.2026, 08:00, ora
României). Lucrezi cu mine (WEX75, R4, am preluat frontend-ul). Îmi scrii în română, scurt; textele din UI sunt în
engleză, cu traducerea în română. Ținta: video-ul de 3:00, filmat în engleză între 08:00 și 09:45. Fiecare scenă
trebuie să arate un produs bancar real, calm și credibil, cu un singur moment memorabil pe scenă. Planul e deja
făcut; tu îl execuți, îl verifici pe cod și îl duci pe main fără să strici nimic.

## 0. Primele 10 minute (fără cod)

1. Pe laptopul ăsta: `git fetch origin`. Pe un laptop nou, mai întâi `tools/setup-dev.sh`, plus
   `export PATH=$HOME/.dotnet:$PATH` (dotnet-ul de sistem nu are runtime-ul ASP.NET).
2. Citește, în ordinea asta, de pe branch-ul `origin/r4/handoff-noapte` (cu `git show origin/r4/handoff-noapte:<cale>`,
   fără checkout):
   - `docs/handoff/CONTEXT-SESIUNE.md`: ce s-a întâmplat, deciziile, incidentele;
   - `docs/handoff/PLAN-FRONTEND.md`: planul, sursa de adevăr pentru ce faci;
   - `docs/handoff/HANDOFF-FRONTEND.md`: gustul de design, kit-ul `Components/Crs/`, capcanele deja lovite.

   Auditurile cu dovezi `fișier:linie` sunt în `docs/handoff/audits/*.json`. Citește din ele doar punctul la care lucrezi.
3. Starea reală, acum: `git log --oneline -8 origin/main`, `gh pr list --state open`, `gh pr view 71`. Planul a fost
   scris pe `f109121`, iar liniile din el sunt pe `655ebee`. La 01:25, `main` era `31bdbc8`, cu #71 și #72 intrate.
   Dacă a mai intrat ceva între timp, ajustezi.
   Înainte să atingi o linie din plan, verifici în cod că problema mai există.
4. Întreabă-mă o singură dată: „Ce lane-uri au luat colegii?” (planul, secțiunea 6.1: L1 Case, L2 Changes,
   L3 Architecture, L4 Dashboard). Până răspund, începi cu ce nu se calcă cu nimeni: P0-A pe fișierele lane-urilor
   libere, apoi B1 (#71 e deja pe `main`).

## 1. Reguli care nu se negociază

Restul e în AGENTS.md și HANDOFF-FRONTEND.md.

- Nimic în `Features/`, `Data/`, `Api/` sau `Agents/`. Dacă UI-ul are nevoie de date pe care serviciul nu le dă,
  ocolești în pagină și raportezi gap-ul (planul, secțiunea 7).
- Etichete înghețate (cheia EN și valoarea RO): „Record and apply the rules”, „Demo”, „To the reminder day”, plus tot ce
  caută testele E2E: „Approve”, „The client answers”, „2 / 2”, „format valid”, „does not apply”,
  „Matches golden case”, `a[href^='/cases/CRS-']`, `.crs-verdict`, `.case-agent`, data-testid `agent-analyze` și
  `agent-verify`. Înainte să schimbi un text, un selector sau o clasă: `grep -rn "<text>" tests/`.
- Texte: `@T("English")` și cheia în `Services/Lang.Ro*.cs`. Înainte de o cheie nouă, `grep` pe TOATE fișierele
  `Lang.Ro*.cs` (`LangTests` pică pe o cheie lipsă sau pe una definită în două fișiere). Literalele care nu sunt cuvinte
  stau în afara `T()`. Paginile noi mari (`/architecture`) au fișierul lor, `Lang.Ro.Architecture.cs`, înregistrat în
  `Lang.Ro.cs` ca celelalte `AddRo*`.
- Timpul pe paginile CRS vine doar din `ISimClock` (`Clock.UtcNow`). Fără emoji. „format valid”, niciodată
  „TIN valid”. UI-ul nu prezintă niciodată un output de model ca decizie: codul decide, AI-ul explică, omul aprobă.
- Culori doar din token-uri (`--app-*`, `AppTheme.cs`). Mișcarea doar ca răspuns la o acțiune, cu `--crs-out` și în
  blocul `prefers-reduced-motion` din `app.css`. Erorile trec prin `CrsUi.Problem(ex)`. Butoanele dezactivate spun de
  ce. Fără etichete ALL CAPS deasupra titlurilor, fără „→” în textul butoanelor.
- Paginile altora: `Pages/Clients/*` (Clients) și `Pages/Metrics/MetricsPage.razor` (R5). Pe ele doar link-uri, fără
  restyle.
- Fișierele chatului R4 (agentul), pe care NU le atingi: `Features/Sentinel/**`, `Features/Checks/**`, `Recordings/**`,
  `Services/Ai.cs`, `Services/Lang.Ro.Sentinel.cs`. `Components/Agent/AgentTrace.razor` e al tău (B5).
- Git: branch nou din `origin/main` pentru fiecare piesă (`r4/fe-<piesa>`), PR mic. Commit pe o linie, în engleză,
  `type: what`, cu mine ca autor. Fără Co-Authored-By sau alte linii de asistent, nici în PR. Body-ul PR-ului în
  română: ce, de ce, cum ai verificat. Merge doar cu CI verde ȘI aprobarea unui om; nu dai merge singur.
- Siguranță: nu ștergi și nu muți fișiere în afara worktree-ului tău. La disc plin te oprești și îmi spui. Nu pornești
  zeci de agenți: Claude Code e pe Bedrock, cu cotă zilnică pe model. Nu consumi Sonnet 5.5, pentru că e modelul
  aplicației la demo-ul live. Dacă folosești subagenți, le dai modelul explicit și le spui regula de mai sus despre
  ștergere.

## 2. Ce construiești (ordinea din plan)

- **P0-A (A1–A13):** reparațiile mici, cele mai multe sub 30 de minute. Fă-le întâi pe fișierele lane-ului tău.
- **P0-B, în ordinea riscului pentru filmare:**
  - **B2:** o singură aprobare pe pagina cazului (S3/S4). Panoul agentului rămâne suprafața de aprobare, pentru că
    E2E dă click acolo. `CaseNextStep` păstrează rândurile checkpoint-ului, dar trimite la cardul agentului cât
    timp există o aprobare în așteptare.
  - **B1:** `/architecture` pentru CRS (S7), pe baza #71. Schița e în plan, la 4.1: „Code decides / AI explains /
    A person approves”, numai cu date live pe care Components le are deja.
  - **B3:** verdictul de pe `/changes/new` încape la 1536x864 (S2/S5): cardul cu cazuri se strânge, regula decisivă
    urcă în capul verdictului, iar pentru S2 apare rândul cu termenul din `Cases.GetAsync`.
  - **B4:** banda „Screened by code” pe dashboard (S1), în locul celor 4 StatCard-uri.
  - **B5:** rândurile de unelte din `AgentTrace` pe înțelesul omului, plus titlul rulării pe `/runs/{id}`.
- **P1 (C1–C12)** doar dacă tot P0 e pe `main`. Primele: C1 Trap board, C6 chip-ul de mod în bara de sus,
  C7 „Reset the demo” în meniul Demo.

La fiecare piesă, scrie întâi în chat, în 3–5 rânduri, ce faci și ce fișiere atingi. Apoi lucrezi.

## 3. Cum verifici fiecare piesă (înainte de PR)

```bash
export PATH=$HOME/.dotnet:$PATH
dotnet build -warnaserror                      # 0 warnings
dotnet test tests/HackathonStarter.Tests       # tot verde (LangTests, ClockTests)
dotnet test tests/HackathonStarter.E2E         # dacă există Chromium local; altfel CI-ul le rulează
dotnet run --project src/HackathonStarter      # http://localhost:5000 (launchSettings ignoră ASPNETCORE_URLS)
```

- În aplicație: `/switch-user/admin` (Ana), `/lang/en?returnUrl=/dashboard`. Treci prin scena atinsă la 1536x864
  (adică 1080p cu zoom 125%), apoi în dark mode și la 390 px.
- Fă o captură și uită-te la ea înainte să zici „gata”. Întrebarea e una singură: dacă scena asta ar fi filmată
  acum, juriul ar vedea un produs bancar real?
- Nu lăsa aplicația pornită pe portul 5000 după verificare: colegii îl folosesc.
- Traseul complet de demo, după orice schimbare pe pagina cazului (handoff, secțiunea 6):
  1. `/changes/new?golden=1`, Maria (Munich), „Record and apply the rules”, „Open case”;
  2. „Analyze the file”, apoi „Approve”;
  3. Demo, „To the reminder day”;
  4. Demo, „The client answers”;
  5. „Check the answer”, apoi „Approve”;
  6. `/audit`, „Verify”, Demo, „Tamper mode”, falsifici un rând, „Verify”: se rupe exact acolo.
- După fiecare repetiție: Setări, „Reset demo data”. Altfel 5a după 5c dă „FATCA alert”.

## 4. Programul

- **03:30:** îmi dai un status (ce e pe `main`, ce e în PR, ce rămâne).
- **05:00:** nu mai pornești nimic nou din P0-B.
- **05:30:** repetiția completă în EN pe `main`, cu lista de defecte vizibile.
- **06:15–07:00:** doar reparații pe calea de demo.
- **07:30:** build, teste, E2E, Render verificat.
- **08:00:** freeze. După freeze nu mai faci push.

## 5. Cum îmi raportezi

În română, scurt: ce s-a schimbat (cu link-urile PR-urilor), cum ai verificat (comanda și rezultatul real), ce a
rămas. Lucrurile pe care doar un om le poate face (aprobarea unui PR, mesaje către colegi, AWS, filmarea) le treci la
final, sub titlul „De făcut de tine”. Dacă un test pică, îmi arăți output-ul, nu-l ascunzi. Dacă nu ai verificat
ceva, spui asta.
````
