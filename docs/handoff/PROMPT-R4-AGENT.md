# Promptul pentru chatul R4 (agentul CRS Sentinel și orchestrarea lui)

Copiază tot blocul de mai jos într-un chat nou, deschis în clona repo-ului. Rulează în paralel cu chatul de frontend;
cele două își împart fișierele (secțiunea 1).

````text
Ești agent-engineer-ul echipei (vezi docs/agents/agent-engineer.md și integration-engineer.md) și lucrezi cu mine,
WEX75, proprietarul rolului R4: agentul CRS Sentinel, tool loop-ul orchestrat de cod, mock-ul, înregistrările și
Bedrock. Îmi scrii în română, scurt. Termenul: code freeze joi 29.10.2026 la 08:00 (ora României), filmare 08:00–09:45,
video în ENGLEZĂ. Misiunea ta: S3 și S4 (Maria, rularea 1 și rularea 2) merg impecabil în engleză, pe Bedrock live și
pe mock. Agentul nu decide nimic, citatele trec verificarea codului, aprobările se opresc unde trebuie, iar nimic din
ce se vede pe ecran nu contrazice „Code decides, AI explains, a person approves”.

## 0. Primele 10 minute (fără cod)

1. Pe un laptop nou: `tools/setup-dev.sh`, apoi `export PATH=$HOME/.dotnet:$PATH`. Credențialele AWS sunt temporare,
   din consola workshop-ului (rolul WSParticipantRole), în variabilele de mediu `AWS_*`, regiunea `us-west-2`
   (eu-central-1 e blocat). Nu le scrii niciodată în repo (e public).
2. Citește: `docs/roluri/R4-agent-si-ai.md`, `docs/PLAN.md` §6.3, §13 și §14, `docs/PITCH.md` S3, S4 și §4 (checklist-ul
   de filmare), apoi `git show origin/r4/handoff-noapte:docs/handoff/CONTEXT-SESIUNE.md` și secțiunile 3, 7 și 8
   din `git show origin/r4/handoff-noapte:docs/handoff/PLAN-FRONTEND.md`.
3. Starea reală: `git log --oneline -10 origin/main`, `gh pr list --state open`. Pe `f109121` erau deja #68 (agentul
   răspunde în limba mesajului; `Lang.TFilled`), #69 (înregistrări doar pentru întrebările care citesc; replay doar cu
   aceleași coduri) și #54 (Maria cap-coadă pe Bedrock ca test `CRS_LIVE`).
4. Dacă „orchestrarea de agenți” înseamnă pentru mine altceva decât agentul CRS Sentinel (de exemplu subagenții
   echipei din `docs/agents` și `tools/agents/sync.py`), întreabă-mă o dată, înainte să începi.

## 1. Fișierele tale și ale altora

- **Ale tale:** `Features/Sentinel/**`, fișiere noi în `Features/Checks/`, `Recordings/**`, `Services/Ai.cs`,
  `Services/Lang.Ro.Sentinel.cs`, secțiunea `AI` din `appsettings*.json` și testele din `tests/**/Crs/Sentinel/`.
- **Ale chatului de frontend, pe care nu le atingi:** `Components/**` (inclusiv `AgentTrace.razor` și
  `CaseAgentPanel.razor`), `wwwroot/**`, `Lang.Ro.CrsUi.cs`, `SiteConfig.cs`. Dacă frontend-ul are nevoie de un câmp
  în output-ul unei unelte, îl adaugi tu, compatibil înapoi, și îi spui exact numele.
- **Ale altora:** `Features/Cases` (R2), `Features/Rules` (R1), `Features/Tin`, `Features/Shield`, `Features/Audit`
  (R3), `Features/Evaluation` și seed-ul (R5). Pe ele nu schimbi nimic fără OK-ul proprietarului. Îi scrii gap-ul cu cea
  mai mică schimbare posibilă.

## 2. Ce faci, în ordinea asta

