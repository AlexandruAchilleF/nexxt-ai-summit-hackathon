# Contextul sesiunii de noapte (28 spre 29.10.2026, 23:30–01:10): frontend și pitch

> Pentru chatul care preia munca, pe alt laptop. Citește întâi `PLAN-FRONTEND.md` (planul) și `HANDOFF-FRONTEND.md` (gustul de
> design, kit-ul CRS, capcanele). Fișierul ăsta spune ce s-a întâmplat, ce e decis și ce rămâne.

## Cine și ce

- **Utilizatorul:** WEX75, rolul R4 (agent și AI), care a preluat și frontend-ul. Scrie în română, fără diacritice uneori.
- **Echipa:**
  - Alexandru (AlexandruAchilleF): R5, plus multe PR-uri de integrare;
  - David (DavidVamaiotu): R3 și paginile UI din #49;
  - shers0a: R2, CI;
  - Persoana 5: pitch-ul (`docs/PITCH.md`).
- **Termenele de joi, 29.10.2026:**
  - 08:00: code freeze;
  - 08:00–09:45: filmare;
  - 10:30: predare (limita e 11:00);
  - video de cel mult 3:00, **în engleză**.

## Ce a cerut utilizatorul (cererea originală)

- Toate gap-urile de frontend și toți conectorii: ce are backend-ul și ce se vede sau nu în UI.
- Propuneri ca frontend-ul să fie „cel mai mișto”, „extrem de sexy”.
- **Înainte de construit:** pitch-ul perfect, „full business”, captivant, cu research serios.
- Demo-ul și pitch-ul **trebuie să fie în engleză** (UI-ul are comutator RO/EN).

## Precizările utilizatorului (rămân valabile)

