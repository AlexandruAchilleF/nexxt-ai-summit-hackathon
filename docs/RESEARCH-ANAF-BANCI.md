# Cercetare: cum interacționează ANAF cu băncile din România

Verificat la 28 octombrie 2026. Sinteză din surse primare (legislatie.just.ro, anaf.ro / static.anaf.ro, irs.gov, transfond.ro,
onpcsb.ro), cu surse secundare marcate (paginile băncilor, presă). Detaliile, citatele și articolele exacte sunt în cele cinci
anexe din [`anaf-banci/`](anaf-banci/):

| Anexă | Ce acoperă |
| --- | --- |
| [01-popriri.md](anaf-banci/01-popriri.md) | Executare silită pe conturi, e-Popriri, suspendare/ridicare, sume exceptate, poprire asigurătorie, scrisori de garanție bancară |
| [02-informatii.md](anaf-banci/02-informatii.md) | Raportarea zilnică a conturilor, registrul IBAN, cereri de rulaje/solduri, secret bancar, VSFP, antifraudă, ONPCSB → ANAF |
| [03-raportari.md](anaf-banci/03-raportari.md) | FATCA, CRS/DAC2 (F3000), DAC6, CESOP (F396), DAC8/DAC9, impozit reținut la sursă, banca contribuabil |
| [04-plati.md](anaf-banci/04-plati.md) | Plata obligațiilor prin bancă, Trezorerie, cont unic, OPT, corecții, restituiri, card/Ghișeul.ro |
| [05-digital.md](anaf-banci/05-digital.md) | API-uri ANAF pentru KYC, venituri cu acordul clientului, CAF, SPV, autenticitatea documentelor ANAF, NIF prin bancă |

**Limite de metodă.** Motoarele de căutare au blocat interogările automate, așa că documentele au fost găsite prin căutarea
internă legislatie.just.ro, navigarea site-urilor oficiale și Google News RSS. Codul de procedură fiscală (CPF) a fost citit în
consolidarea ANAF (actualizată până la Legea 170/2026) și în consolidarea legislatie.just.ro din 08.08.2026. Modificările
publicate după august 2026 pot lipsi. Am reverificat direct câteva puncte-cheie: art. 61 și 236 CPF, termenul de 15 mai, data
plății, OPANAF 3746/2020, OPANAF 146/2022, OPANAF 878/2022, pragul CESOP și cota de 10% pe dobânzi.

## Pe scurt

Relația ANAF – bancă are cinci roluri distincte ale băncii. Fiecare are alt temei legal, alt canal și alt ritm:

1. **Terț poprit** (executare silită). ANAF trimite electronic adresele de poprire (e-Popriri, OPANAF 878/2022). Banca blochează,
   raportează zilnic până la 17:00 suma disponibilă, iar ANAF alege ce bancă plătește (ordonanțare). Volum: circa 2,5 milioane
   de acte e-Popriri în 2024.
2. **Furnizor de informații.** Banca raportează **zilnic** conturile deschise/închise, cu IBAN, împuterniciți și beneficiari
   reali, în registrul central (art. 61–61^1 CPF, OPANAF 3746/2020, XML nou din aprilie 2026). La cerere, trimite rulaje,
   solduri și documente în 5, 10 sau 25 de zile lucrătoare, fără acordul clientului.
3. **Instituție financiară raportoare.** Depune anual F3000 (FATCA + CRS) până la 15 mai, trimestrial F396 (CESOP) și DAC6 în
   30 de zile. Datele AML (numerar și transferuri externe ≥ 10.000 euro) ajung la ANAF indirect, lunar, prin ONPCSB.
4. **Plătitor de venit și contribuabil.** Reține 10% pe dobânzi (D100 lunar, D205/D207 anual) și plătește impozitul pe cifra de
   afaceri (4% în 2026). Depune SAF-T și primește actele ANAF în SPV.
5. **Utilizator de date ANAF.** Verifică firmele prin API-ul public PlatitorTvaRest v9. Cu acordul clientului, interoghează
   veniturile și certificatul de atestare fiscală prin protocolul PJDP (OPANAF 146/2022). Verifică documentele ANAF aduse de
   client (VerifAdev) și cere NIF pentru nerezidenți.

Plus un rol de **intermediar de plată**: banca transmite plățile către Trezorerie (SENT/ReGIS). Nu poate modifica sau rechema o
plată executată; corecția trece prin ANAF (OMFP 164/2017).

## Harta interacțiunilor

| # | Flux | Direcție | Canal | Ritm / termen | Temei principal | Anexă |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Înființare poprire pe conturi | ANAF → bancă | e-Popriri, PDF+XML semnat | zilnic, încărcare până la 24:00 | CPF art. 236; OPANAF 878/2022 | 01 |
| 2 | Raportarea sumei ce poate fi plătită | bancă → ANAF | e-Popriri | până la 17:00 în ziua bancară următoare | art. 236 alin. (20); 878 pct. 3.2 | 01 |
| 3 | Ordonanțare de plată | ANAF → bancă | e-Popriri | până la 24:00; banca virează în aceeași zi | 878 pct. 3.5–3.8 | 01 |
| 4 | Virare în contul 50.67.02 de la Trezoreria Operativă Centrală, validare/retur | bancă → Trezorerie | OP cu număr de evidență a plății | retur în ziua lucrătoare următoare | art. 236 alin. (21); 878 pct. 4 | 01 |
| 5 | Actualizare sume / suspendare / continuare / ridicare | ANAF → bancă | e-Popriri | zilnic; OFC actualizează până la 11:00 | art. 230, 233, 234; 878 pct. 5–7 | 01 |
| 6 | Poprire asigurătorie | ANAF → bancă | adresă (OPANAF 63/2017) | indisponibilizare fără virare | art. 213–214, 236 alin. (15) | 01 |
| 7 | Executarea scrisorii de garanție bancare | ANAF → bancă emitentă | cerere scrisă | virare în max. 15 zile | art. 211, 212, 235; OPANAF 90/2016 | 01 |
| 8 | Conturi deschise/închise, casete de valori | bancă → ANAF | XML validat (portal) | zilnic | art. 61 alin. (2); OPANAF 3746/2020 + 393/2026 | 02 |
| 9 | Rulaje, solduri, documente la cerere | ANAF → bancă → ANAF | PDF+XML semnat | 5 / 10 / 25 zile lucrătoare | art. 61 alin. (1); 3746/2020 anexa 2 art. 4 | 02 |
| 10 | Cerere generală de informații de la terți | ANAF → bancă | formular OPANAF 3076/2017 | termenul din cerere | art. 58, 64 | 02 |
| 11 | Rapoarte AML de numerar, transferuri externe și remiteri | bancă → ONPCSB → ANAF | ONPCSB, lunar către ANAF | bancă: max. 3 zile lucrătoare | Legea 129/2019 art. 7; CPF art. 61 alin. (3) | 02 |
| 12 | Interogarea registrului IBAN | ANAF / ONPCSB / parchete / autorități → registru | sistem CNIF, PatrimVen | la cerere, jurnalizat | art. 61^1; OG 9/2021 | 02 |
| 13 | FATCA + CRS/DAC2 | bancă → ANAF → IRS / state | F3000 PDF+XML | anual, 15 mai | art. 62, 291; OPANAF 1711/2022 | 03 |
| 14 | DAC6 | bancă (intermediar) → ANAF | formular DAC6, e-guvernare | 30 de zile | art. 291^4; OPANAF 1029/2020 | 03 |
| 15 | CESOP | bancă → ANAF → UE | F396, e-guvernare | trimestrial, sfârșitul lunii următoare | Cod fiscal art. 321^2; OPANAF 825/2024 | 03 |
| 16 | DAC8 (criptoactive) | furnizor raportor → ANAF | F8000 | anual, 15 martie (din 2027) | art. 291^6; OPANAF 750/2026 | 03 |
| 17 | Impozit pe dobânzi, nerezidenți | bancă → ANAF | D100 / D205 / D207 | lunar până pe 25; anual în februarie | Cod fiscal art. 97, 132, 224, 230–231 | 03 |
| 18 | Plata obligațiilor clienților | client → bancă → Trezorerie | OPT, SENT (TREZROBU) | data plății = data debitării | CPF art. 163; OMFP 246/2005 | 04 |
| 19 | Corecția plății eronate | client → ANAF → Trezorerie | cerere (SPV pentru PJ) | soluționare în 45 de zile; drept 5 ani | art. 164; OMFP 164/2017 | 04 |
| 20 | Restituire / compensare | ANAF → Trezorerie → bancă | notă de restituire, OPT | 45 de zile; comunicare în 7 zile | art. 167–168; OMFP 1899/2004 | 04 |
| 21 | Verificare CUI la onboarding | bancă → API public | REST, fără autentificare | sincron, 1 cerere/s | API PlatitorTvaRest v9 | 05 |
| 22 | Venituri, situații financiare, CAF cu acord | bancă → ANAF → bancă | sistem PJDP, F2101/F2102/F2103 | răspuns în 1–3 zile lucrătoare | art. 11 alin. (3) lit. d), 70^1; OPANAF 146/2022 | 05 |
| 23 | NIF pentru nerezidenți la deschiderea contului | bancă → ANAF | aplicație online CNIF | 5 zile | art. 83 alin. (4) | 05 |
| 24 | Acte ANAF către bancă (ca contribuabil), declarații | ANAF ↔ bancă | SPV, e-guvernare.ro, SPVWS2 | comunicat la punerea la dispoziție | art. 47, 79; OPANAF 1090/2022 | 05 |

