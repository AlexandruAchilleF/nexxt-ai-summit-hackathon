# Features/Rules (R1): motorul de reguli CRS / FATCA

Cod pur: fără DB, model, rețea sau ceas. Decide indiciile și `Decision`; **nu** calculează termene (R2) și nu întoarce
niciodată `ShieldAlert` (scutul). Teste: `tests/HackathonStarter.Tests/Crs/Rules/`.

## Contractele (Faza 0, `RuleModels.cs`)

Ordinea pozițională și sensul câmpurilor sunt cele din Faza 0 (#13, #18); R1 adaugă doar câmpuri la coadă, opționale.

| Tip | Faza 0 | Adăugat de R1 |
| --- | --- | --- |
| `RuleDefinition` | `Id, Regime, Title` (EN), `TitleRo, GuidePage, Version` | `Description` (EN), `DescriptionRo`, `LegalBasis`, `Basis` |
| `RuleHit` | `RuleId, Regime, Jurisdiction, Explanation` (EN), `ExplanationRo, GuidePage, SourceField, CoveredBy` | `Basis` |
| `RuleOutcome` | `Decision, Indicia, Cleared, ReportableFor, Explanation` (EN), `ExplanationRo, EngineVersion`; `Uncovered`, `Regime` (doar indiciile neacoperite = regimul cazului) | — |
| `RuleBasis` | — | `Verified` = textul legii sau al ghidului · `Interpretation` = citirea noastră · `Policy` = alegerea băncii |

`ICrsRuleEngine` (Faza 0) e implementat de `CrsRuleEngine`; `AddRules()` înregistrează o singură instanță pentru amândouă.
Catalogul are cele 26 de ID-uri din PLAN 8.2, plus `GEN-NEW-ACCOUNT`, `FATCA-SCOPE-ALL` și `FATCA-TIN` (TIN-ul SUA: lipsă sau
în format greșit; regulă FATCA, p. 38). `RuleIds.IsSelfCertificationFinding` spune ce constatări vin din formularul primit
(`SC-*`, `FATCA-TIN`, `FATCA-SC`): ele nu sunt o schimbare de circumstanțe.

## Intrarea: `ClientSnapshot` → `RuleSubject`

`SubjectMapper` construiește `RuleSubject` (vederea imutabilă a motorului) din `ClientSnapshot`; testele pot construi
`RuleSubject` direct. Enum-urile sunt cele comune din `Features/Clients`.

| Din `ClientSnapshot` | Atenție |
| --- | --- |
| `Addresses`, `Phones` (`Country` derivat), `PowersOfAttorney` | `IsServiceProvider` nu există: excepția din pct. 13 nu se aplică din date |
| `Accounts`: `Type`, `OpenedOn`, `Balance`, `Currency`, `StandingOrders` | preexistent se calculează pe regim (`Preexisting.For`); fără `Product`, explicația numește tipul contului |
| `StandingOrders.BeneficiaryAccount`, `BeneficiaryCountry` explicit | SUA nu are IBAN (Î2) |
| `SelfCertification` (ultima validă) | fără semnătură, nume, adresă, data nașterii din formular: `SC-VALID` le lasă operatorului (`Cleared`, `Policy`). Dacă declară statut SUA fără TIN-ul SUA, `FATCA-TIN` rămâne deschis și la reevaluare |
| `AcceptedExplanations` | acoperă indiciul CRS doar dacă niciun cont al clientului nu e preexistent CRS (Î11); jurisdicția nu devine raportabilă (cazul 18) |
| — | rezidența fiscală din KYC în afara autocertificării nu există: lit. a) nu se aplică din date |

## Cum se folosește

```csharp
var engine = sp.GetRequiredService<ICrsRuleEngine>();                  // AddRules() + AddTin() în Program.cs
var outcome = engine.Evaluate(before, after, ChangeKind.AddressChanged); // tot snapshot-ul after (Î3)
var current = engine.EvaluateCurrent(client);                          // unealta agentului, metrici
var closing = engine.EvaluateCurrent(client, knownAt);                 // închiderea cazului: max(Case.Indicia.AddedAt)
var form = engine.EvaluateSelfCertification(client, selfCert);          // cazurile 2, 3
var forAgent = outcome.Summarize(en: Lang.IsEn);                       // R4: unealta evaluateRules
```

