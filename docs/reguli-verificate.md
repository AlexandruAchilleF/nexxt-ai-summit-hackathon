# CRS Sentinel: reguli verificate

Sursa primara: Ghidul ANAF DAC2/CRS/FATCA, varianta 29.12.2025 (79 pagini). Paginile sunt paginile PDF, identice cu numerotarea tiparita.
Verificat la 28.10.2026 pe textul extras din PDF.

Stare: CONFIRMAT = text gasit in ghid. CORECTAT = ghidul spune altceva decat lista de lucru. NEVERIFICAT = nu apare in ghid.

## Important pentru domeniul MVP

Lista de indicii (CRS-IND-*, FATCA-IND-*) este, in ghid, procedura pentru conturile PREEXISTENTE (cautarea in evidenta electronica, p.29 si p.31).
Pentru conturile NOI, ghidul cere declaratia pe proprie raspundere plus testul de rezonabilitate fata de informatiile de la deschidere, inclusiv documentele KYC (p.33).
In MVP folosim indiciile ca criterii prin care banca "stie sau are motive sa stie" ca declaratia e contrazisa (testul de rezonabilitate, schimbarea de situatie). Aceasta este interpretarea noastra de design si se spune deschis in README si in UI.

## CRS / DAC2, persoane fizice

| ID | Regula | Stare | Sursa |
|---|---|---|---|
| CRS-IND-A | Titularul identificat drept rezident al unei jurisdictii raportabile | CONFIRMAT | Ghid p.29, lit. a; Legea 207/2015 Anexa 1 Sect. III subsect. B |
| CRS-IND-B | Adresa de corespondenta sau de rezidenta actuala, inclusiv casuta postala, intr-o jurisdictie raportabila | CONFIRMAT | Ghid p.29, lit. b |
| CRS-IND-C | Unul sau mai multe telefoane dintr-o jurisdictie raportabila SI niciun telefon in Romania | CONFIRMAT | Ghid p.29, lit. c |
| CRS-IND-D | Instructiuni permanente de transfer catre un cont dintr-o jurisdictie raportabila, ALTELE DECAT cele privind un cont de depozit | CORECTAT: nu se aplica la conturile de depozit | Ghid p.29, lit. d |
| CRS-IND-E | Imputernicire sau delegare de semnatura valabila, acordata unei persoane cu adresa intr-o jurisdictie raportabila | CONFIRMAT | Ghid p.29, lit. e |
| CRS-IND-F | Adresa "post-restant" sau "in atentia" intr-o jurisdictie raportabila, cand nu exista nicio alta adresa la dosar | CONFIRMAT, cu precizare: doar in jurisdictie raportabila | Ghid p.29, lit. f; efect p.31 (cautare pe hartie sau declaratie, altfel cont nedocumentat) |
| CRS-IND-EFFECT | Indiciu gasit: titularul e tratat ca rezident in fiecare jurisdictie raportabila cu indiciu, cu exceptiile de la pct.19 | CONFIRMAT | Ghid p.30 |
| CRS-IND-FALSE | Indiciile pot fi "false"; se recomanda contactarea clientului pentru clarificare | CONFIRMAT | Ghid p.30 |
| CRS-RZ-1 | Adresa din declaratie contrazice documentele KYC: testul de rezonabilitate esueaza | CONFIRMAT | Ghid p.36, Exemplul 1 |
| CRS-RZ-2 | Adresa de rezidenta din declaratie nu e in jurisdictia de rezidenta fiscala declarata: esueaza | CONFIRMAT | Ghid p.37, Exemplul 2 |
| CRS-RZ-3 | Declaratie fara TIN, desi portalul OCDE arata ca jurisdictia emite TIN tuturor rezidentilor: declaratie nesigura | CONFIRMAT | Ghid p.37 |
| CRS-RZ-4 | Declara ca nu are rezidenta fiscala, dar dosarul contine o adresa: motiv de indoiala | CONFIRMAT | Ghid p.37 |
| CRS-RZ-5 | Discrepantele dintre declaratie si dosarul principal NU sunt eroare fara importanta | CONFIRMAT | Ghid p.36 |
| CRS-RZ-6 | Explicatii rezonabile pentru adresa diferita: student, profesor sau stagiar, diplomat, functionar UE | CONFIRMAT | Ghid p.37 |
| CRS-AC-1 | Declaratia e valabila doar daca e semnata sau confirmata, datata si contine: nume, adresa de rezidenta, jurisdictia sau jurisdictiile, TIN pentru fiecare jurisdictie raportabila, data nasterii | CONFIRMAT | Ghid p.34 |
| CRS-AC-2 | Confirmarea pozitiva a declaratiei se inregistreaza: pasi, moment, canal, dovezi | CONFIRMAT | Ghid p.34-35 |
| CRS-AC-3 | Mai multe jurisdictii declarate: cont raportabil pentru fiecare jurisdictie raportabila | CONFIRMAT | Ghid p.33-34 |
| CRS-SC-1 | Schimbare de situatie: orice informatie adaugata sau contradictorie relevanta, inclusiv la conturile asociate | CONFIRMAT | Ghid p.30 |
| CRS-SC-2 | Declaratia isi pierde valabilitatea cand banca stie sau are motive sa stie de schimbare; 90 de zile pentru declaratie noua sau explicatie; altfel rezident in jurisdictia initiala SI in cea noua | CONFIRMAT | Ghid p.35-36 |
| CRS-SC-3 | Declaratia CRS nu expira prin simpla trecere a timpului | NEVERIFICAT ca text explicit: ghidul numeste doar schimbarea de situatie drept cauza | Ghid p.35 |
| CRS-TIN-1 | TIN lipsa: motivul A (tara nu emite), B (nu poate obtine, cu explicatie), C (TIN-ul nu e cerut de legea interna) | CONFIRMAT | Ghid p.53, Anexa I, model de declaratie |
| CRS-TIN-2 | Nu exista obligatia raportarii TIN daca jurisdictia nu emite; exista echivalente functionale | CONFIRMAT | Ghid p.48 |
| CRS-ADR-1 | Adresa "post restant" sau "in atentia" nu e adresa de rezidenta | CONFIRMAT | Ghid p.48 |