## Workflow-uri

Fiecare flux are pași detaliați, cu citate, în anexa indicată. Aici e versiunea scurtă.

### A. Executare silită (anexa 01)

**A1. Ciclul unei popriri e-Popriri** (D = ziua bancară în care banca primește adresa):

1. **Premisă.** Somația și titlul executoriu se comunică debitorului; el are 15 zile să plătească (art. 230 CPF). Poprirea bancară
   se emite peste anumite praguri, de exemplu peste 2.000 lei restanți la persoane fizice după 15 zile (OPANAF 2098/2019).
2. **D-1, până la 24:00.** ANAF generează adresele pentru fiecare bancă unde debitorul are conturi, aflate din raportarea zilnică
   a băncilor. Le încarcă în e-Popriri ca PDF cu XML, semnat de MF (878 art. 2, pct. 2.3).
3. **D, la începutul zilei bancare.** Adresa e comunicată, iar poprirea se consideră înființată. Banca înregistrează ziua și ora
   și blochează sumele existente și viitoare, în limita sumei din adresă (art. 236 alin. (8), (13)).
4. **D+1, până la 17:00.** Banca raportează suma care poate fi plătită (Anexa 5 din 878) sau motivul de neînființare: nu are
   cont, insolvență, cont închis, decedat/radiat (Anexa 4). După raportare nu mai face plăți din cont, nici pe cele exceptate
   (art. 236 alin. (20)).
5. **D+1, până la 24:00.** e-Popriri sortează sumele confirmate de toate băncile în ordine descrescătoare și emite
   ordonanțări. Doar băncile alese virează, iar ultima doar diferența (878 pct. 3.5–3.6).
6. **D+2.** Banca descarcă ordonanțarea și virează în aceeași zi în contul RO08TREZ999506702XXXXXXX, cu numărul de evidență a
   plății (pct. 3.8).
7. **Trezoreria Operativă Centrală** validează viramentul după numărul de evidență și CIF. Nu îl validează dacă suma rămasă e
   zero sau dacă poprirea e suspendată; viramentul se întoarce în ziua lucrătoare următoare (pct. 4.2–4.3).
8. **Actualizarea.** Organul fiscal trimite zilnic, până la 11:00, suma rămasă. e-Popriri păstrează minimul și anunță toate
   băncile. La zero se emite automat decizia de ridicare (pct. 6–7).
9. **Dacă banca nu virează după ordonanțare:** referat de nevirare, apoi răspundere solidară (art. 25 alin. (1) lit. b)) sau
   amendă (art. 336 alin. (1) lit. k)). O plată făcută cu încălcarea regulilor este nulă (art. 236 alin. (16)).

De ce există ordonanțarea: înainte, fiecare bancă plătea în 3 zile și se ajungea frecvent la plăți duble (Libra, secundar).
Mecanismul e obligatoriu pentru toate băncile din 01.10.2023 (OPANAF 626/2023).

**A2. Suspendare, continuare, ridicare.**

- **Suspendarea:** înlesnire la plată, garanție la contestație, decizia instanței, poprire care blochează activitatea (art. 233).
  Sumele viitoare nu se mai blochează. Sumele existente rămân blocate și pot plăti doar obligații fiscale și salarii (art. 233
  alin. (4)–(5)).
- **Continuarea:** reia indisponibilizarea și virarea.
- **Ridicarea:** se face doar prin e-Popriri. Dovada plății adusă de client nu deblochează o poprire ANAF (BCR, Libra, ING,
  secundar). Decizia de ridicare se emite în cel mult 2 zile de la încetare (art. 234).

**A3. Sume exceptate și cererea de deblocare a clientului.**

- **Ce nu se poprește:** fondurile nerambursabile (art. 236 alin. (2)). Salariile și pensiile intră doar în limitele Codului de
  procedură civilă (1/3 sau 1/2), iar alocațiile nu se popresc deloc.
- **Ce se mai poate plăti din contul poprit:** salarii (pe declarație pe propria răspundere), accize și obligațiile de care
  depinde o înlesnire (art. 236 alin. (14)), dar **doar până la raportarea de la 17:00**.
- De aceea băncile cer documentele devreme: ING până la 13:00, Libra până la 16:00, BCR procesează până la 16:30 (secundar).
- **Sumele virate din greșeală** se restituie chiar dacă clientul are restanțe (art. 168 alin. (12)). Excedentul se restituie din
  oficiu în 5 zile lucrătoare (art. 168 alin. (4)).

**A4. Poprire asigurătorie.**

1. Organul fiscal emite o decizie motivată, pentru pericol de sustragere (art. 213).
2. Banca blochează sumele fără să le vireze. Acceptă totuși plățile exceptate și plata obligațiilor fiscale către organul care a
   înființat poprirea (art. 236 alin. (15)).
3. Măsura încetează dacă titlul de creanță nu e emis în 6 luni, prelungibil la un an.
4. La scadență devine poprire executorie, cu starea „transformare” în e-Popriri.
5. Se ridică dacă debitorul constituie o garanție (art. 214; OPANAF 2546/2016).

**A5. Scrisoarea de garanție bancară în favoarea ANAF.**

1. **Unde se folosește:** suspendarea executării pe durata contestației (art. 235), ridicarea măsurilor asigurătorii,
   eșalonarea la plată și înlocuirea sechestrului. Formele admise sunt la art. 211.
2. **Conținut obligatoriu** (OPANAF 90/2016, anexa 2, art. 6 alin. (7)): angajament „necondiționat și irevocabil” de plată la
   cererea organului fiscal.
3. **Valabilitate:** minimum 6 luni la art. 235; la eșalonare, cu cel puțin 3 luni peste ultima rată.
4. **Executarea:** ANAF cere băncii virarea, iar banca are cel mult 15 zile (art. 212 alin. (2)). Dacă nu virează, banca răspunde
   solidar (art. 25 alin. (1) lit. d)).
   - La art. 235, garanția se execută după 30 de zile de la respingerea definitivă, dar nu mai târziu de ultima zi de
     valabilitate.
   - La măsurile asigurătorii, garanția se execută chiar în ultima zi de valabilitate, dacă nu e prelungită (art. 213
     alin. (8^1)).

### B. Informații transmise de bănci (anexa 02)

**B1. Raportarea zilnică în registrul IBAN.**

1. **Înrolarea:** administratorul băncii, cu certificat calificat, își înrolează utilizatorii (OPANAF 3746/2020, anexa 3).
2. **Ce se raportează zilnic** (art. 61 alin. (2)), ca XML validat după structura ANAF (din aprilie 2026, conform OPANAF 393/2026):
   - titularii care deschid sau închid conturi;
   - persoanele cu drept de semnătură;
   - persoanele care acționează în numele clientului;
   - beneficiarii reali;
   - IBAN-ul și datele de deschidere/închidere;
   - chiriașii casetelor de valori.
3. **Registrul** („Registrul central electronic pentru conturi de plăți și conturi bancare identificate prin IBAN”, art. 61^1)
   nu conține solduri. Îl gestionează ANAF, cu sistemul CNIF. Datele se păstrează 10 ani.
