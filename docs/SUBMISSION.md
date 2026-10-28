# Predarea: DosarViu (Raiffeisen, Operational Efficiency)

Ce primește juriul: **codul sursă**, o **demonstrație practică a agentului** și un **video de maximum 3 minute**
(problema, ce e unic, cum funcționează arhitectura). Predare **joi, 29 oct., ora 11:00**; noi predăm până la **10:30**.
Prezentare pe scenă doar pentru câștigătorul challenge-ului: o pregătim atunci, nu acum.

## Video: 3 minute, 4 părți

Scenariul de demo e cel din [`PLAYBOOK.md`](PLAYBOOK.md#2-ce-construim-și-ce-nu) (Alfa Logistic, Maria 44%, Elena exact 25%).

| Timp | Parte | Ce se vede | Ce se spune (de scris cuvânt cu cuvânt) |
| --- | --- | --- | --- |
| 0:00–0:30 | **Problema** | Un certificat ONRC lângă un dosar, cu procente scrise de mână | O revizuire KYC de firmă costă băncile 1.500–3.000 $ și durează săptămâni (Fenergo, 2023). Partea grea: lanțurile de proprietate și pragul de 25%. |
| 0:30–1:00 | **Ce e unic** | Pagina principală, secțiunea „Beneficiarul real, calculat” | Calculul e cod testat, nu model. Pragul e o setare: peste 25% azi, 25% sau mai mult din iulie 2027. Cazul la limită ajunge la om. |
| 1:00–2:15 | **Demo** | `/kyc/…`: încărcare certificat → pașii agentului → diferențe → lanțuri → Elena marcată → **Aprobă** | Agentul cere singur certificatele firmelor intermediare. Analistul aprobă actualizarea; totul rămâne în `/runs`. |
| 2:15–3:00 | **Arhitectură + GDPR + bani** | `/architecture`, apoi `/privacy` | Model pe serverul băncii (compatibil OpenAI), date personale mascate, verificări deterministe. Cine plătește: banca, per revizuire; economia: formula din PLAYBOOK, cu cifrele confirmate de mentor. |

Sfaturi:
- **Fără eticheta „Simulare” în video.** Filmăm cu modelul real conectat (Settings → Testează conexiunile pe calculatorul
  de filmare) sau cu rulări înregistrate live (`AI:Record = true`, apoi `AgentMode = Replay`, eticheta devine „Înregistrare”).
- Interfața în română (implicit). Zoom 125% în browser, fără bara de bookmark-uri, date sintetice realiste.
- Vocea separat de ecran. Export 1080p. Durata: **≤ 3:00**.
- Spunem o dată, clar, că pornim de la un template pregătit înainte (e în istoricul repo-ului).

## Program (de la sfârșit spre început)

| Când | Ce |
| --- | --- |
| Joi 07:00 | **Code freeze**: doar reparații pe calea de demo. |
| 07:00–09:30 | Filmare, mai multe duble; montaj; voce. |
| 09:30–10:30 | `dotnet build` (0 warnings) + `dotnet test`, deploy verificat, link în README, video încărcat. |
| **10:30** | **Predare.** Nu în ultimul minut. |

## Checklist demo

- [ ] Scenariul (3–5 pași) scris și repetat
- [ ] Rulările pentru fiecare pas înregistrate (`src/HackathonStarter/Recordings/*.json` în repo)
- [ ] Datele resetate înainte de filmare (Settings → Resetează datele demo)
- [ ] Paginile care nu țin de KYC ascunse din `SiteConfig.Nav` (Arena, Scenarii, Calculatoare)
- [ ] Nicio dată reală: firme, persoane și certificate sintetice

## Checklist cod

- [ ] `dotnet build` cu 0 warnings, `dotnet test` trece, aplicația pornește **fără** `appsettings.Local.json`
- [ ] README: ce face, cum se pornește, arhitectura, ce e AI și ce e cod
- [ ] **Niciun secret în repo (e public):** `git log -p | grep -iE "apikey|sk-|password"` nu găsește nimic real
- [ ] URL-ul Render în README

## Întrebări probabile ale juriului (cu răspunsul)

| Întrebare | Răspuns scurt |
| --- | --- |
| Ce face agentul singur și unde decide omul? | Extrage, compară, calculează și propune. Actualizarea dosarului și orice caz la limită le decide analistul. |
| Ce se întâmplă dacă modelul greșește un procent? | Fiecare câmp are pagina-sursă; diferențele și beneficiarii se calculează în cod, cu teste; analistul aprobă. |
| De ce nu ChatGPT? | Pentru că un model poate greși o înmulțire, iar aici o greșeală înseamnă un beneficiar real ratat. Noi lăsăm calculul în cod și arătăm de unde vine fiecare cifră. |
| Merge cu date reale, sensibile? | Da: model on-premise, mascare înainte de model, temei legal (Legea 129/2019), retenție configurabilă. |
| Ce e real și ce e simulat? | Calculul, diff-ul, verificările și fluxul de aprobare sunt reale. Certificatele și firmele sunt sintetice; integrarea cu ONRC nu e făcută. |
| Cine plătește și cât? | Banca, per revizuire sau per analist; economia = revizuiri × minute economisite × cost/minut (cifrele mentorului). |
| Ce e cu 25% fix? | Azi nu e beneficiar real (peste 25%), din 10 iulie 2027 este (25% sau mai mult). E o setare, iar cazul îl vede un om. |
