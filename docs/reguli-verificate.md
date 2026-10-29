# TRACE: reguli verificate

Sursa primară: Ghidul ANAF DAC2/CRS/FATCA, varianta 29.12.2025 (79 de pagini). Paginile sunt paginile PDF, identice cu numerotarea tipărită.
Verificat la 28.10.2026 pe textul extras din PDF.

Stare: CONFIRMAT = text găsit în ghid. CORECTAT = ghidul spune altceva decât lista de lucru. NEVERIFICAT = nu apare în ghid.
CONFIRMAT din lege / din Acordul FATCA / din IRS = text găsit în actul numit (articol și URL), nu în ghid. INTERPRETARE = regulă de design, spusă deschis în README. CONFIRMAT prin apel real = răspunsul serviciului, nu un text din ghid sau din lege.

## ID-urile de lucru și ID-urile din cod

ID-urile din tabelele de mai jos sunt cele de lucru, din ziua verificării. Codul folosește ID-urile finale din
`src/HackathonStarter/Features/Rules/RuleCatalog.cs`. Fiecare pereche are aceeași literă sau aceeași pagină din ghid și
același conținut:

| ID de lucru (aici) | ID în cod | Unde |
|---|---|---|
| CRS-IND-A | CRS-IND-RES | p. 29, lit. a |
| CRS-IND-B | CRS-IND-ADDR (adresa de reședință) și CRS-IND-MAIL (adresa de corespondență) | p. 29, lit. b |
| CRS-IND-C | CRS-IND-PHONE | p. 29, lit. c |
| CRS-IND-D | CRS-IND-SO | p. 29, lit. d |
| CRS-IND-E | CRS-IND-POA | p. 29, lit. e |
| CRS-IND-F | CRS-IND-CO | p. 29, lit. f |
| CRS-SC-1 | GEN-CHANGE-CIRC | p. 30 |
| CRS-SC-2 | DL-NEW-90 (termenul) și DL-EXPIRED-NEW (consecința) | p. 35–36 |
| CRS-AC-1 | SC-VALID | p. 34 |
| CRS-TIN-1 și CRS-RZ-3 | SC-TIN-MISSING | p. 53; p. 37 |
| CRS-RZ-6 | RESP-EXPLANATION | p. 37 |
| FATCA-IND-1 … FATCA-IND-7 | FATCA-IND-CIT, FATCA-IND-POB, FATCA-IND-ADDR, FATCA-IND-PHONE, FATCA-IND-SO, FATCA-IND-POA, FATCA-IND-CO (în această ordine) | p. 31 |
| FATCA-NEW-2 | FATCA-TIN | p. 38 |
| FATCA-SC-1 | FATCA-SC | p. 38 |

Rândurile care nu apar în tabelul de mai sus nu au o pereche unu-la-unu în `RuleCatalog.cs`.

## Important pentru domeniul MVP

Lista de indicii (CRS-IND-*, FATCA-IND-*) este, în ghid, procedura pentru conturile PREEXISTENTE (căutarea în evidența electronică, p. 29 și p. 31).
Pentru conturile NOI, ghidul cere declarația pe proprie răspundere plus testul de rezonabilitate față de informațiile de la deschidere, inclusiv documentele KYC (p. 33).
În MVP folosim indiciile ca criterii prin care banca „știe sau are motive să știe” că declarația e contrazisă (testul de rezonabilitate, schimbarea de situație). Aceasta este interpretarea noastră de design și se spune deschis în README și în UI.

## CRS / DAC2, persoane fizice