**R4-1. Rularea live în ENGLEZĂ, verificată pe `main`-ul de acum.**
- `SentinelLiveTests.Maria_on_the_live_model_stops_at_both_approvals_and_closes_with_the_proof` rulează doar în
  `ro-RO` (`new UiLanguage("ro-RO")`).
- Adaugă varianta EN: același traseu, în `en-US`, cu mesajele din `SentinelActions` în EN. Verifică:
  - `RunSource.Live`, oprirea la `sendClientRequest` și apoi la `closeCase`;
  - `ChecksPass` (pii-leak și evidence-quoted), dovada `CRS-IND-PHONE` verificată, închiderea cu dovada;
  - textul final în engleză (o aserțiune simplă, fără cuvinte românești de tipul „Cazul”, „termen”);
  - scrisoarea către client rămâne în română (`language = "ro"`).
- Rulezi: `CRS_LIVE=1 dotnet test tests/HackathonStarter.Tests --filter "FullyQualifiedName~SentinelLiveTests"`.
- Notezi tokenii și durata pe fiecare rulare (`Print` le scrie). **Ai grijă la cotă:** contul are o cotă zilnică pe
  model, iar Sonnet 5.5 e chiar modelul demo-ului. Rulezi live de cel mult 2–3 ori în total și îmi spui câte duble mai
  încap.

**R4-2. Mock-ul: fără ghilimele în jurul etichetelor de butoane.**
- `SentinelMock.cs:56`, `:63` și `Next()` (`:497`) scriu „{SentinelActions.VerifyLabel}”, respectiv `"Check the answer"`.
  `EvidenceQuotedCheck` (`Features/Checks/CrsChecks.cs`, regex-ul `Quotation`, de minimum 8 caractere) le ia drept
  citate neverificate, așa că rularea arată „Checks: 1 of 2 failed. Quotes are verified evidence”.
- Scoate ghilimelele, scrie eticheta simplu (sau cu `«»`, dacă regex-ul nu le prinde; verifică) și adaugă un test RO și
  EN: „Analyze” pe un caz deja trimis și „Verify” pe un caz Open trec ambele check-uri.

**R4-3. Cine semnează pașii agentului (cu R2).**
- `CaseService.Actor(user)` pune `user:admin` pe ciorna, dovezile, semnalele, răspunsul și închiderea făcute în
  rularea agentului (`CaseService.cs:933`, și `:381`, `:421`, `:456`, `:566`, `:654`). Pe ecran ciorna apare
  „Drafted by Ana Admin”, iar avertismentul de patru ochi devine fals.
- Propune-i lui R2 cea mai mică schimbare: un parametru opțional `string? actor = null` pe
  `DraftRequestAsync/AddEvidenceAsync/AddSignalAsync/RegisterResponseAsync`. `SentinelTools` trimite
  `SentinelTools.Actor(user)`, adică `agent:crs-sentinel for user:<id>`.
- Faci partea din `Features/Sentinel` doar după OK-ul lui. Frontend-ul afișează deja „CRS Sentinel for Ana Admin” (A2).

**R4-4. TIN on Europa pe traseul Mariei (cu R3).**
- `checkTin` dă sursa `TinOnEuropaLive`, `Replay` sau `LocalRules`. Fără înregistrare (`Recordings/targets/` e gol) și
  cu un timeout de 5 s, se întoarce tăcut pe regulile locale.
- În Development, cu `AI:Record = true`, rulează o dată verificarea pentru `DE 86095174223` și confirmă că EC răspunde
  „validStructure” și „validSyntax”. Altfel rularea 2 spune „format invalid”. Salvează înregistrarea și fă commit pe ea.
- Spune-i lui R3 că `ConnectionCheck` trimite „Hello, are you there?” la ținta SOAP; ar trebui să cheme
  `TinOnEuropaClient.CheckAsync`.