4. **Cine îl citește:**
   - ANAF;
   - ONPCSB, direct și nefiltrat;
   - autoritățile AML, pe bază de protocol;
   - autoritățile publice și locale prin PatrimVen, doar titular și IBAN;
   - poliția, parchetele, DNA, DIICOT, DLAF și ANABI, „de la caz la caz”, cu jurnal păstrat 5 ani (OG 9/2021).

**B2. Cererea punctuală de rulaje, solduri și documente.**

1. Structura ANAF află din registru la ce bănci are persoana conturi (deducție, nu pas prescris).
2. Trimite băncii prin portal o cerere PDF cu XML, semnată electronic. Cererea conține temeiul, identitatea clientului, categoria
   datelor și scopul (OUG 99/2006 art. 113 alin. (3)).
3. Banca răspunde tot ca PDF cu XML, în 5 zile lucrătoare (perioadă ≤ 3 ani), 10 zile lucrătoare (> 3 ani) sau 25 de zile
   lucrătoare (documente).
4. Nu e nevoie de acordul clientului. Codul nu dă băncii un drept de refuz; secretul bancar cedează prin art. 113 alin. (2) lit. c)
   din OUG 99/2006.
5. Datele primite devin secret fiscal și pot fi folosite doar pentru atribuțiile ANAF (art. 61 alin. (4)).

**B3. Verificarea situației fiscale personale (VSFP) și antifrauda.**

1. Analiza de risc semnalează o diferență de peste 10% din veniturile declarate și de cel puțin 50.000 lei (art. 138).
2. Persoana primește o notificare de conformare și are 30 de zile să corecteze.
3. Urmează avizul de verificare: extrasele bancare se prezintă în 60 de zile, sub sancțiunea decăderii (OPANAF 2117/2018).
4. ANAF poate cere datele și direct băncilor și folosește metode indirecte (OPANAF 675/2018). Veniturile nejustificate se
   impozitează cu 70%.
5. **Antifrauda (DGAF)** controlează fără anunț prealabil (art. 136–137^1) și, din 06.07.2026, poate face și VSFP (OPANAF
   768/2026).
6. Rezultate VSFP iulie 2025 – mai 2026: 1.102 controale și 540 milioane lei creanțe suplimentare (economica.net, secundar).

**B4. Fluxul AML către ANAF.**

1. Banca raportează la ONPCSB, în cel mult 3 zile lucrătoare:
   - tranzacțiile în numerar ≥ 10.000 euro;
   - transferurile externe ≥ 10.000 euro;
   - remiterile de bani ≥ 2.000 euro.
2. ONPCSB le trimite **lunar** la ANAF (art. 61 alin. (3)). Din 2019 acest flux a înlocuit raportarea directă a băncilor către
   ANAF.
3. Separat, ONPCSB trimite informări spontane: 221 + 21 către ANAF în 2025.

### C. Raportări automate (anexa 03)

**C1. FATCA și CRS (același formular, F3000).**

1. **La deschiderea contului**, banca obține declarația pe propria răspundere privind rezidența fiscală și NIF-ul și o verifică
   prin testul de rezonabilitate față de datele KYC. Pentru entități, stabilește dacă sunt ENF pasive și cine le controlează.
2. **Până la 15 mai**, banca depune F3000 (PDF cu XML, semnat calificat; `tip_raportare` 1 = FATCA, 2 = CRS). Raportarea nulă e
   obligatorie (CPF art. 62 alin. (7); OPANAF 1711/2022).
3. **ANAF transmite datele** la IRS prin IDES și statelor partenere, în 9 luni de la sfârșitul anului.
4. **Cererile de corecție** de la IRS sau de la alte state ajung în SPV-ul băncii, iar banca depune un F3000 de corecție.
5. **Amenzi:** 2.000–5.000 lei pe cont pentru raportare incorectă și 5.000–10.000 lei pe cont pentru diligență neaplicată
   (art. 336).

**C2. DAC6.** Banca raportează doar dacă e intermediar într-un aranjament transfrontalier cu semne distinctive. Execuția unui
ordin de plată de rutină nu o face intermediar (ghidul ANAF). Raportarea se face în 30 de zile, prin e-guvernare.

**C3. CESOP (formularul 396).**

1. Banca numără plățile transfrontaliere pe fiecare beneficiar. Obligația apare la **peste 25** într-un trimestru (Cod fiscal
   art. 321^2); pagina ANAF spune „cel puțin 25”, dar legea prevalează.
2. Depune XML-ul CESOP prin e-guvernare până la 30.04, 31.07, 31.10 și 31.01.
3. Rezultatul validării din sistemul central UE ajunge în SPV.
4. Corecțiile se depun ca CESOP101 sau CESOP100. Campania de corecție are termen 31.12.2026.

**C4. DAC8.**

- Furnizorii de servicii de criptoactive raportori depun F8000 până la 15 martie, prima dată pentru anul 2026 (OPANAF 750/2026).
- Pentru bănci contează mai ales extinderea CRS la moneda electronică și declarațiile combinate CRS+CARF.

**C5. Reținerea la sursă.**

- **Dobânzi:** 10%, impozit final, reținut la creditare. Se declară în D100 până pe 25 a lunii următoare. D205, pe beneficiari,
  se depune până în ultima zi a lunii februarie.
- **Nerezidenți:** 16% în general, 10% pentru persoane fizice din UE sau din state cu convenție de evitare a dublei impuneri.
  Cota din convenție se aplică doar dacă certificatul de rezidență fiscală e prezentat la plată; certificatul e valabil și 60 de
  zile din anul următor. Se declară în D207, până în februarie.
- **Dividende:** 16% din 2026.

**C6. Banca contribuabil.**

- Impozitul pe cifra de afaceri (Cod fiscal art. 46^1) e 4% în 2026 (2% pentru bănci cu cotă de piață sub 0,2%). Se declară în
  D100 la poziția 94, trimestrial.
- Impozitul pe profit se declară anual, cu plăți anticipate trimestriale.
- SAF-T (D406) e obligatoriu pentru bănci din 01.01.2023.

### D. Plăți către stat (anexa 04)

**D1. Plata unei obligații prin bancă.**

1. **Clientul completează OPT-ul:**
   - Beneficiar: „bugetul general consolidat”;
   - CIF beneficiar: CIF-ul contribuabilului;
   - IBAN Trezorerie: contul unic 55.03 pentru PJ (din 01.07.2018) sau contul distinct; TVA și CAM nu intră în contul unic;
   - numărul de evidență a plății: doar pentru amenzi, obligații din inspecție, popriri și alte cazuri prevăzute (OPANAF
     3607/2016).
2. **Ordinul e irevocabil** după primire (Legea 209/2019 art. 190). Banca îl decontează prin SENT, unde Trezoreria participă ca
   TREZROBU. Trezoreria nu apare în lista Plăți Instant (inferență: nu se plătește instant).
3. **Data plății** este data debitării contului plătitorului, din mesajul băncii (CPF art. 163 alin. (11) lit. d)). Dacă banca
   nu decontează în 3 zile lucrătoare, contribuabilul datorează accesorii, dar are regres împotriva băncii (art. 177).
4. **ANAF distribuie suma din contul unic** în această ordine:
   1. ajutoarele de stat, prioritar din 2025;
   2. bugetele, proporțional;
   3. în fiecare buget: reținerile la sursă, apoi celelalte obligații principale, apoi accesoriile.
   Stingerea se face după vechime (art. 163–165; OPANAF 1613/2018).

**D2. Corecția plății greșite** (CIF greșit, alt cont, alt buget).

1. Banca nu poate modifica plata. Trezoreria creditează doar după CIF și IBAN (Regulamentul BNR 2/2016 art. 4).
2. Debitorul sau, la CIF greșit, plătitorul depune cererea la ANAF; PJ și PFA o depun prin SPV. Atașează OP-ul și extrasul.
   Dreptul se pierde în 5 ani.
3. ANAF verifică și răspunde în 45 de zile. Emite „Nota privind corecția erorilor”, iar Trezoreria stornează suma cu data plății
   inițiale (OMFP 164/2017).
4. Excepție: erorile operatorilor de decontare se corectează din oficiu sau la cererea băncii (art. 164 alin. (8)).

**D3. Restituirea și compensarea.**