| ID | Regula | Stare | Sursa |
|---|---|---|---|
| CRS-IND-A | Titularul identificat drept rezident al unei jurisdicții raportabile | CONFIRMAT | Ghid p. 29, lit. a; Legea 207/2015 Anexa 1 Sect. III subsect. B |
| CRS-IND-B | Adresa de corespondență sau de rezidență actuală, inclusiv căsuța poștală, într-o jurisdicție raportabilă | CONFIRMAT | Ghid p. 29, lit. b |
| CRS-IND-C | Unul sau mai multe telefoane dintr-o jurisdicție raportabilă ȘI niciun telefon în România | CONFIRMAT | Ghid p. 29, lit. c |
| CRS-IND-D | Instrucțiuni permanente de transfer către un cont dintr-o jurisdicție raportabilă, ALTELE DECÂT cele privind un cont de depozit | CORECTAT: nu se aplică la conturile de depozit | Ghid p. 29, lit. d |
| CRS-IND-E | Împuternicire sau delegare de semnătură valabilă, acordată unei persoane cu adresa într-o jurisdicție raportabilă | CONFIRMAT | Ghid p. 29, lit. e |
| CRS-IND-F | Adresă „post-restant” sau „în atenția” într-o jurisdicție raportabilă, când nu există nicio altă adresă la dosar | CONFIRMAT, cu precizare: doar în jurisdicție raportabilă | Ghid p. 29, lit. f; efect p. 31 (căutare pe hârtie sau declarație, altfel cont nedocumentat) |
| CRS-IND-EFFECT | Indiciu găsit: titularul e tratat ca rezident în fiecare jurisdicție raportabilă cu indiciu, cu excepțiile de la pct. 19 | CONFIRMAT | Ghid p. 30 |
| CRS-IND-FALSE | Indiciile pot fi "false"; se recomandă contactarea clientului pentru clarificare | CONFIRMAT | Ghid p. 30 |
| CRS-RZ-1 | Adresa din declarație contrazice documentele KYC: testul de rezonabilitate eșuează | CONFIRMAT | Ghid p. 36, Exemplul 1 |
| CRS-RZ-2 | Adresa de rezidență din declarație nu e în jurisdicția de rezidență fiscală declarată: eșuează | CONFIRMAT | Ghid p. 37, Exemplul 2 |
| CRS-RZ-3 | Declarație fără TIN, deși portalul OCDE arată că jurisdicția emite TIN tuturor rezidenților: declarație nesigură | CONFIRMAT | Ghid p. 37 |
| CRS-RZ-4 | Declară că nu are rezidență fiscală, dar dosarul conține o adresă: motiv de îndoială | CONFIRMAT | Ghid p. 37 |
| CRS-RZ-5 | Discrepanțele dintre declarație și dosarul principal NU sunt eroare fără importanță | CONFIRMAT | Ghid p. 36 |
| CRS-RZ-6 | Explicații rezonabile pentru adresa diferită: student, profesor sau stagiar, diplomat, funcționar UE | CONFIRMAT | Ghid p. 37 |
| CRS-AC-1 | Declarația e valabilă doar dacă e semnată sau confirmată, datată și conține: nume, adresa de rezidență, jurisdicția sau jurisdicțiile, TIN pentru fiecare jurisdicție raportabilă, data nașterii | CONFIRMAT | Ghid p. 34 |
| CRS-AC-2 | Confirmarea pozitivă a declarației se înregistrează: pași, moment, canal, dovezi | CONFIRMAT | Ghid p. 34-35 |
| CRS-AC-3 | Mai multe jurisdicții declarate: cont raportabil pentru fiecare jurisdicție raportabilă | CONFIRMAT | Ghid p. 33-34 |
| CRS-SC-1 | Schimbare de situație: orice informație adăugată sau contradictorie relevantă, inclusiv la conturile asociate | CONFIRMAT | Ghid p. 30 |
| CRS-SC-2 | Declarația își pierde valabilitatea când banca știe sau are motive să știe de schimbare; 90 de zile pentru declarație nouă sau explicație; altfel rezident în jurisdicția inițială ȘI în cea nouă | CONFIRMAT | Ghid p. 35-36 |
| CRS-SC-3 | Declarația CRS nu expiră prin simpla trecere a timpului | NEVERIFICAT ca text explicit: ghidul numește doar schimbarea de situație drept cauză | Ghid p. 35 |
| CRS-TIN-1 | TIN lipsă: motivul A (țara nu emite), B (nu poate obține, cu explicație), C (TIN-ul nu e cerut de legea internă) | CONFIRMAT | Ghid p. 53, Anexa I, model de declarație |
| CRS-TIN-2 | Nu există obligația raportării TIN dacă jurisdicția nu emite; există echivalente funcționale | CONFIRMAT | Ghid p. 48 |
| CRS-ADR-1 | Adresa „post restant” sau „în atenția” nu e adresă de rezidență | CONFIRMAT | Ghid p. 48 |

## FATCA, persoane fizice