**R4-5. Configurația de filmare, scrisă pentru Persoana 5** (în PR-ul tău, ca secțiune nouă în `docs/PITCH.md` §4,
dacă Persoana 5 e de acord, sau în body-ul PR-ului):
- **Live:** `appsettings.Local.json` cu `Provider = Bedrock`, `Region = us-west-2`,
  `Model = us.anthropic.claude-sonnet-5-5`, profilul `extract` pe Haiku 5.5, `AgentMode = Live`, `Record = false`.
  Înainte: Setări, Conexiuni, „Testează conexiunile”.
- **Mock:** `Provider = None`, `AgentMode = Auto`, iar `Recordings/crs-sentinel.json` mutat deoparte (altfel o
  înregistrare asemănătoare are prioritate).
- **Niciodată `AgentMode = Replay`** pe pașii care scriu.
- Eticheta rulării trebuie să fie Live sau Simulation, niciodată Recording.
- Nu reporniți aplicația între propunere și aprobare.

**R4-6. Date pentru UI, fără schimbări de comportament.** Frontend-ul face un trace pe înțelesul omului (B5),
extragerea ca tabel (C4) și vederea „What a model receives” (C8). Verifică că output-urile uneltelor au deja ce le
trebuie: `recordEvidence` cu verified, page și linkedRule; `extractSelfCertification` cu Fields
(field/value/quote/page/verified), Jurisdictions cu TIN și Source; `checkTin` cu Source; `MaskedCount` pe rulare.
Le trimiți chatului de frontend lista exactă, cu numele câmpurilor. Câmpuri lipsă adaugi doar compatibil înapoi,
cu teste.

**Ramuri vechi:** `r4/chat-shield` (5 commit-uri, fără PR), `r4/agent-draft` și `r4/maria-flow` sunt depășite. Nu le
mergezi în noaptea asta fără să-mi spui de ce ar conta pentru video.

## 3. Reguli

Restul e în AGENTS.md.

- Agentul nu decide: verdictul vine din reguli (cod), citatele se verifică în cod, acțiunile care schimbă date cer
  aprobarea unui om (`sendClientRequest`, `closeCase`). „format valid”, niciodată „TIN valid”. Fără emoji. Doar date
  sintetice. Timpul CRS vine doar din `ISimClock`.
- Nu schimbi mesajele fixe din `SentinelActions` și nici etichetele butoanelor fără să verifici E2E
  (`CaseAgentTests.cs`) și `docs/PITCH.md`.
- Git: branch `r4/<subiect>` din `origin/main`, PR mic. Commit pe o linie, în engleză, `type: what`, cu mine ca autor.
  Fără Co-Authored-By sau alte linii de asistent. Body-ul PR-ului în română. Merge doar cu CI verde și aprobarea unui
  om.
- Verificare înainte de PR: `dotnet build -warnaserror` (0 warnings), `dotnet test tests/HackathonStarter.Tests`, iar
  pentru orice atinge pagina cazului, E2E (CI-ul le rulează oricum).
- Siguranță: nu ștergi și nu muți fișiere în afara worktree-ului tău. La disc plin te oprești și îmi spui. Fără
  fan-out mare de subagenți (cota Bedrock). Subagenților le dai modelul explicit și regula despre ștergere.

## 4. Programul și raportul

- **03:00:** R4-1 și R4-2 în PR.
- **04:30:** R4-4 și R4-5 gata; R4-3 cel puțin propus lui R2.
- **05:30:** ești disponibil pentru repetiția completă în EN. Live, dacă cota permite; altfel mock.
- **08:00:** freeze.

Raportezi în română: ce s-a schimbat (cu link-urile PR-urilor), output-ul real al testelor live (sursa, statusul,
check-urile, tokenii), ce a rămas. Lucrurile pe care doar un om le poate face (credențialele AWS, mesajele către R2
și R3, aprobarea PR-urilor) le treci la final, sub titlul „De făcut de tine”.
````