- **R2:** deschide caz dacă `outcome.Uncovered` nu e gol; regimul cazului = `outcome.Regime`. Termenul: `DL-NEW-90` sau
  `DL-PRE-YEAR` după `Preexisting.For`, doar pentru un indiciu de schimbare de circumstanțe (`CRS-IND-*`). La FATCA,
  acordul nu dă termen (`FATCA-SC`). Un caz deschis doar pe constatări `SC-*` ale autocertificării (cazul 2) nu e o
  schimbare de circumstanțe: ghidul nu dă un termen (p. 37; Î1), deci `DueAt = null`, nu cele 90 de zile (p. 35-36). Un
  hit cu `Basis = Interpretation` înseamnă că termenul se bazează pe interpretarea noastră: afișați asta. Explicația
  rezonabilă acoperă doar dacă niciun cont al clientului nu e preexistent CRS (Î11); înainte de închiderea pe explicație,
  `EvaluateCurrent` cu explicația candidată trebuie să dea `Compliant` (cazul 18). La închidere, apelați
  `EvaluateCurrent(snapshot, item.Indicia.Where(i => !RuleIds.IsSelfCertificationFinding(i.RuleId)).Select(i => i.AddedAt).DefaultIfEmpty(item.OpenedAt).Max())`
  (ultima schimbare din caz; constatările pe formularul-răspuns nu mută data): doar așa motorul
  știe că autocertificarea e una nouă și o autocertificare „nu sunt persoană SUA” poate închide un caz FATCA (vezi mai jos).
  Fără data, doar o autocertificare care declară statut SUA acoperă un indiciu FATCA.
- **R3:** motorul folosește `ITinFormat` din `AddTin()`. `Source = NotChecked` = „format neverificat”, niciodată „format
  greșit”. `IssuesTinToAllResidents` are prioritate față de tabelul nostru OCDE (`Jurisdictions`), folosit doar când R3 dă null.
- **R4:** `Summarize()` dă maximum 10 elemente pe listă, cu `LegalBasis` și pagina. Modelul explică, nu schimbă decizia.
- **R5:** `GoldenEngineTests` rulează `GoldenCases.Ready(GoldenLayer.Engine)` prin `ICrsRuleEngine` cablat ca în
  `Program.cs`: datele canonice sunt așteptarea. `tests/.../Crs/Rules/EngineCases.cs` sunt fixturile R1 pe calea
  `RuleSubject`; un test verifică să nu contrazică niciodată cazurile canonice (decizie, indicii, regim).

## FATCA: când închide o autocertificare „nu sunt persoană SUA”

