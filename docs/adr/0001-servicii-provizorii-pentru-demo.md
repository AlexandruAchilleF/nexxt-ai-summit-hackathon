# ADR 0001: Servicii provizorii pentru demo-ul Mariei, până intră R2 și seed-ul R5

- **Data:** 28.10.2026, 19:00
- **Stare:** acceptată (R4, cu acordul echipei: „demo-ul să meargă chiar înainte să fie gata serviciile R2”)
- **Autor:** R4

## Context

Faza 0 (PR #13, #18) a pus pe `main` contractele: `ICaseService`, `IChangeService`, `IClientService`, `ICrsRuleEngine`,
`IDeadlineCalculator`, `ISimClock`, `IShieldedDocuments`, `IEvidenceVerifier`, `ITinValidator`, `IAuditLog`. La 19:00:

- `CaseService` și `ChangeService` sunt stub-uri: interogările întorc liste goale, tranzițiile aruncă `NotImplementedException`.
- `ClientSeed.Create` întoarce o listă goală, deci în bază nu există Maria (`CL-0001`).
- Motorul de reguli al lui R1 e în PR #16 (verde, draft). `DeadlineCalculator` și `SimClock` sunt gata.

Fără `CaseService` nu se poate deschide, aproba sau închide niciun caz. Agentul nu are pe ce rula, iar demo-ul Mariei
(cazul 4 din `docs/GOLDEN-CASES.md`) nu merge.

## Decizia

R4 scrie, în `Features/Sentinel/Interim/`, implementări provizorii ale contractelor din Faza 0:

- `InterimCaseService : ICaseService` acoperă tranzițiile din PLAN 9.3, cu verificarea rolului, jurnalul și termenul
  din `IDeadlineCalculator`;
- `InterimChangeService : IChangeService` acoperă `AddressChanged`, `SelfCertSubmitted` și `DocumentUploaded`;
- seed-ul Mariei cu fluturașul ei;
- o „cutie de intrare” demo, care aduce autocertificarea ei ca document nou.

Toate folosesc doar interfețele din Faza 0. Regulile, termenul și verificarea citatelor rămân în codul lui R1, R2
și R3.

Se activează singure doar cât timp în DI e încă stub-ul din Faza 0. Îl recunoaștem după constructorul fără
dependențe. Când R2 pune un `CaseService` sau `ChangeService` real, respectiv R5 un `ClientSeed` care nu e gol, partea
provizorie corespunzătoare se oprește. La pornire aplicația scrie în log ce piese provizorii sunt active.

## Alternative respinse

1. **Așteptăm R2.** Demo-ul ar depinde de ultima piesă livrată, iar agentul nu poate fi testat cap-coadă.
2. **R4 scrie direct în `Features/Cases` și `Features/Changes`.** Ar intra în conflict cu munca lui R2, în aceleași
   fișiere.
3. **Mock-ul inventează starea cazului fără servicii.** Ar încălca regula „mock-ul apelează uneltele reale” (PLAN
   13.3), iar cazul nu s-ar schimba în baza de date.

## Consecințe

- Demo-ul și testele Mariei merg acum, pe aceleași interfețe pe care le va implementa R2.
- Avem cod duplicat temporar. Când intră serviciile R2, se șterge `Features/Sentinel/Interim/`, iar testul cap-coadă
  al Mariei trebuie să treacă neschimbat pe implementarea lui R2. Testul acela devine criteriul de acceptare pentru R2.
- Riscul unei diferențe de comportament între varianta provizorie și cea finală rămâne. Îl acoperă testul cap-coadă și
  cazurile-etalon din `GoldenCases`.
