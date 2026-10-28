# Playbook: de la temă la demo în 24h

Cum adaptăm template-ul la orice temă primim la NEXXT AI Hackathon (28–29 oct. 2026). Estimările sunt pentru
echipa de 5, cu template-ul de azi. Împreună cu [`SUBMISSION.md`](SUBMISSION.md) (timeline, video, checklist).

## 1. Ce avem deja (nu se mai scrie mâine)

| Nevoie în orice temă | Piesa din template | Unde |
| --- | --- | --- |
| Agent cu unelte, aprobare umană, istoric | `ToolAgent`, `needsApproval`, `/runs` | `Agents/`, `Features/Assistant` |
| Demo care nu pică pe scenă | live → înregistrare → mock | `AI:Record`, `Recordings/` |
| Răspuns la GDPR | mascare la model și la salvare, retenție, `/privacy`, `/privacy-policy` | `PiiVault`, `RunRedaction`, `RetentionService` |
| Verdicte repetabile („no flaky”) | verificări deterministe cu amprentă | `Features/Checks` |
| Cifre corecte | calcule în cod, verificate cu `expected-number` | `FinanceCalculator` |
| Sistem extern (API partener, chatbot testat, model pe serverul nostru) | ținte cu timeout și replay | `Targets:<nume>`, `TargetClient` |
| Conversații pe mai multe replici (client, escroc, atacator) | `ScenarioAgent` + profil `driver` | `Features/Scenarios`, `/scenarios` |
| Documente (PDF, poze) | extragere text, căutare în pasaje, imagini la modele vision | `DocumentText`, `DocumentSearch` |
| Date de test fără persoane reale | CNP/IBAN/card/CUI valide, deterministe | `SyntheticData`, `/api/synthetic/*` |
| Livrabile | export test xUnit/JSON, raport Markdown, cu aprobare | `Features/Exports`, `/exports` |
| Produs „real” | temă întunecată, sidebar + panou de conținut, PWA | `AppTheme`, `wwwroot/app.css` |
| Prima impresie | pagina principală (titlu, imaginea aplicației, secțiuni, „Cum funcționează”), textele în ambele limbi | `SiteConfig.Landing`, `wwwroot/landing.css` |
| Pagini publice | termeni, politică de confidențialitate, date și confidențialitate, contact, în afara aplicației | `PublicLayout`, `SiteConfig.Legal` |
| Română + engleză | română implicit, buton RO/EN; un test pică dacă lipsește o traducere | `@T("…")`, `Services/Lang.Ro.cs` |
| Integrare | API pentru fiecare agent, `/api-docs` | `Api/` |

## 2. Ce lipsește (evaluare sinceră)

- **Deploy-ul public e pe planul gratuit Render** (0.15 CPU, adoarme după ~15 min fără trafic). Bun ca link pentru juriu;
  demo-ul de pe scenă și video-ul le facem de pe laptop (secțiunea 6).
- **Identitatea produsului:** `SiteConfig` (nume, landing, `Legal`) are încă valori de completat. Pe scenă ar apărea „Acme”.
  Textele paginii principale sunt de două ori (`LandingRo` și `LandingEn`), inclusiv exemplul din imaginea aplicației (`Preview`).
- **Feature-ul exemplu `Items`** și pagina **Calculators** nu țin de nicio temă: se ascund din `SiteConfig.Nav` sau se înlocuiesc.
- **Conținut încă în engleză:** datele de exemplu, sugestiile din chat-ul agenților, explicațiile verificărilor și mesajele
  de validare. Le rescriem oricum pentru temă: direct în română (mock-urile înțeleg ambele limbi cu `TextMatch`).
- **Fără cifre pe pagina principală:** le-am scos pentru că erau inventate. Dacă vrem cifre, le citim live din aplicație
  (rulări, verificări picate, exporturi aprobate), nu le scriem de mână.
- **Fără graf** (lanțuri de proprietate, arbori de atac): necesar doar pentru Raiffeisen Ops.
- **Fără migrații:** o bază persistentă se resetează când schimbăm entitățile (în Development se face singur).
- **Login demo** (3 utilizatori): suficient pentru hackathon, de spus deschis dacă juriul întreabă.
- **Textele legale** sunt un punct de plecare, nerevizuite de un jurist.

## 3. Primele 2 ore, indiferent de temă

