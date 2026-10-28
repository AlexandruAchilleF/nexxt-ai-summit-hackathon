# ADR 0001: Servicii provizorii pentru demo-ul Mariei, până intră R2 și seed-ul R5

- **Data:** 28.10.2026, 19:00
- **Actualizare:** 28.10.2026, 21:30 — R2 a intrat în PR #28; ICaseService și IChangeService sunt acum implementate
- **Stare:** acceptată (R4, cu acordul echipei: „demo-ul să meargă chiar înainte să fie gata serviciile R2")
- **Autor:** R4

## Context inițial

Faza 0 (PR #13, #18) a pus pe `main` contractele: `ICaseService`, `IChangeService`, `IClientService`, `ICrsRuleEngine`,
`IDeadlineCalculator`, `ISimClock`, `IShieldedDocuments`, `IEvidenceVerifier`, `ITinValidator`, `IAuditLog`. La 19:00:

- `CaseService` și `ChangeService` erau stub-uri: interogările întorc liste goale, tranzițiile aruncă `NotImplementedException`.
- `ClientSeed.Create` întoarce o listă goală, deci în bază nu există Maria (`CL-0001`).
- Motorul de reguli al lui R1 era în PR #16 (verde, draft). `DeadlineCalculator` și `SimClock` sunt gata.

Fără `CaseService` nu se putea deschide, aproba sau închide niciun caz. Demo-ul Mariei (cazul 4 din `docs/GOLDEN-CASES.md`) nu mergea.

## Decizia de atunci

R4 scrie, în `Features/Sentinel/Interim/`, implementări provizorii ale contractelor din Faza 0:

- `InterimCaseService : ICaseService` acoperă tranzițiile din PLAN 9.3, cu verificarea rolului, jurnalul și termenul
  din `IDeadlineCalculator`;
- `InterimChangeService : IChangeService` acoperă `AddressChanged`, `SelfCertSubmitted` și `DocumentUploaded`;
- seed-ul Mariei cu fluturașul ei;
- scenario-urile demo (aplicare change la caz, livrare răspuns client);
- o bară demo cu ceasul simulat (ISimClock).

## Status curent (28.10 21:30)

PR #28 a intrat pe `main`. R2 a implementat:
- `CaseService : ICaseService` — cu verificări complete de rol, stări și tranzații
- `ChangeService : IChangeService` — care deschide cazuri și le mișcă către staturi noi
- `DeadlineWatcher` — care calculează termenele și declanșează alerte
- `SimClockControl` — pentru a avansa ceasul simulat în demo

Prin urmare:
- Ștergem `InterimCaseService`, `InterimChangeService`, `InterimDeadlineWatcher`, `DemoClock` — R2 le acoperă complet.
- Păstrăm `DemoScenarios` și `MariaSeed` — R4 are nevoie să aplice scenario-urile Mariei și să planteze seed-ul ei,
  iar `ClientSeed.Create` e încă gol (va intra în PR #25 al lui R5).
- `DemoScenarios` nu mai are nevoie de implementări provizorii ale `IChangeService` și `ICaseService` — apelează
  serviciile reale ale lui R2 din DI.

## Testul cap-coadă ca criteriu de acceptare

Testul `MariaFlowTests` (E2E și unit) trebuie să treacă fără schimbări dacă piesele provizorii se retrag și se folosesc
serviciile R2 cu adevărat. Acesta e criteriul de acceptare pentru PR #28 și PR #25.

## Consecințe

- Demo-ul și testele Mariei merg pe serviciile reale ale lui R2.
- Cod provizoric rămas: doar `DemoScenarios` și `MariaSeed` (locale în R4), plus testele specifice demo-ului.
- Cod scos: `Features/Sentinel/Interim/InterimCaseService.cs`, `InterimChangeService.cs`, `InterimDeadlineWatcher.cs`,
  `DemoClock.cs`, `InterimSetup.cs`, `InterimStartup.cs`, `InterimCaseTexts.cs` — toate astea fac de R2 acum.

## Alternative respinse

1. **Așteptăm R2 și R5.** Demo-ul ar depinde de ultima piesă livrată, iar agentul nu poate fi testat cap-coadă.
2. **R4 scrie direct în `Features/Cases` și `Features/Changes`.** Ar intra în conflict cu munca lui R2, în aceleași
   fișiere.
3. **Mock-ul inventează starea cazului fără servicii.** Ar încălca regula „mock-ul apelează uneltele reale" (PLAN
   13.3), iar cazul nu s-ar schimba în baza de date.
