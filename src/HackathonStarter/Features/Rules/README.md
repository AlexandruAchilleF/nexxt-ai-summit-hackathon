# Features/Rules (R1): motorul de reguli CRS / FATCA

Cod pur: fără DB, model, rețea sau ceas. Decide indiciile și `Decision`; **nu** calculează termene (R2) și nu întoarce
niciodată `ShieldAlert` (scutul). Teste: `tests/HackathonStarter.Tests/Crs/Rules/`.

## Contractele (PLAN 8.1, cu câmpuri în plus la coadă, opționale)

| Tip | Ce adaugă față de PLAN |
| --- | --- |
| `RuleDefinition` | `TitleEn`, `LegalBasis` (ex. „CPF Anexa 1, Sec. III.B pct. 12 lit. c); pct. 13”), `Basis`, `DescriptionEn` |
| `RuleHit` | `ExplanationEn`, `Basis` (pe hit: `Interpretation` pe cont nou sau când citirea noastră decide) |
| `RuleOutcome` | `ExplanationEn`; calculate: `Uncovered` (nu se serializează), `CaseRegime` (`Crs` / `Fatca` / `Both` / null) |
| `RuleBasis` | `Verified` = textul legii sau al ghidului · `Interpretation` = citirea noastră · `Policy` = alegerea băncii |
| `Decision`, `Regime`, `Coverage` | identice cu PLAN 7.2 / 8.1 |

**Faza 0: nu redefiniți aceste tipuri.** Sunt aici, în `HackathonStarter.Features.Rules`.

## Intrarea: `RuleSubject` (până la Faza 0)

Motorul citește `RuleSubject`, o vedere imutabilă fără EF. Când `ClientSnapshot` există, `SubjectMapper` (R1) o
construiește din ea, iar `CrsRuleEngine` primește supraîncărcarea `Evaluate(ClientSnapshot, ClientSnapshot, ChangeEvent)`
din `ICrsRuleEngine`. Ce trebuie să conțină `ClientSnapshot`:

| `RuleSubject` | Din modelul comun (PLAN 7.1) | Atenție |
| --- | --- | --- |
| `Addresses` | `Address.Kind`, `Country`, `IsCurrent` | + `IsServiceProvider` (pct. 13), implicit false |
| `Phones` | `Phone.Country` (derivat din prefix), `IsCurrent` | +1 e comun SUA / Canada / Caraibe: țara se setează explicit |
| `Accounts` | `Type`, `OpenedOn`, `Balance`, `Currency`, `StandingOrders` | preexistent se calculează (`AccountStatus`), nu se citește |
| `StandingOrders` | `BeneficiaryCountry` **explicit**, `IsActive` | SUA nu are IBAN (Î2): `BeneficiaryAccount`, nu `BeneficiaryIban` |
| `PowersOfAttorney` | `HolderCountry`, `IsActive` | |
| `SelfCertifications` | `SignedAt`, `Residences`, `DeclaresUsPerson`, `Status == Valid` → `IsValid` | + nume, adresă, țara adresei, data nașterii din formular |
| `AcceptedExplanations` | (jurisdicție, categorie) | PLAN 7.1: în snapshot |
| `RecordedTaxResidences` | rezidența fiscală din KYC, în afara autocertificării | lit. a); gol dacă nu există |
| `RuleChange.Kind` | `ChangeEvent.Kind.ToString()` | doar pentru textul explicației |

## Integrarea cu Faza 0

Adaptorul e gata, dar compilat doar cu `CRS_PHASE0` (verificat pe tipuri cu forma exactă din PLAN 7.1 / 7.2 / 8.1):
`SubjectMapper.cs` (`ClientSnapshot` → `RuleSubject` + `CrsRuleEngine : ICrsRuleEngine`), o linie în `RulesSetup.cs`
și `tests/.../Crs/Rules/SnapshotPathTests.cs` (fiecare caz-etalon dă același rezultat prin snapshot ca direct).

1. Faza 0 pe branch: `git merge upstream/main`. Conflicte în `Features/Rules`: păstrăm `ICrsRuleEngine` din Faza 0 și
   implementarea noastră; ștergem stub-ul lor de motor și copiile lor de `RuleOutcome`, `RuleHit`, `RuleDefinition`,
   `Coverage`. Dacă `Decision` / `Regime` sunt definite de Faza 0 în alt namespace (pentru `RuleEvaluation` / `Case`),
   le ștergem din `RuleModels.cs` și adăugăm `using`-ul lor (valorile sunt aceleași, PLAN 7.2).