| Când | Ce | Cine |
| --- | --- | --- |
| 0:00–0:45 | Research pe temă, pitch-ul într-o propoziție, „cine plătește și cât”, scenariul de demo în 3–5 pași | toți |
| 0:45–1:30 | `SiteConfig`: nume, `Legal`, `Nav` (ascundem Items și Calculators), `LandingRo` + `LandingEn` cu exemplul din `Preview` | frontend + pitch |
| 0:45–1:30 | Entitățile cu `dotnet run --project tools/NewFeature -- <Nume>`, seed din `SyntheticData` | dev .NET |
| 0:45–1:30 | Ținte și chei în `appsettings.Local.json`, **Settings → Test connections** | securitate |
| 1:30–2:00 | Primul push pe `main`, deploy verificat pe URL-ul public, link la mentori | oricine |

**Regula pentru orice text nou din interfață:** se scrie în engleză, `@T("Save")` (nu `"Salvează"` direct în pagină), iar
româna se adaugă în `Services/Lang.Ro.cs`: `["Save"] = "Salvează"`. Fără ea, `LangTests` pică în CI și ne blochează merge-ul. Conținutul lung din `SiteConfig`
și textele legale: `Lang.Pick(ro: "...", en: "...")`.

## 4. Pe temă: ce construim, cât durează

Orele sunt de lucru efectiv (în paralel), fără primele 2 ore și fără video.

### QA DNA – AI Agent Arena (prima opțiune) · ~10–12h

| Pas | Piese folosite | Estimare |
| --- | --- | --- |
| Biblioteca de atacuri: categorii (date personale, MiFID, prompt injection, mai multe replici), câte 3–5 obiective fiecare | `ScenarioAgent.Suggestions` / seed | 1.5h |
| `ArenaScenario : ScenarioAgent`: atacatorul pe profilul `driver` (modelul de pe server), ținta = asistentul open-source (`Targets:target`) | `ScenarioAgent`, `TargetClient` | 2h |
| **Canary** în system prompt-ul țintei: check-ul `prompt-leak` există deja, doar îl configurăm (`Checks:Canaries`) și îl listăm în `Checks` | `prompt-leak` din `BuiltInChecks.cs` | 0.25h |
| Reguli MiFID în plus (`Checks:PhraseRules`) | config | 0.5h |
| Dashboard: atacuri rulate, % blocate, teste generate și aprobate | `StatCard`, `RunStore`, `RunExportService` | 2h |
| Ciclul complet: atac reușit → export xUnit → aprobare → job CI care rulează testele aprobate pe țintă | `/exports`, `GET /api/exports?status=Approved` | 2h |
| Înregistrări pentru demo, lustruire | `AI:Record` | 2h |

Argumentul de business: fiecare atac reușit devine un **test facturabil** (ei facturează pe test creat și întreținut).
Ținta e un asistent open-source, nu unul scris de noi ca să fie vulnerabil (riscul 2).

### Raiffeisen – Operational Efficiency (KYC firme) · ~12–14h (cea mai mare)

| Pas | Piese folosite | Estimare |
| --- | --- | --- |
| Entități: `Company`, `Shareholding` (cine deține cât din cine), `KycDossier` | `NewFeature` | 1h |
| Upload certificat ONRC (PDF) → extragere structurată în record tipizat, cu mock | `DocumentText`, `StructuredAi.GenerateAsync<T>` | 2h |
| Diferențe certificat nou vs dosar vechi: **în cod**, nu în model | serviciu nou + teste | 1.5h |
| Beneficiarul real: produsul procentelor pe fiecare lanț, suma pe persoană, prag 25%, cicluri | calculator nou cu teste (ca `FinanceCalculator`) | 2h |
| Graful de proprietate live (Cytoscape din cdnjs, prin JS interop) | componentă nouă | 3h |
| Agentul: extrage → compară → calculează → propune actualizarea dosarului (cu aprobare) | `ToolAgent`, `needsApproval` | 1.5h |
| Raport KYC exportabil | `IRunExporter` nou | 1h |

Riscul principal e graful: dacă nu e gata în 3h, arătăm lanțurile ca tabel indentat.

### Raiffeisen – Scaling & Growth (anti-țeapă înainte de transfer) · ~9–10h