## FATCA, persoane fizice

| ID | Regula | Stare | Sursa |
|---|---|---|---|
| FATCA-IND-1 | Cetatean sau rezident SUA | CONFIRMAT | Ghid p.31 |
| FATCA-IND-2 | Loc al nasterii in SUA, indicat fara ambiguitate | CONFIRMAT | Ghid p.31 |
| FATCA-IND-3 | Adresa actuala de corespondenta sau resedinta in SUA, inclusiv casuta postala | CONFIRMAT | Ghid p.31 |
| FATCA-IND-4 | Telefon actual din SUA, FARA conditia "niciun telefon RO" | CONFIRMAT | Ghid p.31 |
| FATCA-IND-5 | Ordine permanente catre un cont din SUA, fara exceptia pentru conturile de depozit | CONFIRMAT | Ghid p.31 |
| FATCA-IND-6 | Procura sau drept de semnatura valabil, pentru o persoana cu adresa in SUA | CONFIRMAT | Ghid p.31 |
| FATCA-IND-7 | Adresa "in grija" sau "pastrare de corespondenta" ca singura adresa | CONFIRMAT, cu exceptie: la contul preexistent de valoare mica, c/o din afara SUA nu e indiciu | Ghid p.31 |
| FATCA-NEW-1 | Cont nou: declaratie plus rezonabilitate; cetateanul SUA e rezident fiscal SUA chiar daca e rezident si in alta parte | CONFIRMAT | Ghid p.38 |
| FATCA-NEW-2 | Rezident SUA: declaratie cu TIN SUA, de exemplu W-9 | CONFIRMAT | Ghid p.38 |
| FATCA-SC-1 | Schimbare de situatie: declaratie noua; fara ea, cont raportabil SUA | CONFIRMAT | Ghid p.38 |
| FATCA-CURE | Documentele care pot inlatura un indiciu SUA (exceptiile B(4) din Acordul FATCA) | NEVERIFICAT: ghidul doar trimite la Acord | Ghid p.31 |
| FATCA-W8-1 | W-8BEN valabil de la semnare pana la sfarsitul celui de-al treilea an calendaristic urmator, daca nu intervine o schimbare | CONFIRMAT din IRS, nu din ghidul ANAF; aplicarea in Acordul FATCA Model 1 NEVERIFICATA | IRS, Instructions for Form W-8BEN, "Expiration of Form W-8BEN" |

## Alte afirmatii din brief

| Afirmatie | Stare |
|---|---|
| Nationalitatea nu e indiciu CRS; cetatenia SUA e indiciu FATCA | CONFIRMAT prin listele de la p.29 si p.31 |
| Salariul din alta tara nu e indiciu | CONFIRMAT: nu apare in nicio lista |
| CRS se aplica prin DAC2, transpusa in Legea 207/2015; DAC8 transpusa prin OUG 71/2025 | CONFIRMAT, Ghid p.4 |
| Formularul F3000 si termenul 15 mai | NEVERIFICAT: nu apar in ghid; de verificat in OPANAF 1711/2022 |
| Cuantumul amenzilor | NEVERIFICAT in ghid: ghidul citeaza OUG 102/2022 doar ca baza legala; cifrele vin din EY, sursa secundara |
| Retinerea de 30% FATCA | NEVERIFICAT: nu apare in ghid |
| TIN on Europa verifica doar structura si sintaxa | CONFIRMAT prin apel real: raspunsul contine doar validStructure si validSyntax |