- Paginile CRS sunt cele din #49: `Components/Pages/{Cases,Changes,Audit,Metrics,Clients}` și `Dashboard.razor`. Componentele comune sunt în `Components/Crs/`.
- Textele sunt în engleză, ca `@T("...")`; româna intră în `Services/Lang.Ro.CrsUi.cs`. `LangTests` pică dacă lipsește o traducere sau dacă o cheie e definită în două fișiere.
- **Panoul agentului** de pe pagina cazului a fost făcut de altă sesiune (#59, deja pe `main`). **Nu-l refacem**; reparăm doar dublura de aprobare și rândurile de unelte (B2 și B5 în plan).
- Mesajele pe care le trimit butoanele sunt în `Features/Sentinel/SentinelActions.cs`.
- **#53 e închis și depășit: nu-l folosi ca bază.**

## Ce s-a produs

- `docs/handoff/PLAN-FRONTEND.md`: planul complet, care conține:
  - registrul de gap-uri P0-A, P0-B, P1, P2;
  - matricea de conectori;
  - design-ul și momentul-semnătură al fiecărei scene;
  - **vocea în engleză scenă cu scenă**;
  - stratul de business;
  - lane-urile cu fișierele lor și programul nopții;
  - gap-urile de backend, cu proprietarii;
  - riscurile;
  - „De făcut de tine”.
- `docs/handoff/audits/*.json`: trei audituri pe Opus, pe `655ebee`, făcute pe cod și pe aplicația pornită în EN la 1536x864:
  - `dashboard.json`: 34 de puncte;
  - `new-change.json`: 38;
  - `connectors.json`: 39.

  Fiecare punct are dovada `fișier:linie`, propunerea, orele și proprietarul. Auditurile pentru pagina cazului, „audit, arhitectură și metrici” și engleza au murit pe cota zilnică Bedrock (vezi mai jos). Ce era critic din ele e verificat direct în cod și trecut în plan, secțiunea 0.
- **Nimic nu e implementat încă.** Zero commit-uri de cod din sesiunea asta.

## Starea repo-ului la 01:05

- `main` = `f109121`. Peste ce era: #68 (textele agentului în RO/EN, `Lang.TFilled`) și #69 (înregistrări Bedrock doar pentru întrebările care citesc; replay-ul cere aceleași coduri).
- **#71, deschis, CI verde, fără review:** „fix: run source on the case agent panel, only CRS Sentinel on /architecture”. Atinge `CaseAgentPanel.razor`, `Architecture.razor` și `Lang.Ro.cs`. Se mergează primul; L1 și L3 pornesc după el.
- **#70, deschis:** teste pentru două probleme de expirare (R5).
- **Verificat în cod pe `f109121`:**
  - `/architecture` e tot template-ul;
  - aprobarea e dublă pe pagina cazului;
  - pe Ioana scrie „guide p. NEVERIFICAT”;
  - actorii apar bruți: `agent:crs-sentinel for user:admin`, `system:seed`;
  - mock-ul pune între ghilimele eticheta butonului (`SentinelMock.cs:56,63,497`), așa că pică check-ul `evidence-quoted`, dar numai pe căile de stare greșită, nu pe traseul Mariei.
- CI-ul rulează și E2E (Playwright, Chromium), deci un PR verde e verificat cap-coadă.

## Decizii luate

1. Filmăm cu UI-ul în engleză (butonul RO/EN; cookie-ul `app_lang`), cu vocea în engleză. Implicit rămâne RO; nu schimbăm default-ul, pentru că E2E se bazează pe el.
2. Design-ul rămâne cel din handoff: un instrument de conformitate calm, cu un singur moment pe pagină. Nu schimbăm tema. „Sexy” înseamnă:
   - un moment-semnătură pe fiecare scenă, declanșat de o acțiune;
   - dovada vizibilă (citatul pe pagina documentului, rândul de lege sub regulă);
   - ecrane care spun povestea de business, cu cifre măsurate live, fără promisiuni.
3. Fără schimbări de backend din frontend. Gap-urile de backend merg la proprietari (planul, secțiunea 7).
4. Nu folosim cifre nemăsurate și nici ce e pe lista „Ce NU spunem niciodată” (`docs/SUBMISSION.md`). Exemple: fără „€500K penalties”, fără „TIN valid”, fără „100% accuracy”.

## Incidente (ca să nu se repete)

- **Discul s-a umplut:** SQLITE_FULL în T3, 96% din 284 GB.
  - Cauza: `/home/wex/.git`, un repo gol (fără commit-uri), avea 130 GiB de `tmp_pack_*` eșuate. Ceva încerca să facă checkpoint pe tot home-ul.
  - Am șters gunoiul și am redenumit repo-ul în `~/.git-home-dezactivat-20261029`. Acum discul e la 47%.
  - **Pe laptopul nou:** home-ul să nu fie repo git (`git -C ~ rev-parse --git-dir` trebuie să dea eroare).
- **Un subagent Haiku a șters fișiere:** când a dat de disc plin, a rulat `rm -rf ~/.cache ~/.npm ~/.nvm` și `rm -f /tmp/*.db ...`. S-au pierdut browserele Playwright, Node 24 cu uneltele npm globale (posibil și `codex`) și `/tmp/test-*.db`. Ce trebuie reținut:
  - în orice prompt de subagent: „never delete or move files outside your scratch dir; on disk-full, stop and report”;
  - pe laptopul acesta, mediul setează `CLAUDE_CODE_SUBAGENT_MODEL=claude-haiku-4-5`, deci workflow-urile rulează pe Haiku dacă nu dai modelul explicit.
- **Cota zilnică Bedrock:** Claude Code rulează pe Bedrock, în `us-west-2`, cu credențialele temporare ale workshop-ului.
  - Șase audituri Opus în paralel au dat `429 Too many tokens per day` (3 din 6 au murit).
  - Cota e pe model. **Aplicația folosește Sonnet 5.5** (principal) și Haiku 5.5 (profilul `extract`) pentru demo-ul live: nu consumați cota asta cu agenți de cod.

## Ce rămâne (în ordine)

1. Merge #71, apoi lane-urile L1–L5 din plan (secțiunea 6).
2. P0-A (~2 h), apoi P0-B: B1 `/architecture`, B2 o singură aprobare, B3 verdictul în cadru, B4 banda „Screened by code”, B5 trace-ul pe înțeles.
3. Gap-urile de backend la proprietari. Cel mai urgent: R1, „depository account (depository account)”, care e pe cadrul din S5.
4. Vocea EN în `docs/PITCH.md` (Persoana 5) și checklist-urile de filmare trecute pe EN (`PITCH.md` §4, `SUBMISSION.md:34`).
5. Repetiția completă în EN la 05:30, demo-ul de rezervă până la 07:30, verificările finale până la 08:00.
6. Munca R4 pe agent: are prompt separat, `PROMPT-R4-AGENT.md`.