| Pas | Piese folosite | Estimare |
| --- | --- | --- |
| Entități: `Transfer`, `Payee`, istoric | `NewFeature`, `SyntheticData` | 1h |
| Semnale de țeapă în cod: IBAN nou, sumă neobișnuită, urgență, „cont sigur”, „angajat BNR” | `Check` / `PhraseRule`, `Iban` | 2h |
| Copilotul: evaluează transferul, explică riscul, cere confirmare (aprobare) | `ToolAgent`, `needsApproval` | 2h |
| Demo: **escroc simulat contra copilot** pe mai multe replici | `ScenarioAgent` cu țintă `Agent` | 1.5h |
| Ecranul de transfer (mobil, 390px) | MudBlazor, PWA | 2h |

### Stripe – Financial Confidence (Pilonul III prin angajator) · ~9–10h

| Pas | Piese folosite | Estimare |
| --- | --- | --- |
| **De verificat întâi legea în vigoare** (plafoanele de deducere de 400 € + 400 €/an din research) | – | 0.5h |
| Calcule de deducere și economie fiscală în `FinanceCalculator`, cu teste | `FinanceCalculator`, `expected-number` | 2h |
| Entități: `Employee`, `Contribution`, `Plan` | `NewFeature` | 1h |
| Plăți în **modul test Stripe** (chei `sk_test_…`, niciodată live) | ținta `Http` sau SDK-ul Stripe | 2.5h |
| Agentul: explică angajatului, simulează, propune înscrierea (aprobare) | `ToolAgent`, `RetirementPlan` | 2h |

### Orice altă temă

1. Ce date? → entități cu `NewFeature`, seed cu `SyntheticData`.
2. Ce decide agentul și ce decide omul? → unelte, iar ce are efecte în afara aplicației primește `needsApproval`.
3. Ce trebuie să fie mereu corect? → calcul în cod și verificare (`Check`), nu în model.
4. Ce sistem extern? → `Targets:<nume>`.
5. Există o conversație de simulat? → `ScenarioAgent`.
6. Ce livrabil rămâne după? → un `IRunExporter`.

## 5. Împărțirea echipei (5 oameni)

| Rol | Răspunde de |
| --- | --- |
| Securitate + dev .NET (în pereche, riscul 5) | domeniul: entități, calcule, verificări, teste |
| Dev .NET 2 | agentul, uneltele, scenariile, înregistrările |
| Frontend | `SiteConfig`, pagini, dashboard, mobil, proiector |
| Pitch | research, ROI, deck, scenariul video, montaj, răspunsuri la întrebări |

Regula: `main` = producție. Lucrăm pe branch-uri, facem merge doar cu CI verde, iar înainte de video intră code freeze.

## 6. De făcut azi (deploy și verificări)

Rămânem pe **planul gratuit Render**, fără costuri. Consecințe: serviciul adoarme fără trafic, e lent dacă mulți îl
folosesc odată și fiecare push pe `main` îl repornește (și rupe conexiunile deschise).

1. **Render** e creat (`https://hackathon-starter-sjmj.onrender.com`). Completăm în *Environment* variabilele cerute
   (`AI__…`, `AI__Profiles__driver__…`, `Targets__target__…`, `Security__AccessCode`). Verificăm `/health` → `ok`.
2. **Keep-awake:** GitHub → *Settings → Secrets and variables → Actions → Variables* → **Repository variable**
   `APP_URL` = URL-ul Render, fără `/` la final (setat; rularea manuală a ieșit verde).
   **Obligatoriu și UptimeRobot** (gratuit, HTTP, la 5 min, pe `/health`): cron-ul GitHub n-a pornit nicio rulare
   programată pe 27 oct., deci singur nu ține serviciul treaz. Le oprim pe amândouă după finală.
3. Serverul cu modelul open-source **accesibil din internet** (IP public, Tailscale sau tunel), pentru că Render și sala
   de hackathon nu sunt în rețeaua noastră. Verificăm cu **Settings → Test connections** pe URL-ul public, nu pe localhost.
4. **Laptopul de demo:** `git pull`, `dotnet run --project src/HackathonStarter`. Rulăm o dată cu Wi-Fi-ul oprit: trebuie să
   cadă singur pe înregistrare / mock. Pe scenă și în video prezentăm de pe laptop; Render rămâne linkul pentru juriu.
5. Toți: `git pull`, `dotnet watch --project src/HackathonStarter`, aplicația pornește local.
6. **După video, fără push pe `main`** până după finală (repornește Render în timpul jurizării).