1. Cererea se depune prin SPV pentru PJ. Compensarea restanțelor este obligatorie înainte (art. 167–168).
2. ANAF emite decizia și nota de restituire, iar Trezoreria plătește prin OPT în contul bancar. Comunicarea se face în 7 zile
   (OMFP 1899/2004).
3. Dobânda la restituirea întârziată e 0,02% pe zi, la cerere.

**D4. Card, Ghișeul.ro, POS, numerar.**

- Cardul online prin SPV sau Ghișeul.ro e disponibil „numai pentru persoanele fizice” (broșura ANAF 2024). Data plății e data
  autorizării.
- Acceptatorul virează la Trezorerie zilnic, în cel mult o zi lucrătoare (HG 1235/2010).
- PJ pot plăti cu cardul la POS-urile Trezoreriei (OMF 94/2022).

### E. Date ANAF folosite de bancă: KYC, credit, documente (anexa 05)

**E1. Verificarea unei firme la onboarding prin API.**

1. Cerere: `POST https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva`, cu JSON `[{"cui":N,"data":"AAAA-LL-ZZ"}]`, cel mult
   100 de CUI-uri pe cerere și 1 cerere pe secundă. Testat live pe 28.10.2026: HTTP 200. Versiunea v8 e retrasă.
2. Câmpuri utile:
   - denumire, adresa sediului social, numărul de la registrul comerțului, CAEN;
   - starea de inactiv/reactivat și data radierii;
   - TVA, cu temeiul anulării;
   - TVA la încasare (RTVAI), split TVA, registrul RO e-Factura.
3. Semnale: inactivitatea produce efecte față de terți din ziua următoare înscrierii (art. 92). Din 18.12.2025, o PJ fără cont de
   plăți în România poate fi declarată inactivă.
4. Limite: răspunsul nu e semnat, deci rămâne evidență internă. Nu dovedește beneficiarii reali, lipsa datoriilor sau
   autenticitatea actelor.
5. Servicii conexe: registrul RO e-Factura, bilanțul pe ani (întoarce și 2024), varianta asincronă.

**E2. Veniturile clientului, cu acordul lui (protocolul PJDP).**

1. Banca semnează cu ANAF protocolul după modelul din OPANAF 146/2022. Are nevoie de audit IT extern anual și de utilizatori
   înrolați cu certificat calificat.
2. Clientul dă acordul: olograf, cu semnătură electronică sau prin acceptare în aplicație, generată ca PDF cu nume, dată, oră și
   IP. **Acordul e valabil 5 zile lucrătoare.**
3. Banca încarcă cererea (XML F2101 într-un PDF semnat) împreună cu acordul.
4. ANAF răspunde cu F2102 (veniturile persoanei fizice sau PFA pe 3 ani) sau F2103 (firme). Termenul e de 3 zile lucrătoare în
   protocol și 1 zi lucrătoare în procedura tehnică, deci contradictoriu.
5. Banca păstrează acordurile și jurnalele 8 ani. Clientul poate vedea în SPV interogările făcute de bănci.
6. Practica de piață (secundar): Raiffeisen cere „acordul de consultare a veniturilor din baza de date ANAF”, ING îl semnează în
   Home'Bank, iar CEC verifică automat salariații și pensionarii.
7. **Proiect din septembrie 2026, neadoptat la 28.10.2026:**
   - firmele ar da acordul numai prin SPV;
   - băncile ar primi și D100, D101 și D300 pe ultimele 12 luni, doar pentru riscul de credit și frauda la creditare;
   - persoanele fizice fără SPV ar semna în continuare la bancă.
   Sursa este Financiarul, 12.09.2026 (secundar); textul proiectului nu a fost citit.

**E3. Certificatul de atestare fiscală (CAF).**

- Banca îl poate cere, cu acordul clientului, prin sistemul dedicat băncilor de pe portalul ANAF (OPANAF 3654/2015 modificat
  prin 3008/2019). Alternativ, îl aduce clientul din SPV.
- Se emite în cel mult 3 zile lucrătoare și poate fi folosit 30 de zile (90 pentru persoane fizice fără activitate) (art. 158).

**E4. Verificarea unui document ANAF adus de client** (adeverință de venit, CAF, cazier fiscal, certificat de rezidență).

1. Se cere PDF-ul original din SPV; scanarea pierde semnătura.
2. Se validează semnătura. Rădăcina e „MFP Autoritate Certifier”, autosemnată și valabilă până în 2037. Nu e pe lista de
   încredere eIDAS, deci trebuie configurată manual.
3. Se verifică la sursă în https://www.anaf.ro/VerifAdev/, cu numărul de înregistrare și CNP/CIF. Aplicația are captcha, deci
   pasul rămâne manual.
4. Documentul se acceptă doar dacă semnătura e validă **și** documentul obținut de la ANAF coincide cu cel prezentat.
5. Pentru o e-factură, originalul legal este XML-ul cu sigiliul MF (OUG 120/2021 art. 4).

**E5. NIF pentru nerezidenți.** La deschiderea contului, banca cere ANAF atribuirea NIF/CIF, iar ANAF răspunde în 5 zile prin
aplicația online (art. 83 alin. (4)).

**E6. Banca în SPV, ca persoană juridică.**

- Comunicarea electronică e obligatorie din 01.03.2022; documentele depuse pe hârtie nu se iau în considerare (art. 79
  alin. (1^1)).
- Declarațiile se depun prin e-guvernare.ro, cu certificat calificat.
- Actele ANAF se consideră comunicate la data punerii la dispoziție în SPV (art. 47 alin. (15)) și rămân acolo 60 de zile,
  deci trebuie descărcate și arhivate.
- Mesajele se pot prelua automat prin `SPVWS2`.

## Calendarul unei bănci

| Termen | Obligație | Temei |
| --- | --- | --- |
| zilnic | Conturi deschise/închise și casete de valori, în registrul IBAN | CPF art. 61 alin. (2); OPANAF 3746/2020 |
| zilnic, până la 17:00 | Suma ce poate fi plătită pentru fiecare poprire e-Popriri | CPF art. 236 alin. (20); OPANAF 878/2022 |
| zilnic, la deschidere | Descărcarea ordonanțărilor și virarea în aceeași zi | OPANAF 878/2022 pct. 3.8 |
| max. 3 zile lucrătoare | Rapoarte de numerar și transferuri externe către ONPCSB | Legea 129/2019 art. 7 |
| 5 / 10 / 25 zile lucrătoare | Răspunsuri la cererile ANAF de rulaje, solduri, documente | OPANAF 3746/2020 anexa 2 art. 4 |
| max. 15 zile de la cerere | Virarea sumei din scrisoarea de garanție executată | CPF art. 212 alin. (2) |
| 25 ale fiecărei luni | D100: impozit pe dobânzi, rezidenți și nerezidenți | Cod fiscal art. 97, 224 |
| 25 după T1–T3, 25 martie pentru T4 | Impozitul pe cifra de afaceri (D100 poz. 94) | Cod fiscal art. 46^1; OPANAF 193/2025 |
| ultima zi a lunii februarie | D205 și D207 | Cod fiscal art. 132, 231 |
| 15 martie (din 2027) | F8000, dacă banca e furnizor raportor de criptoactive | CPF anexa 6; OPANAF 750/2026 |
| 15 mai | F3000 FATCA + CRS, inclusiv raportarea nulă | CPF art. 62 alin. (7) |
| 30.04 / 31.07 / 31.10 / 31.01 | F396 CESOP | OPANAF 825/2024 |
| 30 de zile de la declanșare | Raport DAC6 | CPF art. 291^4 |
| 31.12.2026 | Campania ANAF de corecție a datelor CESOP | instrucțiunile ANAF de corecție |

## Amenzi și răspundere

