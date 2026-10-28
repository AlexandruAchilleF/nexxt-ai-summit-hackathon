# Predarea: CRS Sentinel (Raiffeisen, AI for Operational Efficiency)

Ce primește juriul: **codul sursă**, o **demonstrație practică a agentului** (scenariu cap-coadă) și un **video de maximum
3 minute** (problema, ce e unic, cum funcționează arhitectura). Predare **joi, 29 oct., ora 11:00**; noi predăm până la
**10:30**. Prezentare pe scenă doar pentru câștigătorul challenge-ului: o pregătim atunci, nu acum.

Planul: [`PLAN.md`](PLAN.md) și [`PLAYBOOK.md`](PLAYBOOK.md). Cazurile-etalon: [`GOLDEN-CASES.md`](GOLDEN-CASES.md).
Faptele și paginile din ghid: [`RESEARCH-CRS.md`](RESEARCH-CRS.md).

## Video: 3 minute, 6 părți

Textul vocii, cuvânt cu cuvânt, cu numărul de cuvinte, ce se vede pe fiecare segment, modul de filmare, sursele cifrelor și
checklist-ul de filmare: [`PITCH.md`](PITCH.md). E singurul text al vocii; aici rămân doar timpii și sfaturile. Timpii
de mai jos sunt cei din `PITCH.md` §1, calculați pe cuvintele rostite (377, cu cifrele citite în litere, ≈ 2:54).

| Timp | Parte | Segmentele din `PITCH.md` | Ce se vede |
| --- | --- | --- | --- |
| 0:00–0:32 | **Problema** | S1 | Titlul, apoi `/dashboard`: termenele care expiră |
| 0:32–1:24 | **Maria, cap-coadă** | S2–S4 | `/changes/new` → `/cases/{code}`: Maria Popescu (`CL-0001`) se mută la München → caz deschis, termen de 90 de zile → dovadă cu citat verificat, salariul german ca semnal, fără alertă → cererea aprobată de operator → data simulată +85 de zile: memento și escaladare → autocertificare nouă, format valid → caz închis cu dovada confirmării |
| 1:24–1:58 | **Capcane și scut** | S5–S6 | `/changes/new`: Andrei Pop (`CL-0013`), Radu Ionescu (`CL-0005`, cazul 5a lângă 5c), Ioana Mureșan (`CL-0015`); `/clients/CL-0010`: Victor Neagu, textul alb ascuns oprit de scut |
| 1:58–2:20 | **Arhitectura** | S7 | `/architecture`, apoi `/audit` cu „Tamper” |
| 2:20–2:46 | **Testabil** | S8 | `/metrics`: cazurile-etalon, așteptat vs. obținut |
| 2:46–3:00 | **Fraza de final** | S9 | Logo + URL |

Sfaturi:
- **Filmăm live (Bedrock) sau pe mock, niciodată în `AgentMode = Replay` pe pașii care modifică date.** Replay-ul nu
  rulează uneltele: în chat apar pașii, dar nu se salvează nicio dovadă și cazul nu își schimbă starea; o aprobare reluată,
  dacă e aprobată, chiar rulează, cu argumentele mascate din înregistrare (PLAN §14). Modelul real se
  testează înainte (Setări → Conexiuni → „Testează conexiunile”, pe calculatorul de filmare); dacă nu e disponibil, filmăm
  pe mock, care cheamă uneltele reale (eticheta „Simulare” rămâne vizibilă). Înregistrările (`AI:Record = true`) sunt doar
  pentru întrebările care citesc (ex. „rezumă cazul”); fără model, o înregistrare asemănătoare are prioritate față de mock,
  deci pentru dubla pe mock o mutăm deoparte (`PITCH.md` §4). Replay-ul `TargetClient` pentru TIN on Europa e altceva
  și rămâne.
- Interfața în română (implicit). Zoom 125% în browser, fără bara de bookmark-uri, date sintetice realiste.
- Vocea separat de ecran; montajul taie așteptarea modelului. Export 1080p. Durata: **≤ 3:00**.
- Fraza despre template-ul pregătit înainte e în voce (S7) și în README §13 (template-ul e în istoricul repo-ului).

## Program (de la sfârșit spre început)

| Când | Ce |
| --- | --- |
| Joi 08:00 | **Code freeze**: doar reparații pe calea de demo. Demo-ul de rezervă e deja înregistrat. |
| 08:00–09:45 | Filmare, mai multe duble; montaj; voce. |
| 09:45–10:30 | `dotnet build` (0 warnings) + `dotnet test`, deploy verificat, link în README, video încărcat. |
| **10:30** | **Predare.** Nu în ultimul minut. |

## Checklist demo

- [ ] Scenariul (Maria + 3 capcane + scut + tamper + metrici) scris cuvânt cu cuvânt în [`PITCH.md`](PITCH.md) și repetat
- [ ] Mock-ul parcurge tot traseul, cu uneltele reale; înregistrările (`src/HackathonStarter/Recordings/*.json` în repo)
      doar pentru întrebările care nu modifică date
- [ ] Data simulată resetată și datele demo resetate înainte de filmare (repornire cu `Database:RecreateOnStartup = true`;
      Setări → „Resetează datele demo” nu reface încă datele CRS; checklist-ul complet în `PITCH.md` §4)
- [ ] `/metrics` complet verde pe cazurile-etalon, pe aceeași versiune care e pe Render
- [ ] Paginile care nu țin de CRS ascunse din `SiteConfig.Nav` (Arena, Scenarii, Calculatoare)
- [ ] Nicio dată reală: clienți, documente și TIN-uri sintetice

## Checklist cod

