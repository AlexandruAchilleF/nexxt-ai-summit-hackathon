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
Catalogul are cele 26 de ID-uri din PLAN 8.2, plus `GEN-NEW-ACCOUNT` și `FATCA-SCOPE-ALL`.

## Intrarea: `ClientSnapshot` → `RuleSubject`

`SubjectMapper` construiește `RuleSubject` (vederea imutabilă a motorului) din `ClientSnapshot`; testele pot construi
`RuleSubject` direct. Enum-urile sunt cele comune din `Features/Clients`.

| Din `ClientSnapshot` | Atenție |
| --- | --- |
| `Addresses`, `Phones` (`Country` derivat), `PowersOfAttorney` | `IsServiceProvider` nu există: excepția din pct. 13 nu se aplică din date |
| `Accounts`: `Type`, `OpenedOn`, `Balance`, `Currency`, `StandingOrders` | preexistent se calculează pe regim (`Preexisting.For`); fără `Product`, explicația numește tipul contului |
| `StandingOrders.BeneficiaryAccount`, `BeneficiaryCountry` explicit | SUA nu are IBAN (Î2) |
| `SelfCertification` (ultima validă) | fără semnătură, nume, adresă, data nașterii din formular: `SC-VALID` le lasă operatorului (`Cleared`, `Policy`) |
| `AcceptedExplanations` | acoperă indiciul CRS, jurisdicția nu devine raportabilă (cazul 18) |
| — | rezidența fiscală din KYC în afara autocertificării nu există: lit. a) nu se aplică din date |

## Cum se folosește

```csharp
var engine = sp.GetRequiredService<ICrsRuleEngine>();                  // AddRules() + AddTin() în Program.cs
var outcome = engine.Evaluate(before, after, ChangeKind.AddressChanged); // tot snapshot-ul after (Î3)
var current = engine.EvaluateCurrent(client);                          // unealta agentului, metrici
var closing = engine.EvaluateCurrent(client, trigger.ReceivedAt);      // reevaluare înainte de închiderea cazului
var form = engine.EvaluateSelfCertification(client, selfCert);          // cazurile 2, 3
var forAgent = outcome.Summarize(en: Lang.IsEn);                       // R4: unealta evaluateRules
```

- **R2:** deschide caz dacă `outcome.Uncovered` nu e gol; regimul cazului = `outcome.Regime`. Termenul: `DL-NEW-90` sau
  `DL-PRE-YEAR` după `Preexisting.For`. La FATCA, acordul nu dă termen (`FATCA-SC`). Un hit cu `Basis = Interpretation`
  înseamnă că termenul se bazează pe interpretarea noastră: afișați asta. Înainte de închiderea pe explicație,
  `EvaluateCurrent` cu explicația candidată trebuie să dea `Compliant` (cazul 18). La închidere, apelați
  `EvaluateCurrent(snapshot, trigger.ReceivedAt)`: doar așa motorul știe că autocertificarea e una nouă și o
  autocertificare „nu sunt persoană SUA” poate închide un caz FATCA (vezi mai jos). Fără data, doar o autocertificare care
  declară statut SUA acoperă un indiciu FATCA.
- **R3:** motorul folosește `ITinFormat` din `AddTin()`. `Source = NotChecked` = „format neverificat”, niciodată „format
  greșit”. `IssuesTinToAllResidents` are prioritate față de tabelul nostru OCDE (`Jurisdictions`), folosit doar când R3 dă null.
- **R4:** `Summarize()` dă maximum 10 elemente pe listă, cu `LegalBasis` și pagina. Modelul explică, nu schimbă decizia.
- **R5:** `tests/.../Crs/Rules/EngineCases.cs` are datele din `docs/GOLDEN-CASES.md` pentru stratul de motor;
  `SnapshotPathTests` le trece prin entități, `ClientSnapshot.From` și `ICrsRuleEngine`.

## FATCA: când închide o autocertificare „nu sunt persoană SUA”

Textul Acordului FATCA (Legea 233/2015), Anexa I, verificat pe versiunea publicată de Trezoreria SUA
(<https://home.treasury.gov/system/files/131/FATCA-Agreement-Romania-5-28-2015.pdf>). Se aplică doar unei
autocertificări semnate în ziua în care banca a aflat de schimbare sau după (`EvaluateCurrent(client, changeKnownAt)`).

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
| Jurisdicții neraportabile, „SUA nu e jurisdicție CRS” (`Cleared`) | Listele ANAF nu sunt în sursele din repo (NEVERIFICAT) | R5 / specialistul de domeniu |
| `SC-TIN-MISSING`, motiv C | Clientul declară legea altei țări (p. 53); motorul nu o cunoaște. Nu bate p. 37 (țara emite TIN tuturor) | operatorul |
| FATCA: autocertificare „nu sunt persoană SUA” pe cont nou | III.B.2 cere doar o autocertificare validă; că indiciul nu o face nesigură e lectura noastră | specialistul de domeniu |
| `CRS-IND-PHONE` cu telefon RO vechi | Telefonul RO care nu mai e actual nu anulează indiciul (Maria) | specialistul de domeniu |
| `DL-PRE-YEAR` (R2) | Cea mai târzie dintre 31.12 și 90 de zile: ghidul spune „sau”; Anexa 2 pct. 1 spune „mai recentă” pentru testul adresei de rezidență | decis de echipă (`docs/decizii-domeniu`) |
| `SC-TIN-FORMAT` | Politică: formatul greșit duce la clarificare | banca |
| `FATCA-SCOPE-ALL` | Politică: nu aplicăm pragurile de 50.000 USD | banca |
| Valoarea mică FATCA | Sold curent, curs fix maxim (`AccountStatus`), nu soldul la 30.06.2014 / 31.12 | banca |
| `SC-TIN-MISSING` AT, motiv A sau B (Î8) | Acceptăm oricare, cu explicație la B | specialistul de domeniu |