| Faptă | Sancțiune | Temei |
| --- | --- | --- |
| Încălcarea art. 61 (conturi, informații la cerere) | 1.000–5.000 lei (contribuabili mijlocii și mari) | CPF art. 336 alin. (1) lit. i)–j), alin. (2) lit. d) |
| Nerespectarea obligațiilor terțului poprit | 1.000–5.000 lei, respectiv 500–1.000 lei | art. 336 alin. (1) lit. k) |
| Sume sustrase indisponibilizării | răspundere solidară cu debitorul | art. 25 alin. (1) lit. b) |
| Neplata scrisorii de garanție | răspundere solidară | art. 25 alin. (1) lit. d) |
| Plăți făcute cu încălcarea popririi | nulitatea plății | art. 236 alin. (16) |
| CRS/FATCA: raportare întârziată sau incorectă | 2.000–5.000 lei pe cont | art. 336 lit. ț), k^1) |
| CRS/FATCA: diligență neaplicată | 5.000–10.000 lei pe cont | lit. ț^1), k^2) |
| CRS/FATCA: neraportare, evidențe, documente | 20.000–100.000 lei | lit. ț^2)–ț^4), k^3) |
| Refuzul datelor AML cerute de ANAF (art. 62^1) | 20.000–100.000 lei | lit. ț^5), k^5) (OUG 71/2025) |
| DAC6: neraportare sau întârziere | 20.000–100.000 lei | lit. w), n) |
| Rapoarte ONPCSB (numerar, transferuri externe) | 10.000–90.000 lei, maximul crescut cu 10% din venituri | Legea 129/2019 art. 43 |
| CESOP | nu am găsit o contravenție specifică | neverificat |

## Cronologie 2015–2026

| Data | Ce s-a schimbat |
| --- | --- |
| 03.11.2015 | Intră în vigoare acordul FATCA România–SUA (Legea 233/2015) |
| 01.01.2016 | Noul CPF: raportarea zilnică a conturilor și cererile punctuale (art. 61) |
| 25.04.2016 | Legea 70/2016 ratifică acordul multilateral CRS |
| 2017 | OMFP 691/2017: popririle comunicate băncilor prin portal, ca PDF cu XML |
| 01.07.2018 | Contul unic pentru PJ (OPANAF 1612–1613/2018) |
| 07.2019 | Legea 129/2019: rapoartele de numerar și transferuri externe ajung la ANAF prin ONPCSB, lunar |
| 01.02.2020 | Abrogarea plății defalcate a TVA (OUG 78/2019; dată calculată) |
| 03.2020 | OMFP 1665/2020 introduce ordonanțarea, doar pentru băncile cu convenție |
| 15.07.2020 | OUG 111/2020 (AMLD5): registrul IBAN, extinderea la instituțiile de plată și cele de monedă electronică |
| 02.11.2020 | OPANAF 3746/2020 înlocuiește OPANAF 3770/2015; registrul e operațional din ianuarie 2021 (presă) |
| 2021 | e-Popriri intră în funcțiune |
| 31.08.2021 | OG 9/2021: poliția și parchetele au acces la registrul IBAN |
| 16.02.2022 | OPANAF 146/2022: protocolul ANAF – bănci pentru venituri, cu acordul clientului |
| 01.03.2022 | PJ sunt obligate să comunice electronic cu ANAF (SPV) |
| 11.05.2022 | OPANAF 878/2022 preia procedura e-Popriri |
| 29.09.2022 | OPANAF 1711/2022: F3000 comun FATCA/CRS |
| 01.01.2023 | SAF-T obligatoriu pentru bănci |
| 01.10.2023 | Ordonanțarea e-Popriri devine obligatorie pentru toate băncile (OPANAF 626/2023) |
| 2024 | CESOP (Legea 33/2024, OPANAF 825/2024); impozit pe cifra de afaceri 2% (art. 18^2, Legea 296/2023) |
| 2025 | Impozitul pe cifra de afaceri trece la art. 46^1 (Legea 290/2024; 4% din 07.2025, Legea 141/2025) |
| 10.12.2025 | OUG 71/2025: DAC8, CRS amendat, sancțiuni reformulate |
| 18.12.2025 | PJ fără cont de plăți în România pot fi declarate inactive |
| 01.01.2026 | Dividende 16%; câmpuri CRS noi pentru 2026 |
| 01.04.2026 | Noul XML pentru raportarea zilnică a conturilor (OPANAF 393/2026), cu 10 zile lucrătoare de tranziție |
| 06.07.2026 | DGAF poate face VSFP (OPANAF 768/2026); F8000 DAC8 (OPANAF 750/2026) |
| 12.09.2026 | Proiect ANAF de acord prin SPV pentru credite (presă; neadoptat la 28.10.2026) |

## Capcane și contradicții găsite

- **„FICOBA” nu e numele românesc.** FICOBA e fișierul francez (CNIL). În România se numește „Registrul central electronic
  pentru conturi de plăți și conturi bancare identificate prin IBAN”.
- **Registrul nu conține solduri.** Titlurile de presă de tipul „ANAF știe câți bani ai în cont” sunt inexacte. Mișcările ajung
  la ANAF doar prin cereri punctuale, prin ONPCSB (lunar, peste prag) și prin CESOP (trimestrial, transfrontalier). Nu există o
  raportare periodică a soldurilor interne.
- **OPANAF 3770/2015 este abrogat**, dar OPANAF 878/2022 art. 2 alin. (2) încă trimite la el.
- **Momentul comunicării prin SPV:** legea (art. 47 alin. (15), din 03.09.2022) spune „la data punerii la dispoziție”, iar
  OPANAF 1090/2022 spune „în 15 zile”. Prevalează legea.
- **Termenul ANAF în protocolul PJDP:** 3 zile lucrătoare în protocol și 1 zi lucrătoare în procedura tehnică, în același ordin.
- **Pragul CESOP:** legea spune „peste 25 de plăți”, pagina ANAF „cel puțin 25”.
- **Termenul F3000 este 15 mai**, nu 31 mai sau 30 iunie.
- **Cardul online în SPV:** broșura ANAF spune „numai pentru persoanele fizice”, dar OMFP 2809/2020 are o anexă pentru PJ.
- **OMFP 164/2017:** antetul indică MO 108 din 8 februarie 2017, iar nota din același document 8 ianuarie 2017.
- **Documentația API-ului de bilanț e învechită:** spune 2014–2019, dar API-ul întoarce date pentru 2024.

## Statistici publice

- **e-Popriri:**
  - 2.168.482 de acte în 2023;
  - 2.528.710 în 2024, din care 1.260.034 înființări (22,17 mld. lei) și 1.167.449 ridicări (rapoartele de performanță ANAF);
  - popririle pe conturi bancare au adus 3,9 mld. lei în 2023, circa 25% din încasările din executare silită, și 2,92 mld. lei
    în S1 2025.
- **ONPCSB 2025:** 29.082 de rapoarte de tranzacții suspecte, 27.644 de rapoarte de numerar și 8.745 de rapoarte de transferuri
  externe, aproape toate de la bănci.
- **VSFP iulie 2025 – mai 2026:** 1.102 controale, 540,2 mil. lei creanțe suplimentare și 123,5 mil. lei măsuri asigurătorii
  (secundar).

## Ce înseamnă pentru proiect

Aceasta este o inferență din surse, nu o validare cu mentorul. Pentru CRS Sentinel, partea direct relevantă este
[03-raportari.md](anaf-banci/03-raportari.md), W1–W2: F3000 până la 15 mai, raportarea nulă, corecțiile din SPV și amenzile
pe cont. Pentru reguli, sursa de adevăr rămâne ghidul ANAF din `docs/ghid/`.

- **Revizuirea KYC a unei firme atinge cel puțin trei fluxuri ANAF:**
  - verificarea CUI prin API: inactiv, radiat, adresă, TVA;
  - datele de beneficiar real și de împuterniciți, pe care banca le raportează zilnic în registrul IBAN (art. 61 alin. (2));
  - declarația CRS a entității (ENF pasivă, persoane care exercită controlul).
  Proiectul ANAF care a stat la baza OPANAF 393/2026 enumera ca erori frecvente „conturi fără beneficiar real” și „conturi ale
  nerezidenților fără NIF” (EVZ, secundar). O actualizare KYC corectă poate contribui deci la calitatea raportării către ANAF;
  dacă o modificare a beneficiarului real trebuie reraportată imediat nu am verificat în ordinul în vigoare.
- **Verificări care pot fi cod, nu model:**
  - termenele (5/10/25 zile, 17:00, 15 mai);
  - pragurile (peste 25 de plăți CESOP, 10.000 euro ONPCSB);
  - valabilitatea documentelor: CAF 30/90 zile, acordul PJDP 5 zile lucrătoare, certificatul de rezidență verificabil un an;
  - starea „inactiv” din API.
- **Ce nu poate face un agent:**
  - nu poate automatiza legitim VerifAdev, din cauza captcha-ului;
  - nu poate citi e-Popriri sau PJDP fără protocol și certificate;
  - nu poate decide deblocarea unei popriri pe baza unei dovezi de plată.