- [ ] `dotnet build` cu 0 warnings, `dotnet test` trece, aplicația pornește **fără** `appsettings.Local.json` și fără AWS
- [ ] README: ce face, cum se pornește, arhitectura, ce e AI și ce e cod, domeniul MVP-ului, interpretările de design
- [ ] **Niciun secret în repo (e public):** `git log -p | grep -iE "apikey|sk-|password|aws_secret"` nu găsește nimic real
- [ ] URL-ul Render în README

## Ce NU spunem niciodată

| Nu | Ci |
| --- | --- |
| „Băncile au primit amenzi de sute de mii de euro.” | „Riscul e pe fiecare cont.” Suma de 102.000 lei (ANAF, 2024) doar la întrebări, cu contextul ei (`PITCH.md` §6). |
| „Raiffeisen are probleme cu CRS.” | Cifrele ANAF sunt pe tot sectorul. |
| „Nimeni n-a mai făcut asta.” | „Metodă cunoscută, aplicată pe fluxul de după onboarding, cu regulile din ghidul ANAF și omul la decizie.” |
| „Venitul din străinătate înseamnă rezidență acolo.” | Nu e indiciu oficial; e semnal contextual, fără efect. |
| „TIN valid.” | „Format valid.” |
| „Orice actualizare pornește un termen.” | „Fiecare actualizare trebuie evaluată; doar cele relevante fiscal pornesc termenul.” |
| „Termenul e mereu 90 de zile.” | „90 de zile la conturile noi; la un cont preexistent documentat prin adresa de rezidență, cea mai târzie dintre 31 decembrie și 90 de zile de la aflarea schimbării (Codul de procedură fiscală, Anexa nr. 2 pct. 1; ghidul ANAF, p. 29, scrie «sau»).” La indiciile din căutarea electronică, „cea mai târzie” e interpretarea noastră (`PITCH.md` §6). |
| Reținerea FATCA de 30% în pitch | Doar la întrebări: „România are acord FATCA Model 1: o bancă conformă nu suportă reținerea de 30%. Reținerea apare doar dacă o neconformare gravă nu e rezolvată în 18 luni de la notificare. Până atunci se aplică amenzile din legea română.” (acordul, art. 4(1) și 5(2)(b)) |

## Întrebări probabile ale juriului (cu răspunsul)

GDPR, FATCA, amenzile, cifrele ANAF și termenul F3000, cu sursele: [`PITCH.md`](PITCH.md) §6. Rândurile comune au aceeași formulare.

| Întrebare | Răspuns scurt |
| --- | --- |
| Ce face agentul singur și unde decide omul? | Alege ce dovezi caută, le citează, scrie cererea și evaluează răspunsul. Codul decide dacă există un indiciu și calculează termenul; omul aprobă cererea și închiderea. |
| Ce se întâmplă dacă modelul greșește sau inventează un citat? | Citatul e verificat în cod față de document și respins dacă nu există. Verdictul vine din reguli, nu din model. |
| De ce nu ChatGPT? | Un chatbot dă o părere și poate răspunde diferit la același dosar. Aici regulile sunt cod (fiecare indiciu cu pagina din ghid), iar termenul e urmărit pe fiecare caz. |
| Raiffeisen nu are deja asta? | Semnalul la modificarea datelor poate exista. Noi acoperim ce urmează după el: dovezi, cerere, răspuns, termen, închidere cu dovadă. `[răspunsul mentorului]` |
| De ce ordinul permanent către Germania nu e indiciu, dar cel către SUA este? | La CRS, ordinele permanente din conturile de depozit sunt excluse (ghidul, p. 29); la FATCA excepția nu există (p. 31). |
| De ce termenul Mariei e 90 de zile? | Contul ei e cont nou (deschis în 2019): 90 de zile de la data la care banca știe sau are motive să știe de schimbare (ghidul, p. 35–36). La un cont preexistent aplicăm cea mai târzie dintre 31.12 și +90 de zile: lege la contul documentat prin adresa de rezidență (Codul de procedură fiscală, Anexa nr. 2 pct. 1); interpretarea noastră la indiciile din căutarea electronică (Anexa nr. 1, secț. III, subsecț. B, pct. 16 spune doar „ori”), aliniată la Comentariul OCDE (Secțiunea III, pct. 13 și 16); ghidul (p. 29) spune „sau”. |
| De ce folosiți indiciile la conturile noi? | La conturile noi, ghidul cere testul de rezonabilitate al autocertificării (p. 33); folosim indiciile drept criteriile prin care autocertificarea e contrazisă. E interpretarea noastră de design (README §5). |
| Cum verificăm că merge? | `/metrics` rulează toate cazurile-etalon cu rezultatul scris înainte; `/changes/new` răspunde la „și dacă…?” pe loc; `dotnet test` rulează aceleași cazuri. |
| Merge cu date reale, sensibile? | Da, cu condiții: CNP, IBAN, card, e-mail și telefoanele (românești și străine cu indicativul țării) mascate înainte de model (numele, adresele și telefoanele străine fără indicativ nu, azi), model care poate rula la bancă, temei legal (obligația de raportare din Codul de procedură fiscală, art. 62 alin. (1); GDPR art. 6 alin. (1) lit. c); detaliile în `PITCH.md` §6), retenție configurabilă. |
| Ce e real și ce e simulat? | Reale: regulile, termenele, scutul, verificarea formatului TIN (local și prin TIN on Europa, live sau replay `TargetClient`; TIN on Europa verifică doar structura și sintaxa), bucla cu aprobări. Sintetice: clienții, documentele, răspunsurile clientului. Nefăcute: integrarea cu core banking, trimiterea mesajelor. |
| Cine plătește și cât? | Banca, per caz sau licență; economia = cazuri × minute economisite × cost pe minut, cu cifrele mentorului; riscul evitat e pe fiecare cont. |