| ID | Regula | Stare | Sursa |
|---|---|---|---|
| FATCA-IND-1 | Cetățean sau rezident SUA | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-2 | Loc al nașterii în SUA, indicat fără ambiguitate | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-3 | Adresa actuală de corespondență sau reședință în SUA, inclusiv căsuța poștală | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-4 | Telefon actual din SUA, FĂRĂ condiția "niciun telefon RO" | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-5 | Ordine permanente către un cont din SUA, fără excepția pentru conturile de depozit | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-6 | Procură sau drept de semnătură valabil, pentru o persoană cu adresă în SUA | CONFIRMAT | Ghid p. 31 |
| FATCA-IND-7 | Adresă „în grija” sau „păstrare de corespondență” ca singură adresă | CONFIRMAT, cu excepție: la contul preexistent de valoare mică, c/o din afara SUA nu e indiciu | Ghid p. 31 |
| FATCA-NEW-1 | Cont nou: declarație plus rezonabilitate; cetățeanul SUA e rezident fiscal SUA chiar dacă e rezident și în altă parte | CONFIRMAT | Ghid p. 38 |
| FATCA-NEW-2 | Rezident SUA: declarație cu TIN SUA, de exemplu W-9 | CONFIRMAT | Ghid p. 38 |
| FATCA-SC-1 | Schimbare de situație: declarație nouă; fără ea, cont raportabil SUA | CONFIRMAT | Ghid p. 38 |
| FATCA-CURE | Documentele care pot înlătura un indiciu SUA (excepțiile B(4) din Acordul FATCA) | NEVERIFICAT: ghidul doar trimite la Acord | Ghid p. 31 |
| FATCA-W8-1 | W-8BEN valabil, de regulă, de la semnare până la sfârșitul celui de-al treilea an calendaristic următor, dacă nu intervine o schimbare | CONFIRMAT din IRS, nu din ghidul ANAF; aplicarea în Acordul FATCA Model 1 NEVERIFICATĂ | IRS, Instructions for Form W-8BEN (Rev. 10/2021), "Expiration of Form W-8BEN", https://www.irs.gov/instructions/iw8ben |

## Alte afirmații din brief

| Afirmație | Stare |
|---|---|
| Naționalitatea nu e indiciu CRS; cetățenia SUA e indiciu FATCA | CONFIRMAT prin listele de la p. 29 și p. 31 |
| Salariul din altă țară nu e indiciu | CONFIRMAT: nu apare în nicio listă |
| CRS se aplică prin DAC2, transpusă în Legea 207/2015; DAC8 transpusă prin OUG 71/2025 | CONFIRMAT, Ghid p. 4 |
| Formularul F3000 și termenul 15 mai | CONFIRMAT din lege, nu din ghid: Codul de procedură fiscală, art. 62 alin. (7), https://legislatie.just.ro/Public/DetaliiDocument/312603; OPANAF 1711/2022 aprobă doar formularele F3000 DAC2/CRS și F3000 FATCA, https://legislatie.just.ro/Public/DetaliiDocument/259750 |
| Cuantumul amenzilor | CONFIRMAT din lege, nu din ghid: Codul de procedură fiscală, art. 336 alin. (2) lit. k^1) 2.000-5.000 lei și lit. k^2) 5.000-10.000 lei „pentru fiecare cont care face obiectul raportării”, https://legislatie.just.ro/Public/DetaliiDocument/312603 (introduse prin OUG 102/2022, https://legislatie.just.ro/Public/DetaliiDocument/256922) |
| Reținerea de 30% FATCA | CONFIRMAT din Acordul FATCA, nu din ghid: banca conformă nu e supusă reținerii (art. 4(1)); doar după o neconformare semnificativă nerezolvată în 18 luni de la notificare (art. 5(2)(b)), https://home.treasury.gov/system/files/131/FATCA-Agreement-Romania-5-28-2015.pdf; cota de 30% e în 26 U.S.C. 1471(a), https://www.govinfo.gov/content/pkg/USCODE-2023-title26/html/USCODE-2023-title26-subtitleA-chap4-sec1471.htm. Nu o spunem în pitch |
| Cont preexistent, schimbare de circumstanțe: cea mai târzie dintre 31.12 și +90 de zile | CONFIRMAT din lege pentru testul adresei de rezidență (Cod proc. fisc., Anexa nr. 2 pct. 1, https://legislatie.just.ro/Public/DetaliiDocument/312603, care transpune Directiva 2011/16/UE, Anexa II pct. 1, introdusă prin Directiva 2014/107/UE: "by the later of the last day of the relevant calendar year or other appropriate reporting period, or 90 calendar days following the notice or discovery of such change in circumstances", https://eur-lex.europa.eu/legal-content/EN/TXT/?uri=CELEX:32014L0107, citat verificat pe https://www.legislation.gov.uk/eudr/2011/16/annex/II la 28.10.2026; OCDE Sect. III pct. 13); INTERPRETARE la indiciile din căutarea electronică (Anexa nr. 1 Sect. III subsect. B pct. 16 spune doar „ori”; OCDE Sect. III pct. 16); ghidul p. 29 spune „sau”, în paragraful despre testul adresei de rezidență (nota 57); la căutarea electronică ghidul (p. 30) nu dă acest termen |
| TIN on Europa verifică doar structura și sintaxa | CONFIRMAT prin apel real: răspunsul conține doar validStructure și validSyntax |