- **e-Popriri e deja structurat** (PDF+XML, termene la oră fixă). Munca manuală rămasă la bancă e în excepții:
  - clasificarea încasărilor neurmăribile înainte de 17:00;
  - cererile de deblocare venite pe canale diferite;
  - reconcilierea retururilor de la Trezorerie;
  - urmărirea valabilității scrisorilor de garanție.

## Catalogul documentelor

Legendă: **V** = în vigoare (consolidare verificată sau fără acte de abrogare găsite), **A** = abrogat (istoric), **?** =
statut neverificat. Linkurile duc la legislatie.just.ro (`DetaliiDocument/<id>`) sau la static.anaf.ro. Detaliile sunt în
anexe.

### Legi, coduri, ordonanțe

| Act | Ce reglementează în relația ANAF – bancă | St. | Link |
| --- | --- | --- | --- |
| Legea 207/2015, Codul de procedură fiscală | art. 11 secret fiscal; 25 răspundere solidară; 47 comunicare; 58–64 informații; 61–61^1 conturi și registru; 62 CRS/FATCA; 70^1 protocoale; 83 NIF; 92 inactivi; 138–147 VSFP; 158 CAF; 163–168 plăți, corecții, restituiri; 210–214 garanții și măsuri asigurătorii; 230–236 executare și poprire; 291–291^9 schimb automat; 336 amenzi | V | [170005](https://legislatie.just.ro/Public/DetaliiDocument/170005), [consolidare ANAF](https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm) |
| Legea 227/2015, Codul fiscal | art. 46^1 impozit pe cifra de afaceri a băncilor; 97 dobânzi și dividende; 132 D205; 224–232 nerezidenți și certificat de rezidență; 321^2 CESOP | V | [171282](https://legislatie.just.ro/Public/DetaliiDocument/171282) |
| OUG 99/2006, instituțiile de credit | art. 111–114 secret bancar și excepții (ANAF: art. 113 alin. (2) lit. c)) | V | [295420](https://legislatie.just.ro/Public/DetaliiDocument/295420) |
| Legea 129/2019, prevenirea spălării banilor | art. 7 rapoarte către ONPCSB; 15 date de identificare; 33 răspunsuri către ONPCSB; 43 amenzi; 55 fluxul către ANAF | V | [216157](https://legislatie.just.ro/Public/DetaliiDocument/216157) |
| OUG 111/2020 | transpune AMLD5: rescrie art. 61, introduce art. 61^1 (registrul IBAN) | V | [227817](https://legislatie.just.ro/Public/DetaliiDocument/227817) |
| OG 9/2021 (aprobată prin Legea 169/2022) | accesul poliției, parchetelor, DNA, DIICOT, DLAF și ANABI la registrul IBAN, cu jurnal | V | [245921](https://legislatie.just.ro/Public/DetaliiDocument/245921) |
| Legea 296/2023 | extinde accesul la registru; 70% pe venituri nejustificate; art. 18^2 impozit pe cifra de afaceri 2024 | V (art. 18^2 A) | [275745](https://legislatie.just.ro/Public/DetaliiDocument/275745) |
| OUG 71/2025 | DAC8/CARF, CRS amendat, sancțiuni art. 336 reformulate | V | [305122](https://legislatie.just.ro/Public/DetaliiDocument/305122) |
| OG 1/2026 | DAC9 (art. 291^7–291^9) | V | [306795](https://legislatie.just.ro/Public/DetaliiDocument/306795) |
| Legea 33/2024 | introduce CESOP în Codul fiscal (art. 321^2) | V | [279603](https://legislatie.just.ro/Public/DetaliiDocument/279603) |
| Legea 290/2024; Legea 141/2025 | impozitul pe cifra de afaceri al băncilor (art. 46^1), cote 2025–2026; dividende 16% | V | [291237](https://legislatie.just.ro/Public/DetaliiDocument/291237), [300022](https://legislatie.just.ro/Public/DetaliiDocument/300022) |
| Acordul FATCA România–SUA; Legea 233/2015 de ratificare | IGA Model 1: banca raportează la ANAF, ANAF transmite la IRS în 9 luni | V | [173045](https://legislatie.just.ro/Public/DetaliiDocument/173045), [172463](https://legislatie.just.ro/Public/DetaliiDocument/172463) |
| Legea 70/2016 | ratifică acordul multilateral CRS | V | [178026](https://legislatie.just.ro/Public/DetaliiDocument/178026) |
| Legea 209/2019, serviciile de plată | art. 190 irevocabilitate; 195 creditare; 200 IBAN greșit | V | [219736](https://legislatie.just.ro/Public/DetaliiDocument/219736) |
| Regulamentul BNR 2/2016 | transfer credit; Trezoreria identifică beneficiarul doar după CIF și IBAN (art. 4) | V | [181304](https://legislatie.just.ro/Public/DetaliiDocument/181304) |
| OUG 146/2002, resursele prin Trezorerie | conturile la Trezorerie, ordinea de utilizare a sumelor | V | [39860](https://legislatie.just.ro/Public/DetaliiDocument/39860) |
| HG 1235/2010, Ghișeul.ro | plata online, contul colector, virarea zilnică la Trezorerie | V | [124409](https://legislatie.just.ro/Public/DetaliiDocument/124409) |
| OUG 120/2021, RO e-Factura | XML-ul cu sigiliul MF este originalul (art. 4) | V | [247243](https://legislatie.just.ro/Public/DetaliiDocument/247243) |
| OUG 74/2013, reorganizarea ANAF | drepturile inspectorilor antifraudă (art. 6) | ? | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OUG_74_2013.pdf) |
| OG 31/2022 | comunicare SPV „la data punerii la dispoziție”; NIF prin bancă | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OG_31_2022.pdf) |
| OG 23/2017, plata defalcată a TVA; OUG 78/2019 de abrogare | conturile de TVA (istoric 2017–2020) | A | [192765](https://legislatie.just.ro/Public/DetaliiDocument/192765), [221169](https://legislatie.just.ro/Public/DetaliiDocument/221169) |
| Legea 202/2026, conturile de economii și investiții | băncile ca administratori; 10% la retragere din 2027 | V | [315218](https://legislatie.just.ro/Public/DetaliiDocument/315218) |
| Codul de procedură civilă, art. 729 și 781 (rep.) | limitele de urmărire a salariilor și pensiilor, sume neurmăribile | ? | [legeaz, secundar](https://legeaz.net/noul-cod-de-procedura-civila/art-728) |

### Executare silită, e-Popriri, garanții

| Act | Ce reglementează | St. | Link |
| --- | --- | --- | --- |
| OPANAF 878/2022 | procedura e-Popriri: înființare, sold disponibil, ordonanțare, virare, actualizare, suspendare, ridicare; anexele 3–7 cu modelele | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_878_2022.pdf), [254993](https://legislatie.just.ro/Public/DetaliiDocument/254993) |
| OPANAF 626/2023 | modifică 878: ordonanțare obligatorie din 01.10.2023, fără validare în timpul suspendării | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_626_2023.pdf) |
| OMFP 691/2017; OMFP 1665/2020; OMF 128/2022; OMF 1112/2022 | istoricul procedurii (portal, ordonanțare, contul 50.67.02, abrogare) | A | [691](https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_691_2017.pdf), [1665](https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_1665_2020.pdf), [128](https://static.anaf.ro/static/10/Anaf/legislatie/OMF_128_2022.pdf), [1112](https://static.anaf.ro/static/10/Anaf/legislatie/OMF_1112_2022.pdf) |
| OPANAF 2153/2019 | organul de executare pentru procese electronice masive (DGRCCB, „punctul central”) | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2153_2019.pdf) |
| OPANAF 727/2019 + 2098/2019 | pragurile de la care se emit somații și popriri bancare | V | [727](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_727_2019.pdf), [2098](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2098_2019.pdf) |
| OPANAF 63/2017 (+362/2022) | formulare: adresele de poprire asigurătorie și executorie pe conturi, ridicare | V | [63](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_63_2017.pdf), [362](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_362_2022.pdf) |
| OPANAF 967/2017 | formulare tipărite prin Unitatea de Imprimare Rapidă: adresa de poprire, suspendarea, continuarea | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_967_2017.pdf) |
| OPANAF 2546/2016 | măsurile asigurătorii, inclusiv poprirea asigurătorie pe conturi | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2546_2016.pdf) |
| OPANAF 1757/2019 | medierea înainte de executare (art. 230^1) | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1757_2019.pdf) |
| OPANAF 6138/2024 | cazurile speciale de executare silită (abrogă 3744/2015) | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_6138_2024.pdf) |
| OPANAF 90/2016 (+1960/2019) | eșalonarea; conținutul obligatoriu al scrisorii de garanție | ? | [90](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_90_2016.pdf), [1960](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1960_2019.pdf) |
| OPANAF 159/2024 | abrogă normele naționale pentru garanțiile în tranzitul vamal | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_159_2024.pdf) |
| Comunicate ANAF e-Popriri (10.07.2019; 10.11.2023) | mecanismul, sortarea descrescătoare, înștiințarea prin SPV sau poștă | – | [2019](https://static.anaf.ro/static/3/Anaf/20190710172941_poprireaelectronica.pdf), [2023](https://static.anaf.ro/static/3/Anaf/20231110125654_compopriri.pdf) |

### Informații, registru, control, AML

| Act | Ce reglementează | St. | Link |
| --- | --- | --- | --- |
| OPANAF 3746/2020 | registrul IBAN și procedura art. 61: raportare zilnică, termenele 5/10/25, înrolare; abrogă 3770/2015 | V | [231985](https://legislatie.just.ro/Public/DetaliiDocument/231985) |
| OPANAF 393/2026 | XML nou validat pentru raportarea zilnică, sediul real al PJ, tranziție de 10 zile lucrătoare | V | [308978](https://legislatie.just.ro/Public/DetaliiDocument/308978) |
| OPANAF 3770/2015 (+1106/2016, 3592/2016) | prima procedură art. 61 (STS, extranet, raportarea inițială 2010–2016) | A | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3770_2015.pdf) |
| OMFP 2632/2016 | PatrimVen pentru autoritățile publice | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_2632_2016.pdf) |
| OPANAF 3076/2017 | formularul „Cerere de furnizare a informațiilor/înscrisurilor” către terți | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3076_2017.pdf) |
| OPANAF 2117/2018 | formularele VSFP: aviz, extrase bancare în 60 de zile | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2117_2018.pdf) |
| OPANAF 2778/2020 (+1286/2022, 768/2026) | competența VSFP; DGAF din 06.07.2026 | V | [227433](https://legislatie.just.ro/Public/DetaliiDocument/227433), [312044](https://legislatie.just.ro/Public/DetaliiDocument/312044) |
| OPANAF 675/2018 (+417/2023, 91/2026) | metodele indirecte de stabilire a veniturilor; notificarea de conformare | V | [265963](https://legislatie.just.ro/Public/DetaliiDocument/265963) |
| Ordinul ONPCSB 191/2023 | formatul rapoartelor de numerar și transferuri externe (necitit) | ? | – |
| Raportul ONPCSB 2025 | statistici de raportare și informările către ANAF | – | [PDF](https://www.onpcsb.ro/uploads/articole/attachments/6a392cbe767d9833303636.pdf) |
| Planul strategic ANAF 2025–2028; Strategia PIT 2023–2025 | Big Data, folosirea datelor din conturile bancare în analiza de risc | – | [plan](https://static.anaf.ro/static/10/Anaf/Informatii_R/240917_%20Plan_%20StrategicANAF2025-2028.pdf), [strategie](https://static.anaf.ro/static/10/Anaf/Informatii_R/Strategia_PIT_23112022.pdf) |

### Raportări automate și reținere la sursă

| Act | Ce reglementează | St. | Link |
| --- | --- | --- | --- |
| OPANAF 1711/2022 | F3000 FATCA și CRS: PDF+XML, raportare nulă, corecții prin SPV | V | [259750](https://legislatie.just.ro/Public/DetaliiDocument/259750) |
| OPANAF 503/2016 | înțelegerea autorităților competente FATCA | V | [175422](https://legislatie.just.ro/Public/DetaliiDocument/175422) |
| OMFP 1939/2016 | vechiul cadru F3000 (structura XML încă trimite la el) | ? | [181124](https://legislatie.just.ro/Public/DetaliiDocument/181124) |
| Ghidul ANAF DAC2/CRS/FATCA (29.12.2025); pagina F3000 | diligență, declarații pe propria răspundere, XSD și validator | – | [ghid](https://static.anaf.ro/static/10/Anaf/AsistentaContribuabili_r/AEOI/Ghid%20DAC2%20CRS%20FATCAVARIANTA%2029.12.2025.pdf), [F3000](https://static.anaf.ro/static/10/Anaf/Declaratii_R/3000.html) |
| OPANAF 1029/2020 (+755/2025); ghidul DAC6 | formularul DAC6, banca ca intermediar | V | [225866](https://legislatie.just.ro/Public/DetaliiDocument/225866), [ghid](https://static.anaf.ro/static/10/Anaf/AsistentaContribuabili_r/GHID_DAC6_13012021.pdf) |
| OPANAF 825/2024 | formularul 396 CESOP | V | [282330](https://legislatie.just.ro/Public/DetaliiDocument/282330) |
| OPANAF 6508/2024 | formularul 709: înregistrarea prestatorilor de plăți nerezidenți | V | [289823](https://legislatie.just.ro/Public/DetaliiDocument/289823) |
| Pagina CESOP ANAF; instrucțiunile de corecție | XSD, validare, termene, campania 31.12.2026 | – | [pagină](https://www.anaf.ro/anaf/internet/ANAF/despre_anaf/strategii_anaf/proiecte_digitalizare/CESOP), [corecție](https://static.anaf.ro/static/10/Anaf/Informatii_R/CESOP/instructiuni%20de%20corectie.pdf) |
| OPANAF 750/2026; OPANAF 608/2026 | F8000 DAC8; controalele la furnizorii de criptoactive | V | [312052](https://legislatie.just.ro/Public/DetaliiDocument/312052) |
| OPANAF 179/2022 (ultima modificare 303/2026) | D205 și D207 | V | [251562](https://legislatie.just.ro/Public/DetaliiDocument/251562) |
| OPANAF 587/2016 (+193/2025) | D100; poziția 94 pentru impozitul pe cifra de afaceri al băncilor | V | [294623](https://legislatie.just.ro/Public/DetaliiDocument/294623) |
| OMFP 1099/2016 | rezidența fiscală a persoanelor fizice; certificatul de rezidență | V | [180514](https://legislatie.just.ro/Public/DetaliiDocument/180514) |
| OPANAF 1783/2021 | SAF-T (D406); bănci din 01.01.2023 | V | [248326](https://legislatie.just.ro/Public/DetaliiDocument/248326) |

### Plăți, Trezorerie, corecții, restituiri

| Act | Ce reglementează | St. | Link |
| --- | --- | --- | --- |
| OMFP 246/2005 (norme OPT, consolidare 2023) | completarea OPT și OPME, ora-limită la Trezorerie, CIF-ul Trezoreriei | V | [268643](https://legislatie.just.ro/Public/DetaliiDocument/268643) |
| OPANAF 1612/2018; OPANAF 1613/2018 | contul unic 55.03 pentru PJ: nomenclatorul și distribuirea | V | [202128](https://legislatie.just.ro/Public/DetaliiDocument/202128), [202137](https://legislatie.just.ro/Public/DetaliiDocument/202137) |
| OPANAF 2936/2018; OPANAF 2937/2018 | contul unic 55.04 pentru persoane fizice (Declarația unică) | V | [208042](https://legislatie.just.ro/Public/DetaliiDocument/208042), [208033](https://legislatie.just.ro/Public/DetaliiDocument/208033) |
| OPANAF 1281/2018 | CAM în contul distinct 20.A.47.03.00 | V | [201318](https://legislatie.just.ro/Public/DetaliiDocument/201318) |
| OPANAF 3607/2016 | numărul de evidență a plății (23 de cifre) și când se folosește | V | [184874](https://legislatie.just.ro/Public/DetaliiDocument/184874) |
| OMFP 164/2017 | îndreptarea erorilor din plăți: cerere, nota de corecție, stornare | V | [186434](https://legislatie.just.ro/Public/DetaliiDocument/186434) |
| OMFP 1899/2004 | restituiri: decizia și nota de restituire către Trezorerie | ? | [58349](https://legislatie.just.ro/Public/DetaliiDocument/58349) |
| OMFP 2809/2020; OMF 94/2022 | ce obligații se pot plăti cu cardul online și la POS-urile Trezoreriei | V | [231231](https://legislatie.just.ro/Public/DetaliiDocument/231231), [251291](https://legislatie.just.ro/Public/DetaliiDocument/251291) |
| Broșura ANAF „Modalități de plată” (2024); codurile IBAN | canalele de plată, structura IBAN Trezorerie | – | [broșură](https://static.anaf.ro/static/10/Anaf/AsistentaContribuabili_r/Brosura_plati_v2024.pdf), [IBAN](https://www.anaf.ro/anaf/internet/ANAF/asistenta_contribuabili/plata_oblig_fiscale/coduri_iban) |
| TransFonD: SENT, programul zilnic, participanți, Plăți Instant | decontarea bancă → Trezorerie (TREZROBU) | – | [SENT](https://www.transfond.ro/servicii/casa-de-compensare-automata-sent) |

### Canale digitale, KYC, documente

| Act / sursă | Ce reglementează | St. | Link |
| --- | --- | --- | --- |
| API PlatitorTvaRest v9 (documentație 04.02.2025) | starea fiscală a unei firme: TVA, inactiv, radiere, split TVA, e-Factura | V | [doc](https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/doc_WS_V9.txt) |
| API-urile pentru registrul RO e-Factura, bilanț, varianta asincronă | servicii publice conexe | V | [e-Factura](https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/docV1.txt), [bilanț](https://static.anaf.ro/static/10/Anaf/Informatii_R/doc_WS_Bilant_V1.txt) |
| Procedura OAuth ANAF; informațiile tehnice e-Factura | API-urile protejate, limitele de apeluri | V | [OAuth](https://static.anaf.ro/static/10/Anaf/Informatii_R/API/Oauth_procedura_inregistrare_aplicatii_portal_ANAF.pdf), [e-Factura](https://mfinante.gov.ro/ro/web/efactura/informatii-tehnice) |
| SPVWS2 (servicii web SPV); clientul MfpAnaf/ClientSPV | preluarea automată a mesajelor și documentelor din SPV | ? | [doc](https://static.anaf.ro/static/10/Anaf/Informatii_R/Prezentare_WS_SPV.txt) |
| OPANAF 146/2022 | protocolul ANAF – bănci: venituri, situații financiare, CAF, cu acord valabil 5 zile lucrătoare | V | [251680](https://legislatie.just.ro/Public/DetaliiDocumentAfis/251680) |
| OPANAF 3731/2016 (+3194/2019) | vechiul protocol ANAF – IFN | A | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3731_2016.pdf) |
| Proiectul ANAF de acord prin SPV (09.2026) | ar înlocui 146/2022; D100/D101/D300 pentru credit | proiect | [Financiarul, secundar](https://financiarul.ro/economie/bancile-ar-putea-verifica-veniturile-prin-anaf-cu-acord-dat-in-spv/) |
| OPANAF 1090/2022 | procedura SPV: identificare, semnătură/sigiliu, păstrare 60/100 de zile, verificare | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1090_2022.pdf) |
| OMFP 1154/2014; OMFP 660/2017; OMFP 1420/2022 | procedurile SPV anterioare și abrogarea lor | A | [1154](https://legislatie.just.ro/Public/DetaliiDocumentAfis/161005), [1420](https://legislatie.just.ro/Public/DetaliiDocumentAfis/256717) |
| OPANAF 2326/2017 | declarațiile care se depun obligatoriu electronic (100, 205, 207 etc.) | V | [PDF](https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2326_2017.pdf) |
| OPANAF 3654/2015 (+3008/2019) | certificatul de atestare fiscală, inclusiv cererea băncii prin sistemul dedicat | V | [174241](https://legislatie.just.ro/Public/DetaliiDocument/174241) |
| OMFP 583/2016 | certificatul de rezidență fiscală: formulare, verificabil un an | V | [178301](https://legislatie.just.ro/Public/DetaliiDocument/178301) |
| VerifAdev; lista documentelor verificabile | verificarea la sursă a documentelor ANAF din SPV | V | [VerifAdev](https://www.anaf.ro/VerifAdev/), [listă](https://static.anaf.ro/static/10/Anaf/spv/Lista_verificare_documente.pdf) |
| Catalogul serviciilor ANAF 2025 | fișe cu bază legală, canal și termen pentru fiecare serviciu | – | [PDF](https://static.anaf.ro/static/10/Anaf/Informatii_R/Catalogul_serviciilor_ANAF_2025.pdf) |
| Comunicate ANAF–ARB (protocolul pe credite din 2019; NIF pentru nerezidenți din 2022) | cooperarea ANAF – ARB | – | [2019](https://static.anaf.ro/static/10/Anaf/Informatii_R/ANAF_As_Rom_Banci_110322.pdf), [2022](https://static.anaf.ro/static/10/Anaf/Informatii_R/Comunicat_ARB_09022023.pdf) |

### Rapoarte ANAF și pagini ale băncilor (practică)

- Rapoartele de performanță și de activitate ANAF
  ([2023](https://static.anaf.ro/static/10/Anaf/Informatii_R/Raport_de_performanta_2023.pdf),
  [2024](https://static.anaf.ro/static/10/Anaf/Informatii_R/Raportperformanta2024.pdf),
  [S1 2025](https://static.anaf.ro/static/10/Anaf/Informatii_R/18.08.2025%20-%20Raport%20de%20activitateANAF_2025S1_V7.pdf)):
  volumele e-Popriri și încasările din executare.
- Băncile despre popriri (secundar):
  [ING PF](https://ing.ro/persoane-fizice/info/popriri), [ING IMM](https://ing.ro/imm/info/popriri),
  [Libra FAQ](https://www.librabank.ro/Intrebari_frecvente/Persoane_fizice_popriri),
  [BCR](https://www.bcr.ro/ro/persoane-fizice/informatii-utile/popriri), [BRD](https://www.brd.ro/contacteaza-ne/popriri),
  [BRD sold cert](https://www.brd.ro/brd-implementeaza-comunicarea-soldului-cert-catre-anaf), [CEC](https://www.cec.ro/contact-popriri),
  [Raiffeisen, comisionul de poprire](https://www.raiffeisen.ro/ro/persoane-fizice/in-sprijinul-tau/informatii-utile/informare-privind-platile-efectuate-in-executarea-unei-instructiuni-de-poprire.html).
- Băncile despre consultarea veniturilor la ANAF (secundar):
  [Raiffeisen](https://www.raiffeisen.ro/ro/persoane-fizice/produsele-noastre/credite/credit-de-nevoi-personale-fara-ipoteca.html),
  [ING](https://ing.ro/persoane-fizice/credite/ing-personal),
  [CEC](https://www.cec.ro/persoane-fizice/credite/credite-nevoi-personale/credit-online-de-nevoi-personale).

## Ce rămâne neverificat

- **Modificări după august 2026.** Consolidările citite se opresc la 08.08.2026 (legislatie.just.ro) și la Legea 170/2026 (ANAF).
  Nu am găsit modificări ale OPANAF 878/2022 după 626/2023, dar nu le pot exclude.
- **Documente nepublice:**
  - protocolul ANAF–ARB–bănci pentru e-Popriri;
  - schema XML e-Popriri;
  - specificațiile F2101/F2102/F2103;
  - protocoalele concrete ale Raiffeisen, ING și CEC cu ANAF.
- **Dacă o modificare de beneficiar real sau de împuterniciți se reraportează în registrul IBAN.** Ordinul în vigoare spune doar
  „zilnic” pentru conturile deschise și închise; ordinul vechi cerea explicit și modificările.
- **Canalul exact al cererilor de la art. 61** („portal”, fără nume). Nici canalul F3000 nu e numit explicit în ordin.
- **Detalii operaționale:** orele-limită la Trezorerie, pragul ReGIS, instrucțiunile SWIFT pentru plățile din străinătate și
  câmpul IBAN din cererea de restituire.
- **Alte puncte deschise:**
  - forma actuală a art. 729 și 781 din Codul de procedură civilă;
  - garanțiile vamale;
  - amenzile CESOP;
  - cotele impozitului pe cifra de afaceri după 2026;
  - textul proiectului din septembrie 2026.
- **Paginile BT și UniCredit** nu au putut fi citite (403 sau inexistente).
