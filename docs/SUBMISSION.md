# Predarea: CRS Sentinel (Raiffeisen, AI for Operational Efficiency)

Ce primește juriul: **codul sursă**, o **demonstrație practică a agentului** (scenariu cap-coadă) și un **video de maximum
3 minute** (problema, ce e unic, cum funcționează arhitectura). Predare **joi, 29 oct., ora 11:00**; noi predăm până la
**10:30**. Prezentare pe scenă doar pentru câștigătorul challenge-ului: o pregătim atunci, nu acum.

Planul: [`PLAN.md`](PLAN.md) și [`PLAYBOOK.md`](PLAYBOOK.md). Cazurile-etalon: [`GOLDEN-CASES.md`](GOLDEN-CASES.md).
Faptele și paginile din ghid: [`RESEARCH-CRS.md`](RESEARCH-CRS.md).

## Video: 3 minute, 6 părți

| Timp | Parte | Ce se vede | Ce se spune (de scris cuvânt cu cuvânt) |
| --- | --- | --- | --- |
| 0:00–0:20 | **Problema** | Ecranul cu termenele care expiră | Când un client își schimbă adresa, banca are un termen să afle unde plătește taxe. Altfel îl raportează greșit la ANAF, pe fiecare cont. În primul semestru din 2025, după verificările ANAF, 891 de înregistrări au fost corectate. |
| 0:20–1:20 | **Maria, cap-coadă** | `/cases/{code}`: Maria se mută la München → caz deschis, termen 90 de zile → dovezi cu citat (telefon german, niciun telefon RO) → salariul german ca semnal, fără alertă → cererea scrisă de agent, aprobată de operator → data simulată la ziua 85: memento și escaladare → autocertificare nouă, format TIN verificat → caz închis cu canalul, momentul și dovada | Agentul alege ce dovezi caută; codul decide dacă un indiciu există, cu pagina din ghid; omul aprobă cererea și închiderea. |
| 1:20–1:50 | **Capcane și neprevăzut** | Ordin permanent către Germania (fără caz) lângă ordin permanent către SUA (alertă FATCA); Cluj → Iași (niciun caz); PDF cu text alb ascuns oprit de scut; răspuns contradictoriu → excepție la om | Ordinul permanent din contul curent nu e indiciu CRS (ghidul, p. 29), dar este indiciu FATCA (p. 31). Ce nu se potrivește ajunge la om, cu ambele citate. |
| 1:50–2:30 | **Arhitectura** | `/architecture`, apoi `/privacy` | LLM-ul citește și scrie, codul verifică regulile, omul decide. Date personale mascate înainte de model, servicii externe cu replay, model care poate rula pe serverele băncii. |
| 2:30–2:50 | **Testabil** | `/metrics`: toate cazurile-etalon, așteptat vs. obținut; `/changes/new`: un câmp schimbat, verdictul pe loc | Fiecare caz are rezultatul scris înainte de rulare. Ce nu prindem încă: conturile firmelor, W-8BEN, fișierul F3000. |
| 2:50–3:00 | **Fraza de final** | Logo + URL | Fiecare modificare de date poate porni un termen legal. CRS Sentinel o transformă într-o decizie înainte să expire termenul, cu dovadă pentru fiecare pas și cu omul la decizie. |

Sfaturi:
- **Filmăm live (Bedrock) sau pe mock, niciodată în `AgentMode = Replay` pe pașii care modifică date.** Replay-ul nu
  rulează uneltele: în chat apar pașii, dar nu se salvează nicio dovadă și cazul nu își schimbă starea; o aprobare reluată,
  dacă e aprobată, chiar rulează, cu argumentele mascate din înregistrare (PLAN §14). Modelul real se
  testează înainte (Settings → Testează conexiunile, pe calculatorul de filmare); dacă nu e disponibil, filmăm pe mock,
  care cheamă uneltele reale (eticheta „Simulare” rămâne vizibilă). Înregistrările (`AI:Record = true`) sunt doar pentru
  întrebările care citesc (ex. „rezumă cazul”). Replay-ul `TargetClient` pentru TIN on Europa e altceva și rămâne.
- Interfața în română (implicit). Zoom 125% în browser, fără bara de bookmark-uri, date sintetice realiste.
- Vocea separat de ecran. Export 1080p. Durata: **≤ 3:00**.
- Spunem o dată, clar, că pornim de la un template pregătit înainte (e în istoricul repo-ului).