Textul Acordului FATCA (Legea 233/2015), Anexa I, verificat pe versiunea publicată de Trezoreria SUA
(<https://home.treasury.gov/system/files/131/FATCA-Agreement-Romania-5-28-2015.pdf>). Se aplică doar unei
autocertificări semnate în ziua (calendaristică, în România) în care banca a aflat de schimbare sau după
(`EvaluateCurrent(client, changeKnownAt)`).

| Indiciu | Cont nou (III.B.2) | Cont preexistent (II.B.3-4) |
| --- | --- | --- |
| Cetățenie SUA la dosar (lit. a) | nu acoperă: dosarul contrazice formularul (VI.A) | nu acoperă: II.B.4 nu are excepție |
| Locul nașterii în SUA (lit. b) | nu acoperă: cerem documentele din II.B.4 lit. a (interpretare) | nu acoperă: + act non-SUA + certificat de pierdere a cetățeniei sau explicație (lit. a) |
| Adresă SUA, doar telefoane SUA, ordin permanent (lit. c-e) | acoperă (`Interpretation`: rezonabilitatea o confirmă operatorul) | nu acoperă: + documente justificative (lit. b-c) |
| Împuternicire, «în grija», telefon SUA lângă unul non-SUA (lit. d, f, g) | acoperă | acoperă (lit. d) |

Motorul nu primește documente, deci nu acoperă niciodată ce le cere; cazul rămâne deschis, iar explicația spune ce lipsește.
Când toate indiciile FATCA sunt acoperite, `Cleared` are `FATCA-SC` („contul nu e raportabil SUA”).

## Ce e interpretare, nu lege (afișat în UI prin `Basis`)

| ID / situație | De ce | Cine confirmă |
| --- | --- | --- |
| `GEN-NEW-ACCOUNT` | Lista de indicii e procedura conturilor preexistente; pe conturile noi o folosim ca «motive să știe». VII.A pct. 5 lit. d) (RO: lit. b-d; OCDE: c-e) spune că unele indicii singure nu fac autocertificarea incorectă. Afectează 4, 5b, 11, 14, 6, 7. | specialistul de domeniu |
| `GEN-DOMESTIC` | Nicio regulă nu se aplică; analogie cu VII.A pct. 5 lit. c) | specialistul de domeniu |
| `CRS-IND-CO`, `FATCA-IND-CO` cu adrese vechi la dosar | „Nicio altă adresă” = nicio altă adresă **actuală** (Î6) | specialistul de domeniu |
| `CRS-IND-MAIL` din adresa «în atenția» | Singura adresă actuală c/o e și adresa de corespondență (lit. b; Î10, cazul 12); ruta rămâne cea c/o (p. 31). Nu și la «post-restant» sau la c/o lângă altă adresă | specialistul de domeniu |
| Explicație acceptată, client cu cont preexistent CRS | Nu acoperă: ghidul cere autocertificare și document justificativ (p. 29-30); explicația e o rută a conturilor noi (p. 35, 37; Î11). Testul e pe client (orice cont preexistent), nu pe cont | specialistul de domeniu |
| `FATCA-IND-POB` în `Cleared` | „Născut în X, nu în SUA” se spune doar când clientul are un indiciu FATCA și țara e un cod real (cazul 7); pe cont nou e `Interpretation` | banca |
| Jurisdicții neraportabile, „SUA nu e jurisdicție CRS” (`Cleared`) | Listele ANAF nu sunt în sursele din repo (NEVERIFICAT) | R5 / specialistul de domeniu |
| `SC-TIN-MISSING`, motiv C | Clientul declară legea altei țări (p. 53); motorul nu o cunoaște. Nu bate p. 37 (țara emite TIN tuturor) | operatorul |
| FATCA: autocertificare „nu sunt persoană SUA” pe cont nou | III.B.2 cere doar o autocertificare validă; că indiciul nu o face nesigură e lectura noastră | specialistul de domeniu |
| `CRS-IND-PHONE` cu telefon RO vechi | Telefonul RO care nu mai e actual nu anulează indiciul (Maria) | specialistul de domeniu |
| `DL-PRE-YEAR` (R2) | Cea mai târzie dintre 31.12 și 90 de zile: ghidul spune „sau”; Anexa 2 pct. 1 spune „mai recentă” pentru testul adresei de rezidență | decis de echipă (`docs/decizii-domeniu`) |
| `SC-TIN-FORMAT`, `FATCA-TIN` cu TIN SUA în format greșit | Politică: formatul greșit duce la clarificare | banca |
| `SC-VALID`, jurisdicția | Lipsa jurisdicției e `Verified` (p. 34, 36). Un cod care nu e ISO 3166-1 (plus `XK`) e `Policy`: p. 36 lasă banca să accepte o abreviere potrivită cu actul de identitate, pe care motorul nu-l vede. Un rând gol lângă unul completat se ignoră (politică); un rând cu TIN, dar fără țară, e `Interpretation`. În aplicație, `ChangeService` respinge azi țara goală înainte de motor, deci aceste două rute sunt doar ale motorului | operatorul |
| `FATCA-SCOPE-ALL` | Politică: nu aplicăm pragurile de 50.000 USD | banca |
| Valoarea mică FATCA | Sold curent, curs fix maxim (`AccountStatus`), nu soldul la 30.06.2014 / 31.12 | banca |
| `SC-TIN-MISSING` AT, motiv A sau B (Î8) | Acceptăm oricare, cu explicație la B | specialistul de domeniu |