2. Activarea: `sed -i '/^#if CRS_PHASE0$/d; /^#endif$/d'` pe cele trei fișiere, apoi numele raportate de compilator
   (namespace-uri, forma `ClientSnapshot`, `BeneficiaryAccount` vs. `BeneficiaryIban`, `AcceptedExplanation`).
3. `dotnet build -warnaserror` și `dotnet test tests/HackathonStarter.Tests`.

Ce nu vine din PLAN 7.1 și ce face adaptorul:

| Lipsește din model | Efect |
| --- | --- |
| Numele, adresa, data nașterii din formularul de autocertificare | `SC-VALID` le lasă operatorului (`Cleared`, `Policy`); nu le raportează lipsă |
| `IsServiceProvider` pe adresă / telefon | excepția din pct. 13 nu se aplică din date |
| Rezidența fiscală din KYC, în afara autocertificării | lit. a) nu se aplică din date |

## Cum se folosește

```csharp
var engine = sp.GetRequiredService<CrsRuleEngine>();          // AddRules() în Program.cs (Faza 0)
var outcome = engine.Evaluate(before, after, change);        // tot snapshot-ul after (Î3)
var outcome2 = engine.EvaluateSelfCertification(client, sc); // cazurile 2, 3
var forAgent = outcome.Summarize(en: Lang.IsEn);             // R4: unealta evaluateRules
```

- **R2:** deschide caz dacă `outcome.Uncovered` nu e gol; regimul cazului = `outcome.CaseRegime`. Termenul: din
  `AccountStatus.IsCrsPreexisting(OpenedOn)` → `DL-NEW-90` sau `DL-PRE-YEAR` (Anexa 2 pct. 1: „oricare dintre acestea
  este mai recentă”). La FATCA, acordul nu dă termen (`FATCA-SC`). Un hit cu `Basis = Interpretation` înseamnă că
  termenul se bazează pe interpretarea noastră: afișați asta.
  Cura prin autocertificare care **exclude** jurisdicția + document (pct. 19) se înregistrează ca `AcceptedExplanation`
  pe acea jurisdicție, altfel reevaluarea redeschide indiciul.
- **R3:** implementați `ITinFormatCheck` (adaptor peste `TinFormat`) și înregistrați-l; fără el, formatul apare
  „neverificat” și `SC-TIN-FORMAT` nu se aplică.
- **R4:** `Summarize()` dă maximum 10 elemente pe listă, cu `LegalBasis` și pagina. Modelul explică, nu schimbă decizia.
- **R5:** `tests/.../Crs/Rules/EngineCases.cs` are datele din `docs/GOLDEN-CASES.md` pentru stratul de motor.

## Ce e interpretare, nu lege (afișat în UI prin `Basis`)

| ID / situație | De ce | Cine confirmă |
| --- | --- | --- |
| `GEN-NEW-ACCOUNT` | Lista de indicii e procedura conturilor preexistente; pe conturile noi o folosim ca «motive să știe». VII.A pct. 5 lit. d) (RO: lit. b-d; OCDE: c-e) spune că unele indicii singure nu fac autocertificarea incorectă. Afectează 4, 5b, 11, 14, 6, 7. | specialistul de domeniu |
| `GEN-DOMESTIC` | Nicio regulă nu se aplică; analogie cu VII.A pct. 5 lit. c) | specialistul de domeniu |
| `CRS-IND-CO` cu adrese vechi la dosar | „Nicio altă adresă” = nicio altă adresă **actuală** (Î6) | specialistul de domeniu |
| `CRS-IND-PHONE` cu telefon RO vechi | Telefonul RO care nu mai e actual nu anulează indiciul (Maria) | specialistul de domeniu |
| `DL-PRE-YEAR` (R2) | Cea mai târzie dintre 31.12 și 90 de zile: ghidul spune „sau”; Anexa 2 pct. 1 spune „mai recentă” pentru testul adresei de rezidență | decis de echipă (`docs/decizii-domeniu`) |
| `SC-TIN-FORMAT` | Politică: formatul greșit duce la clarificare | banca |
| `FATCA-SCOPE-ALL` | Politică: nu aplicăm pragurile de 50.000 USD | banca |
| Valoarea mică FATCA | Sold curent, curs fix maxim (`AccountStatus`), nu soldul la 30.06.2014 / 31.12 | banca |
| `SC-TIN-MISSING` AT, motiv A sau B (Î8) | Acceptăm oricare, cu explicație la B | specialistul de domeniu |