## Program (de la sfârșit spre început)

| Când | Ce |
| --- | --- |
| Joi 08:00 | **Code freeze**: doar reparații pe calea de demo. Demo-ul de rezervă e deja înregistrat. |
| 08:00–09:45 | Filmare, mai multe duble; montaj; voce. |
| 09:45–10:30 | `dotnet build` (0 warnings) + `dotnet test`, deploy verificat, link în README, video încărcat. |
| **10:30** | **Predare.** Nu în ultimul minut. |

## Checklist demo

- [ ] Scenariul (Maria + 3 capcane + scut + tamper + metrici) scris cuvânt cu cuvânt și repetat
- [ ] Mock-ul parcurge tot traseul, cu uneltele reale; înregistrările (`src/HackathonStarter/Recordings/*.json` în repo)
      doar pentru întrebările care nu modifică date
- [ ] Data simulată resetată și datele demo resetate înainte de filmare (Settings → Resetează datele demo)
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
| „Băncile au primit amenzi de sute de mii de euro.” | „Riscul e pe fiecare cont.” (Amenzile publice pe 2024: 102.000 lei în total.) |
| „Raiffeisen are probleme cu CRS.” | Cifrele ANAF sunt pe tot sectorul. |
| „Nimeni n-a mai făcut asta.” | „Metodă cunoscută, aplicată pe fluxul de după onboarding, cu regulile din ghidul ANAF și omul la decizie.” |
| „Venitul din străinătate înseamnă rezidență acolo.” | Nu e indiciu oficial; e semnal contextual, fără efect. |
| „TIN valid.” | „Format valid.” |
| „Orice actualizare pornește un termen.” | „Fiecare actualizare trebuie evaluată; doar cele relevante fiscal pornesc termenul.” |
| „Termenul e mereu 90 de zile.” | „90 de zile la conturile noi; la cele preexistente, până la sfârșitul anului sau 90 de zile.” |
| Reținerea FATCA de 30% | Nu apare în ghid: nu o spunem. |

## Întrebări probabile ale juriului (cu răspunsul)

| Întrebare | Răspuns scurt |
| --- | --- |
| Ce face agentul singur și unde decide omul? | Alege ce dovezi caută, le citează, scrie cererea și evaluează răspunsul. Codul decide dacă există un indiciu și calculează termenul; omul aprobă cererea și închiderea. |
| Ce se întâmplă dacă modelul greșește sau inventează un citat? | Citatul e verificat în cod față de document și respins dacă nu există. Verdictul vine din reguli, nu din model. |
| De ce nu ChatGPT? | Un chatbot dă o părere și poate răspunde diferit la același dosar. Aici regulile sunt cod, cu pagina din ghid, iar termenul e urmărit pe fiecare caz. |
| Raiffeisen nu are deja asta? | Semnalul la modificarea datelor poate exista. Noi acoperim ce urmează după el: dovezi, cerere, răspuns, termen, închidere cu dovadă. (Răspunsul mentorului: de completat.) |
| De ce ordinul permanent către Germania nu e indiciu, dar cel către SUA este? | La CRS, ordinele permanente din conturile de depozit sunt excluse (ghidul, p. 29); la FATCA excepția nu există (p. 31). |
| De ce termenul Mariei e 90 de zile? | Contul ei e cont nou (deschis după 2016): 90 de zile de la pierderea valabilității (p. 35–36). La un cont preexistent, termenul e sfârșitul anului sau 90 de zile (p. 29). |
| Cum verificăm că merge? | `/metrics` rulează toate cazurile-etalon cu rezultatul scris înainte; `/changes/new` răspunde la „și dacă…?” pe loc; `dotnet test` rulează aceleași cazuri. |
| Merge cu date reale, sensibile? | Da: mascare înainte de model, model care poate rula la bancă, temei legal (Legea 207/2015, Acordul FATCA), retenție configurabilă. |
| Ce e real și ce e simulat? | Reale: regulile, termenele, scutul, verificarea formatului TIN (local și prin TIN on Europa, live sau replay `TargetClient`; TIN on Europa verifică doar structura și sintaxa), bucla cu aprobări. Sintetice: clienții, documentele, răspunsurile clientului. Nefăcute: integrarea cu core banking, trimiterea mesajelor. |
| Cine plătește și cât? | Banca, per caz sau licență; economia = cazuri × minute economisite × cost pe minut, cu cifrele mentorului; riscul evitat e pe fiecare cont. |
