# Playbook: DosarViu (Raiffeisen, Operational Efficiency)

NEXXT AI Hackathon, 28–29 oct. 2026. Kickoff miercuri 09:00, **predare joi 11:00**: cod sursă, demonstrația agentului
și un **video de maximum 3 minute**. Nu există prezentare live decât pentru câștigătorul fiecărui challenge.
Faptele și sursele: [`RESEARCH-KYC.md`](RESEARCH-KYC.md). Video și predare: [`SUBMISSION.md`](SUBMISSION.md).

## 1. Ideea, într-o propoziție

> **La revizuirea KYC a unei firme, agentul citește certificatul ONRC, îl compară cu dosarul băncii, calculează
> beneficiarii reali pe fiecare lanț de proprietate și pregătește actualizarea; analistul doar verifică și aprobă.**

- **Cine suferă:** analiștii KYC / onboarding de firme din Raiffeisen. Copiază date din PDF-uri, compară de mână și
  calculează procente indirecte pe lanțuri de firme.
- **Cine plătește:** Raiffeisen (bugetul de operațiuni și conformitate). O revizuire KYC corporate costă în medie
  1.500–3.000 $ și durează săptămâni ([`RESEARCH-KYC.md`](RESEARCH-KYC.md#3-cât-costă-azi-pentru-cine-plătește)).
- **Ce e unic (de spus în video):**
  1. **Calculul e cod, nu model.** Procentele indirecte și pragul legal sunt funcții testate; modelul doar extrage
     și explică. Același certificat dă mereu același rezultat.
  2. **Pragul e o setare, iar 25% fix ajunge la om.** Peste 25% azi (Legea 129/2019), 25% sau mai mult din
     10 iulie 2027 (AMLR). Nimeni altcineva din sală nu va ști asta.
  3. **Datele nu pleacă din bancă.** Modelul poate rula pe serverul nostru / al băncii (API compatibil OpenAI),
     iar datele personale sunt mascate înainte de orice model.

## 2. Ce construim (și ce nu)

**Scenariul de demo** (firmă și persoane sintetice, `SyntheticData`):

```
Alfa Logistic SRL (clientul)
├── Maria Ionescu        20% direct
├── Beta Holding SRL     40%  ├─ Maria Ionescu 60%    ⇒ 24% indirect ⇒ total 44%  ✔ beneficiar real
│                             └─ Andrei Pop    40%    ⇒ 16%                       ✘ sub prag
└── Gamma Invest SRL     40%  ├─ Elena Radu    62,5%  ⇒ exact 25%                 ⚠ la prag: decide omul
                              └─ Ion Vasile    37,5%  ⇒ 15%                       ✘ sub prag
```

Elena e exact la 25%: **nu** e beneficiar real după Legea 129/2019 (peste 25%), dar **este** după AMLR din 2027
(25% sau mai mult). Calculul se face cu `decimal`, ca 40% × 62,5% să dea fix 25%, nu 24,9999%.

Dosarul vechi o avea pe Maria doar cu 20% direct; certificatul nou arată intrarea Beta Holding.
Agentul trebuie să găsească diferența, să ceară certificatele Beta și Gamma, să calculeze și să propună actualizarea.

| Must-have (pentru video) | Nice-to-have (doar dacă rămâne timp) | Nu facem |
| --- | --- | --- |
| Entități + seed sintetic | Graf de proprietate vizual (Cytoscape) | Integrare reală ONRC / registrul BR |
| Extragere certificat → câmpuri (cu mock) | Raport de discrepanță față de registrul BR | Persoane politic expuse (PEP), sancțiuni |
| Diferențe certificat vs dosar, **în cod**, cu teste | Raport KYC exportabil (`IRunExporter`) | Entități străine (doar semnalate) |
| Calculatorul de beneficiari reali, **cu teste** (lanțuri, sume, cicluri, prag configurabil) | Mod „regim 2027” comutabil din UI | OCR pe scanări proaste |
| Agentul: extrage → compară → calculează → propune actualizarea (**cu aprobare**) | | |
| Pagina dosarului: asociați, lanțuri (tabel indentat), beneficiari | | |
| Verificări deterministe pe răspuns (`pii-leak` + una nouă, vezi mai jos) | | |

**Verificare nouă (Check):** `ubo-threshold-explained`: dacă răspunsul agentului numește un beneficiar real,
procentul trebuie să fie cel calculat de cod (ca `expected-number`), iar cazurile la limită să fie menționate.
Verdictul îl dă codul, nu modelul.

## 3. Plan de construcție (echipa)

Ore efective, în paralel. Estimările vin din playbook-ul de ieri, ajustate pentru scenariul de mai sus.

| Pas | Piese din template | Cine | Ore |
| --- | --- | --- | --- |
| Entități `Company`, `Shareholding`, `KycFile` + seed (3 firme, 4 persoane, dosarul vechi) | `tools/NewFeature`, `SyntheticData` | dev .NET 1 | 1 |
| `OwnershipCalculator`: lanțuri, produs, sumă pe persoană, cicluri, prag `> / ≥`, fallback conducere. **Teste întâi** | ca `FinanceCalculator` | dev .NET 1 + securitate | 2 |
| `KycDiff`: certificat vs dosar (asociați noi/ieșiți, cote, administratori), cu teste | serviciu nou | securitate | 1.5 |
| Certificate ONRC sintetice în PDF (3 bucăți) + extragere `StructuredAi.GenerateAsync<CertificateData>` cu mock | `DocumentText`, `StructuredAi` | dev .NET 2 | 2 |
| `KycAgent : ToolAgent`: `extractCertificate`, `compareWithFile`, `computeBeneficialOwners`, `listMissingCertificates`, `updateFile` (**needsApproval**) + mock RO/EN | `ToolAgent`, `TextMatch` | dev .NET 2 | 2 |
| Paginile `/kyc` (listă) și `/kyc/{id}` (dosar, lanțuri, beneficiari, chat cu agentul) + `SiteConfig.Nav` | `PageHeader`, `Pill`, `AgentChat` | frontend | 3 |
| Dashboard pe KYC (dosare de revizuit, discrepanțe, aprobări în așteptare) în locul celui cu Items | `StatCard`, `ActivityRings` | frontend | 1.5 |
| Check-ul `ubo-threshold-explained` + test în `PlatformTests.cs` | `Features/Checks` | securitate | 1 |
| `/privacy` și textele legale la zi pentru KYC (temei legal, retenție) | `Privacy.razor`, `LegalTexts` | securitate | 0.5 |
| Înregistrări pentru video (`AI:Record = true`), README | `Recordings/` | dev .NET 2 | 1 |
| ROI, scenariul video, montaj, întrebări probabile | `SUBMISSION.md` | pitch | continuu |

**Ordinea care nu se negociază:** calculatorul și diff-ul cu teste (fără ele nu avem produs), apoi agentul,
apoi paginile. Graful vizual vine ultimul; dacă nu e gata la 22:00, rămâne tabelul indentat.

**Reguli de lucru:**
- Branch → PR → CI verde → merge. Render publică `main` doar după CI (`autoDeployTrigger: checksPass`).
- Orice text nou din interfață: `@T("English")` + româna în `Services/Lang.Ro.cs` (altfel `LangTests` pică).
- Calcule doar în cod, cu teste. Modelul nu face aritmetică.
- Doar date sintetice. Niciun certificat sau CNP real în repo (repo-ul e public).
- Înainte de video: ascundem din `SiteConfig.Nav` paginile care nu țin de KYC (Arena, Scenarii, Calculatoare).

## 4. Cine plătește și cât (calculul pentru video)

Formula, ca s-o completăm cu cifrele mentorului (`RESEARCH-KYC.md`, secțiunea 4):

```
economie pe an = revizuiri de firme pe an × minute economisite pe revizuire × cost pe minut de analist
```

**Exemplu ilustrativ, nu cifră Raiffeisen** (îl înlocuim după checkpoint): 10.000 de revizuiri/an × 45 min × 0,5 €/min
≈ **225.000 €/an**. În video spunem doar cifre confirmate sau cu sursă (1.500–3.000 $ per revizuire, Fenergo).

**Model de business:** licență per revizuire procesată sau per analist, instalare on-premise (modelul rulează la bancă).
Banca plătește fiindcă scade costul per revizuire și riscul de amendă pentru beneficiari reali greșiți.

## 5. GDPR: răspunsurile pregătite

| Întrebare | Răspuns |
| --- | --- |
| Pe ce temei prelucrați datele beneficiarilor? | Obligație legală (art. 6 alin. 1 lit. c GDPR): Legea 129/2019 obligă banca să identifice beneficiarul real. Noi nu adăugăm date noi; procesăm ce banca are deja obligația să verifice. |
| Unde ajung datele? | CNP, IBAN etc. sunt mascate înainte de model. Modelul poate rula pe serverul băncii; în demo arătăm `/privacy`, care citește configurația live. |
| Cât le păstrați? | Cât cere legea AML pentru dosarul clientului (5 ani de la încetarea relației, art. 40 din Directiva 2015/849); rulările agentului au retenție separată, configurabilă. |
| Decide AI-ul? | Nu. Agentul propune; actualizarea dosarului pleacă doar cu aprobarea analistului (art. 22 GDPR: fără decizii exclusiv automate). |
| Ce date ați folosit? | Doar sintetice: firme, persoane și certificate generate. Nu ne-a trebuit nicio dată Raiffeisen. |

## 6. Riscuri

| Risc | Ce facem |
| --- | --- |
| Certificatul real arată altfel decât credem | Întrebăm mentorul la primul checkpoint; extragerea are mock, deci demo-ul nu depinde de format. |
| Modelul extrage greșit un procent | Analistul vede fiecare câmp cu pagina-sursă; diff-ul și calculul sunt în cod, cu teste. |
| Nu terminăm graful | Tabel indentat (e deja must-have). |
| Pică modelul în timpul filmării | Înregistrări + `AgentMode = Replay`; în video nu trebuie să apară eticheta „Simulare”, deci testăm conexiunea înainte. |

## 7. Program

| Când | Ce |
| --- | --- |
| Miercuri 09:00–10:00 | Kickoff, citim regulamentul (cod scris dinainte? date?), confirmăm ideea. Branch-uri pe roluri. |
| 10:00–13:00 | Calculator + diff cu teste; entități și seed; certificate sintetice. |
| 13:00–17:00 | Extragere, agent, primele pagini. **Checkpoint mentor:** întrebările din `RESEARCH-KYC.md`. |
| 17:00–22:00 | Pagini complete, dashboard, check nou, `/privacy`; graful dacă e timp. |
| 22:00–02:00 | Înregistrări, lustruire pe calea de demo, README, scenariul video scris cuvânt cu cuvânt. |
| Joi 07:00 | **Code freeze**: doar reparații pe calea de demo. |
| 07:00–09:30 | Filmare (mai multe duble, `AgentMode = Replay`), montaj, voce. |
| 09:30–10:30 | `dotnet build` (0 warnings) + teste, deploy, link în README. **Predare până la 10:30.** |

## 8. Ce avem deja în template (nu se mai scrie)

| Nevoie | Piesa | Unde |
| --- | --- | --- |
| Agent cu unelte, aprobare, istoric | `ToolAgent`, `needsApproval`, `/runs` | `Agents/` |
| PDF → text, extragere tipizată | `DocumentText`, `StructuredAi` | `Services/` |
| Verdicte deterministe | `Check`, `expected-number`, `pii-leak` | `Features/Checks` |
| Mascare date personale, retenție, `/privacy` | `PiiVault`, `RunRedaction`, `RetentionService` | `Services/` |
| Date sintetice valide (CNP, CUI, IBAN) | `SyntheticData` | `Features/Synthetic` |
| Export (teste, rapoarte), cu aprobare | `IRunExporter`, `/exports` | `Features/Exports` |
| Demo care nu pică | live → înregistrare → mock | `AI:Record`, `Recordings/` |
| Pagina principală, pagini publice, RO/EN | `SiteConfig.Landing`, `PublicLayout`, `Lang` | `Config/`, `Components/` |
