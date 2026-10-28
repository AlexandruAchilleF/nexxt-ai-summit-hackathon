# 05 — Canale digitale ANAF ↔ bănci: API-uri pentru KYC, date de venit cu acord, SPV, autenticitatea documentelor ANAF

Cercetare făcută pe 2026-10-28. Sursele sunt primare (anaf.ro, static.anaf.ro, mfinante.gov.ro, legislatie.just.ro), cu excepția celor marcate **[secundar]**. Textul consolidat al Codului de procedură fiscală (CPF, Legea 207/2015) a fost citit în două variante: cea de pe ANAF, cu mențiunea „ultima actualizare: Legea nr. 170/2026” (https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm), și cea de pe legislatie.just.ro, consolidată la 08.08.2026 (https://legislatie.just.ro/Public/DetaliiDocument/170007).

Testele live sunt făcute de mine pe 2026-10-28, câte un apel, cu respectarea limitei de 1 cerere pe secundă:
- `POST https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva` cu `[{"cui":361820,"data":"2026-10-28"}]` → HTTP 200 („RAIFFEISEN BANK SA”, `statusInactivi:false`, `scpTVA:true`, `statusRO_e_Factura:false`).
- `/v10/tva` → 404 („No endpoint POST /PlatitorTvaRest/v10/tva”).
- URL-ul vechi v8 `https://webservicesp.anaf.ro/PlatitorTvaRest/api/v8/ws/tva` → 404.
- `https://webservicesp.anaf.ro/api/registruroefactura/v1/interogare` → `{"found":[],"notFound":[361820]}`.
- `GET https://webservicesp.anaf.ro/bilant?an=2024&cui=361820` → HTTP 200, cu indicatori pentru 2024.

## Documente

### A. Servicii web publice ANAF (fără autentificare), folosite la KYC

1. **„Serviciu web pentru verificarea contribuabililor care sunt înregistrați conform art. 316 din Codul Fiscal…” (PlatitorTvaRest v9)**, emitent ANAF. Documentația: https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/doc_WS_V9.txt. Pagina de servicii (https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/servicii_sincron.html) arată „Instrucţiuni versiune nouă serviciul web sincron – publicat în data de 04.02.2025” și URL-ul `https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva`. **Status: activ** (test 200), v10 nu există, v8 a fost retras (404).
   - Ce acoperă: Registrul TVA (art. 316 Cod fiscal), RTVAI, registrul inactivi/reactivați, plata defalcată (split TVA) și Registrul RO e-Factura.
   - Câmpuri:
     - `date_generale`: `denumire`, `adresa`, `nrRegCom`, `cod_CAEN`, `iban`, `stare_inregistrare`, `data_inregistrare`, `statusRO_e_Factura`, `organFiscalCompetent`, `forma_juridica`;
     - `inregistrare_scop_Tva`: `scpTVA` și `perioade_TVA`, cu `mesaj_ScpTVA` = „Temeiul legal al anularii”;
     - `inregistrare_RTVAI`;
     - `stare_inactiv`: `dataInactivare`, `dataReactivare`, `dataRadiere`, `statusInactivi`;
     - `inregistrare_SplitTVA`;
     - `adresa_sediu_social` și `adresa_domiciliu_fiscal`.
   - Răspunsul live conține și un câmp nedocumentat, `data_inreg_Reg_RO_e_Factura`.
   - Reguli de utilizare, citat: „Un request poate contine maxim 100 de CUI-uri. Un client poate executa maxim 1 request pe secunda.” și „Orice tentativa de suprasolicitare a serverului va fi pedepsita conform reglementarilor in vigoare.”
2. **Serviciul web asincron pentru aceleași registre (v8)**: https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/doc_WS_Async_V8.txt („actualizat în data de 20.04.2023”), URL `https://webservicesp.anaf.ro/AsynchWebService/api/v8/ws/tva`. Reguli: „Raspunsul poate fi descarcat o singura data”, se așteaptă minimum 2 secunde înainte de primul GET, „Clientul trebuie sa descarce raspunsul in maxim 3 zile”, maximum 100 de CUI-uri pe cerere și 1 cerere pe secundă. Status: publicat; nu l-am testat live.
3. **Serviciul web Registrul RO e-Factura**: https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/docV1.txt („publicat în data de 05.11.2024”), `POST https://webservicesp.anaf.ro/api/registruroefactura/v1/interogare`. Câmpuri: `registru`, `categorie`, `dataInscriere`, `dataRenuntare`, `dataRadiere`, `dataOptiuneB2G`, `stare`. Cod 404 „In cazul in care nu au fost gasite date pentru nici un cui”. Aceleași limite: 100 de CUI-uri și 1 cerere/s. Activ (testat).
4. **Serviciul web pentru situațiile financiare (bilanț)**: https://static.anaf.ro/static/10/Anaf/Informatii_R/doc_WS_Bilant_V1.txt (20.01.2021), `GET https://webservicesp.anaf.ro/bilant?an=AAAA&cui=N`, „maxim 1 request pe secunda”. Documentația spune „indicatorii publicati pe web… pentru perioada 2014‑2019”, dar testul live a întors date pentru 2024, deci documentația este învechită.
5. **Pagina-index „Servicii web”**: https://www.anaf.ro/anaf/internet/ANAF/servicii_online/servicii_web_anaf. Registrul TVA, RTVAI, split și inactivi apar cu mențiunea „actualizat în data de 21.03.2023”. Alte servicii listate: registrul agricultorilor (`/RegAgric/api/v2/ws/agric`) și cel al cultelor (`/RegCult/api/v2/ws/cult`).
6. **CPF art. 92 („Registrul contribuabililor/plătitorilor inactivi/reactivați”)**: alin. (11) „…este public și se afișează pe site-ul A.N.A.F.”; alin. (13) decizia „produce efecte față de terți din ziua următoare datei înscrierii în registru”. Alin. (1^2) lit. a) declară inactivă PJ care „nu are cont de plăți în România” (introdus prin Legea 239/2025, aplicabil din 18.12.2025, conform notei de pe legislatie.just.ro, id 170007).
7. **Lista contribuabililor PJ fără obligații restante**: CPF art. 162^1 și OPANAF nr. 1.923/2022, conform Catalogului serviciilor ANAF 2025, serviciul 1.8, cu actualizare „trimestrial” (https://static.anaf.ro/static/10/Anaf/Informatii_R/Catalogul_serviciilor_ANAF_2025.pdf). Textul OPANAF 1923/2022 nu l-am citit direct.

### B. API-uri protejate (OAuth sau certificat calificat): e-Factura și SPV

8. **„Procedura de înregistrare a aplicațiilor în portalul ANAF” (OAuth)**, PDF de 35 de pagini, „actualizat în data de 12.04.2024”: https://static.anaf.ro/static/10/Anaf/Informatii_R/API/Oauth_procedura_inregistrare_aplicatii_portal_ANAF.pdf.
   - IdP-ul este `logincert.anaf.ro`, cu autentificare prin certificat digital calificat al unui utilizator SPV PJ („reprezentant legal, reprezentant desemnat, imputernicit”). Endpoint-uri: `https://logincert.anaf.ro/anaf-oauth2/v1/authorize` și `/token`.
   - Valabilitate: „ACCES TOKEN JWT: 129600 minute = 90 zile”, „REFRESH TOKEN JWT: 525600 minute = 365 zile”.
   - Limită: „1000 Requests pe 1 minut” pe api.anaf.ro (codul 429 la depășire).
9. **Informații tehnice RO e-Factura** (MF): https://mfinante.gov.ro/ro/web/efactura/informatii-tehnice. Pagina conține swagger-ele (upload, stare, listă, descărcare, validare, xml-to-pdf, „validare semnătură”), menționează „Aplicație verificare semnătură factură electronică – actualizat 13.03.2026” și precizează că URL-ul B2C este obligatoriu din 31.03.2025.
   - Limitele specifice (https://mfinante.gov.ro/static/10/eFactura/limiteApeluriAPI.txt): „max. 1000 apeluri/minut”, `/lista` „max 1500 interogari/ zi/ CUI la lista simpla”, `/descarcare` „max 10 descarcari/ zi/ un anumit mesaj”.
   - Endpoint-urile sunt `https://api.anaf.ro/prod/FCTEL/rest/...` (OAuth) sau `https://webserviceapl.anaf.ro/prod/FCTEL/rest/...` (certificat), conform https://static.anaf.ro/static/10/Anaf/Informatii_R/Servicii_web/url_eFactura.html.
10. **Swagger „XML Signature Validation API”** (validarea sigiliului MF pe factura XML): https://mfinante.gov.ro/static/10/eFactura/validaresemnatura.html. Endpoint `POST /api/validate/signature` (multipart: `file` + `signature`, tip „detached”). Mesajul de succes spune că XML-ul „este considerat document original din perspectiva legislativă”. Serverul de bază nu apare explicit în swagger (neverificat).
11. **OUG nr. 120/2021 (RO e-Factura)**, forma consolidată pe legislatie.just.ro (https://legislatie.just.ro/Public/DetaliiDocument/247243), art. 4:
    - alin. (4): „se aplică sigiliul electronic al Ministerului Finanțelor… atestă primirea acesteia în sistemul național”;
    - alin. (6): „Exemplarul original al facturii electronice se consideră fișierul de tip XML însoțit de sigiliul electronic al Ministerului Finanțelor.”
12. **„Prezentare servicii web SPV” (SPVWS2)**: https://static.anaf.ro/static/10/Anaf/Informatii_R/Prezentare_WS_SPV.txt. Serviciile au fost publicate pe 21.02.2018; pagina sincron indică „publicat în data de 04.06.2018”. Clientul model este https://github.com/MfpAnaf/ClientSPV (organizația GitHub MfpAnaf, legată din pagina ANAF; ultima actualizare a repo-ului: 2019-05-07).
    - `GET https://webserviced.anaf.ro/SPVWS2/rest/listaMesaje?zile=XX` întoarce mesajele „pentru toate persoanele fizice sau juridice pentru care are drepturi posesorul certificatului digital calificat”.
    - `GET /SPVWS2/rest/descarcare?id=…` „intoarce un pdf = mesajul din SPV”.
    - `/SPVWS2/rest/cerere?tip=…`: după README, tipurile includ „Adeverinte Venit”, „D212” (duplicat), „Fisa Rol”, „Situatie Sintetica”, „Istoric declaratii” și **„InterogariBanci – Situatia interogarilor efectuate de banci la ANAF privind veniturile persoanei fizice”**. Motivul „Institutie financiar bancara asigurare etc.” este acceptat pentru adeverința de venit.

### C. Accesul băncilor la datele fiscale ale clientului, cu acordul acestuia

13. **CPF art. 11 („Secretul fiscal”)** (sursa ANAF de mai sus):
    - alin. (3) lit. d): organul fiscal poate transmite informații „oricărui solicitant, cu consimțământul expres și neechivoc al contribuabilului/plătitorului… Responsabilitatea privind modalitatea de utilizare a informațiilor astfel obținute revine solicitantului”. Litera a fost modificată prin Legea 295/2020 (nota de pe legislatie.just.ro).
    - alin. (6^1): solicitantul „care prin natura activității solicită frecvent informații de natura secretului fiscal despre clienții/partenerii săi poate încheia un protocol… utilizând sistemul informatic în conformitate cu art. 70^1”. Alineatul a fost modificat prin OG 11/2021.
14. **CPF art. 70^1 („Modalitatea de colaborare”)**: alin. (1) lit. b), pentru „persoanele juridice de drept privat, utilizând sisteme informatice dedicate”; alin. (2) definește PatrimVen (pentru instituțiile publice); alin. (4) lit. b) trimite la un ordin ANAF pentru modelul-cadru al protocolului.
15. **OPANAF nr. 146 din 1 februarie 2022**, publicat în MO nr. 157 din 16.02.2022, privind condițiile de furnizare a informațiilor, modelul-cadru al protocolului de colaborare și procedura de schimb de informații între ANAF și persoanele juridice de drept privat semnatare (art. 70^1 alin. (1) lit. b) CPF): https://legislatie.just.ro/Public/DetaliiDocumentAfis/251680. **Status: în vigoare.** O căutare după titlu pe legislatie.just.ro („nr. 146/2022”) nu a găsit acte modificatoare.
    - Art. 2 alin. (2): se aplică „instituțiilor de credit, instituțiilor financiare nebancare și altor persoane juridice de drept privat”.
    - Art. 2 alin. (3) lit. c): cere un audit IT extern ex ante și apoi anual (raport până la 15 aprilie; acceptă și certificări de tip ISO 27001, conform anexei, art. 2 alin. (3)).
    - Art. 2 alin. (7) enumeră informațiile disponibile:
      - pentru persoane fizice, „natura, sursa și cuantumul veniturilor… ultimilor 3 ani fiscali”;
      - pentru persoane juridice, informații din situațiile financiare pe 3 ani;
      - „certificatul de atestare fiscală pentru persoane juridice”;
      - existența unui cont curent la altă bancă, pentru contul cu servicii de bază (Legea 258/2017).
    - Art. 4 alin. (1) lit. a): acordul poate fi semnat olograf sau electronic ori acceptat expres. Pentru semnătura electronică simplă, acordul se generează în PDF și trebuie să conțină „numele și prenumele în clar…, data și ora acceptării exprese, precum și IP-ul stației”. „Acordul este valabil 5 zile lucrătoare.”
    - Art. 3: banca trebuie să informeze clientul online că datele fiscale consultate „le sunt puse la dispoziție și direct prin spațiul privat virtual”.
    - Art. 8: accesările și acordurile se păstrează „pe o perioadă de 8 ani”.
    - Art. 10: protocoalele existente cu băncile și IFN-urile au avut o perioadă de tranziție de 6 luni, iar OPANAF 3731/2016 a fost abrogat.
    - Anexele: nr. 1 este acordul pentru venituri (DA/NU), nr. 2 acordul pentru CAF, nr. 3 acordul pentru contul de bază, nr. 4 procedura comună de lucru, nr. 5 formularul de acceptanță.
15b. **Proiect de ordin ANAF care ar înlocui OPANAF 146/2022 (septembrie 2026, neadoptat la 28.10.2026)** [secundar]: Financiarul, 12.09.2026, https://financiarul.ro/economie/bancile-ar-putea-verifica-veniturile-prin-anaf-cu-acord-dat-in-spv/ (subiectul a fost preluat și de Profit.ro și HotNews).
    - Firmele ar putea da acordul „numai electronic, prin SPV”, prin formularul „acord pentru IC”, valabil 5 zile lucrătoare. Banca ar primi situațiile financiare pe 3 ani, CAF și, nou, D100, D101 și D300 (decontul de TVA) din ultimele 12 luni, doar „pentru evaluarea riscului de credit și prevenirea sau detectarea fraudelor în procesul de creditare”.
    - Persoanele fizice cu SPV ar folosi SPV; cele fără SPV ar semna în continuare la bancă (olograf sau electronic, PDF cu nume, dată, oră și IP).
    - Banca ar trimite cererea prin platforma „Schimb de informații ANAF-PJDP”, iar CNIF ar valida automat acordul.
    - Status: articolul spune „Schimbarea nu este încă în vigoare”. La 28.10.2026, căutarea pe legislatie.just.ro după „persoanele juridice de drept privat” întoarce doar OPANAF 146/2022, iar lista vizibilă de proiecte de pe anaf.ro (29.09–21.10.2026) nu îl mai conține. Textul proiectului nu a fost citit.
16. **OPANAF nr. 3731/2016**, publicat în MO nr. 17 din 09.01.2017 (https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3731_2016.pdf), modelul-cadru al protocolului ANAF – IFN, modificat prin **OPANAF nr. 3194/2019** (MO 1015/17.12.2019; https://legislatie.just.ro/Public/DetaliiDocumentAfis/221025). **Status: abrogat** prin OPANAF 146/2022, art. 10 alin. (3). Este relevant istoric: acordul „în format letric”, valabil 5 zile lucrătoare, date pe „ultimii 2 ani fiscali”, termenul de răspuns al ANAF „maximum 2 zile lucrătoare” și obligația IFN de a raporta lunar către ANAF creditele aprobate sau respinse.

### D. SPV și comunicarea electronică ANAF ↔ contribuabil (inclusiv banca, în calitate de contribuabil)

17. **CPF art. 46**:
    - alin. (1): actul se emite „pe suport hârtie sau în formă electronică”;
    - alin. (4): actul electronic emis de organul fiscal central „se semnează cu semnătura electronică extinsă a Ministerului Finanțelor Publice, bazată pe un certificat calificat”;
    - alin. (9) lit. c): data emiterii este „data aplicării semnăturii electronice extinse”.
18. **CPF art. 47 („Comunicarea actului administrativ fiscal”)**:
    - alin. (15), în forma dată de OG 31/2022 și aplicabilă din 03.09.2022: actul electronic „se consideră comunicat la data punerii la dispoziția contribuabilului/plătitorului prin aceste mijloace”;
    - alin. (16): procedura se aprobă prin ordin ANAF, cu avizul ADR;
    - alin. (16^1): ANAF „poate să înregistreze din oficiu contribuabilii/plătitorii în sistemul de comunicare electronică”;
    - alin. (16^2): dacă un contribuabil înregistrat din oficiu nu accesează sistemul în 15 zile, comunicarea se face prin publicitate.
19. **CPF art. 79 alin. (1^1)**. Nota de pe legislatie.just.ro arată că art. 79 a fost completat prin OG 11/2021 (MO 832/31.08.2021), cu aplicare de la 01.03.2022, și apoi prin OUG 188/2022. Faptul că alin. (1^1) provine din OG 11/2021 este probabil, nu confirmat pe text. Conținutul: PJ, asocierile și persoanele care exercită profesii liberale sau sunt PFA „sunt obligați să transmită organului fiscal central documente… prin mijloace electronice de transmitere la distanță…, respectiv prin înrolarea în sistemul de comunicare electronică dezvoltat de Ministerul Finanțelor/A.N.A.F.” Alin. (1^2): documentele depuse pe hârtie „nu vor fi luate în considerare”. În practică, acesta este temeiul obligativității SPV pentru PJ.
20. **CPF art. 80 alin. (1)**: lit. a) PJ și PFA se identifică „numai cu certificate calificate”; lit. b) celelalte persoane fizice se identifică prin furnizori autorizați de servicii publice de autentificare sau prin dispozitive (utilizator/parolă cu coduri unice, telefon mobil, digipass etc.).
21. **CPF art. 103**: depunerea declarațiilor electronic. Alin. (4): data depunerii este data înregistrării pe portal, „cu condiția validării conținutului”.
22. **OPANAF nr. 1090 din 9 iunie 2022**, publicat în MO nr. 585 din 16.06.2022 (https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1090_2022.pdf), Procedura de comunicare prin mijloace electronice de transmitere la distanță între organul fiscal central și persoanele fizice, juridice și alte entități. **Status: în vigoare.** A intrat în vigoare la data abrogării OMFP 660/2017 (art. 9 alin. (2)), adică pe 24.06.2022, prin OMFP 1420/2022 (vezi pct. 23). Catalogul ANAF din 15.10.2025 îl indică drept bază legală a SPV. Prevederi principale:
    - Art. 2 definește SPV ca „serviciu de distribuție electronică înregistrată”.
    - Art. 4 și art. 7: documentele se semnează cu „semnătură electronică extinsă bazată pe un certificat calificat… sau cu sigiliu electronic calificat, emis în conformitate cu Regulamentul (UE) nr. 910/2014”.
    - Art. 6: PJ și reprezentanții se identifică cu certificate calificate; persoanele fizice cu certificat calificat sau NPOTP (nivel „substanțial”).
    - Art. 12: PJ accesează SPV prin reprezentant legal, reprezentant desemnat sau împuternicit.
    - Art. 17 alin. (6): verificarea pentru NPOTP se face electronic, la ghișeu sau „prin identificarea persoanei la distanță prin mijloace video”.
    - Art. 20 alin. (8)–(9): oricine poate verifica autenticitatea documentelor din SPV cu un dispozitiv de verificare a semnăturii sau prin aplicația ANAF, „folosind numărul de înregistrare al documentului și codul de identificare fiscală”.
    - Art. 24 alin. (2): actele „se consideră comunicate în termen de 15 zile de la data punerii la dispoziție”, ceea ce intră în conflict cu CPF art. 47 alin. (15) după 03.09.2022 (vezi Explicații).
    - Art. 24 alin. (4): documentele se semnează „cu certificatul calificat sau cu sigiliul electronic calificat al Ministerului Finanțelor”.
    - Anexa 1 (persoane fizice): „Certificatul de atestare fiscală” și „Adeverință de venit” se păstrează 100 de zile, „Certificatul de rezidență fiscală” 60 de zile. Anexa 2 (PJ): CAF, actele de executare și CRF se păstrează 60 de zile.
23. **Lanțul de acte anterioare**:
    - **OMFP nr. 1.154/2014** (MO 646/02.09.2014), procedura pentru persoane fizice: https://legislatie.just.ro/Public/DetaliiDocumentAfis/161005;
    - **OMFP nr. 660/2017** (PF și PJ), modificat prin OMFP 2894/2019, 2012/2020 și 3192/2020;
    - abrogat prin **OMFP nr. 1.420/2022** (MO 622/24.06.2022): https://legislatie.just.ro/Public/DetaliiDocumentAfis/256717.
    Toate trei sunt ordine ale ministrului finanțelor, nu OPANAF; în prezent sunt abrogate sau istorice.
24. **OG nr. 31/2022** (MO 857/31.08.2022): https://static.anaf.ro/static/10/Anaf/legislatie/OG_31_2022.pdf. Modifică CPF art. 47 alin. (15) și art. 83 alin. (4) (atribuirea NIF prin bancă, vezi pct. 31).
25. **OPANAF nr. 2326/2017** (MO 649/08.08.2017): https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2326_2017.pdf. Lista declarațiilor care „se transmit obligatoriu electronic” include 100, 101, 205, 207, 300, 390, 394 și 710. Se aplică de la obligațiile aferente lunii ianuarie 2018. Catalogul 2025 adaugă în aceeași listă 112 și 101 Grup fiscal.
26. **„Etapele pe scurt”** pentru depunerea declarațiilor de către PJ: https://static.anaf.ro/static/10/Anaf/Declaratii_R/instructiuni/etape_depunere.htm. Pașii: certificat calificat, apoi formularul 150, apoi „www.e-guvernare.ro Depunere declaraţii”, apoi recipisa. Pagina conexă: https://www.anaf.ro/anaf/internet/ANAF/servicii_online/declaratii_electronice. Pentru înrolarea PJ în SPV: https://www.anaf.ro/anaf/internet/ANAF/servicii_online/inreg_inrol_pf_pj_spv.
27. **Catalogul serviciilor livrate contribuabililor 2025**, „actualizat în data de 15.10.2025”, 290 de pagini: https://static.anaf.ro/static/10/Anaf/Informatii_R/Catalogul_serviciilor_ANAF_2025.pdf. Conține fișe cu bază legală, canal și termen (SPV 1.5, CAF 7.2, CRF 7.12, înregistrare fiscală 3.1.1).

### E. Autenticitatea documentelor ANAF și certificatele fiscale

28. **„Verificare documente electronice eliberate în Spațiul Privat Virtual”**: https://www.anaf.ro/anaf/internet/ANAF/servicii_online/verif_doc_electronice_spv. Aplicația este la https://www.anaf.ro/VerifAdev/ și cere „Număr de înregistrare document”, „CNP sau CIF” și un cod captcha, apoi buton „Obține document”. Scopul declarat: „verificarea autenticității și integrității documentelor .pdf emise și transmise contribuabililor în cadrul serviciului Spațiul Privat Virtual”.
    - Lista documentelor verificabile (PDF creat la 30.09.2022: https://static.anaf.ro/static/10/Anaf/spv/Lista_verificare_documente.pdf): adeverința de venit, certificatul de atestare fiscală, certificatul de cazier fiscal, certificatele de rezidență fiscală pe toate variantele și certificatele de atestare a impozitului plătit de nerezidenți.
    - Instrucțiunile de validare a semnăturii (PDF din 31.03.2015): https://static.anaf.ro/static/10/Anaf/spv/instructiuni_validare_certificat_SPV.pdf. Indică instalarea certificatului „autoritate MFP” în Adobe Reader, ca identitate de încredere.
    - Rădăcina publicată (https://static.anaf.ro/static/10/Anaf/spv/autoritate.zip), inspectată cu openssl: `CN=MFP Autoritate Certifier, O=Ministerul Finantelor Publice, OU=DGTI`, autosemnată, valabilă 22.03.2007 – 23.03.2037.
29. **CPF art. 158 („Certificatul de atestare fiscală emis de organul fiscal central”)**: alin. (5) „se emite în maximum 3 zile lucrătoare… poate fi utilizat… pe o perioadă de până la 30 de zile”; pentru persoanele fizice fără activitate independentă, „până la 90 de zile”. **CPF art. 160 alin. (1)**: terții pot solicita CAF „numai cu consimțământul expres și neechivoc al contribuabilului”.
30. **OPANAF nr. 3654/2015** (MO 954/23.12.2015), procedura de eliberare a CAF, în forma consolidată (https://legislatie.just.ro/Public/DetaliiDocument/174241), modificat prin **OPANAF nr. 3008/2019** (MO 945/26.11.2019). Art. 1 alin. (3) lit. e): cererea se poate depune „prin intermediul sistemului informatic pus la dispoziția instituțiilor de credit, în spațiul privat de pe portalul [ANAF]”. Art. 11 alin. (1) lit. c) și alin. (2): CAF-ul comunicat electronic „este valabil și fără semnătura… și ștampila organului emitent”.
31. **CPF art. 83 alin. (4)**, în forma dată de OG 31/2022: odată cu cererea de deschidere a unui cont, „instituțiile de credit transmit organului fiscal central solicitarea de atribuire a numărului de identificare fiscală” pentru persoane fizice nerezidente sau PJ fără cod. ANAF comunică NIF/CIF „în termen de 5 zile”, iar pentru cererile făcute prin aplicația online, NIF-ul se comunică băncii „prin aplicația online”.
32. **OMFP nr. 583/2016** (formularistica pentru art. 230 și 232 din Codul fiscal; certificate de rezidență fiscală), forma consolidată: https://legislatie.just.ro/Public/DetaliiDocument/178301. A fost modificat prin OMFP 1830/2020 (MO 302/10.04.2020) și OMF 499/2021 (MO 410/20.04.2021).
    - Art. 5 alin. (2): cererea electronică se depune „în format PDF cu XML atașat”, cu o arhivă ZIP de cel mult 5 MB.
    - Art. 6: certificatele „comunicate… prin intermediul «Spațiului Privat Virtual», pot fi verificate de terțe persoane pe site-ul [ANAF]… pe durata de un an de la data emiterii”.
33. **Codul fiscal (Legea 227/2015) art. 230 alin. (2)**, forma consolidată la 25.10.2026 (https://legislatie.just.ro/Public/DetaliiDocument/171282): nerezidentul prezintă plătitorului de venit (de exemplu, banca, la plata dobânzii) certificatul de rezidență fiscală; „Certificatul… prezentat în cursul anului… este valabil și în primele 60 de zile calendaristice din anul următor”.

### F. Alte canale ANAF ↔ bănci (doar semnalate)

34. **e-Popriri**: Procedura din 3 mai 2022 de comunicare electronică către instituțiile de credit a actelor de executare silită, aprobată prin **OPANAF nr. 878/2022** (MO 466/10.05.2022): https://legislatie.just.ro/Public/DetaliiDocumentAfis/254994. Detalii în [01-popriri.md](01-popriri.md).
35. **CPF art. 61**: băncile comunică ANAF „zilnic” deschiderile și închiderile de conturi, împuterniciții și beneficiarii reali. Procedura în vigoare este OPANAF 3746/2020, modificat prin OPANAF 393/2026. OPANAF 3770/2015 (modificat prin 1106/2016 și 3592/2016) a fost abrogat prin OPANAF 3746/2020, art. 5. Detalii în [02-informatii.md](02-informatii.md).
36. **ROeID** (SSO-ul național, ADR): lista furnizorilor (https://www.roeid.ro/furnizori-servicii) conține CNPP, eToll, eSănătateaMea și primării, dar **nu și ANAF** la data verificării (2026-10-28).
37. **Lista de încredere RO (eIDAS)**: https://trustedlist.adr.gov.ro/trustedlist.xml (ListIssueDateTime 2026-09-28). Prestatorii listați sunt Trans Sped, DigiSign, certSIGN, AlfaTrust, Centrul de Calcul, STS, Zipper, Simpliworks, SRI și Semnaturescu. Ministerul Finanțelor nu apare ca prestator.
38. **Planul Strategic ANAF 2025‑2028**, publicat la 17.09.2024: https://static.anaf.ro/static/10/Anaf/Informatii_R/240917_%20Plan_%20StrategicANAF2025-2028.pdf. Promite „platforma audio-video pentru audierea online”, chatbot și precompletarea formularelor. Nu am găsit nimic specific despre interconectarea ANAF cu băncile.

### G. Pagini ale băncilor [secundar]

39. **ING**, https://ing.ro/persoane-fizice/credite/ing-personal. Citat: creditul „poate fi acordat pe loc, fără adeverință de venit… atunci când veniturile tale sunt verificate exclusiv în baza de date a ANAF”. În Home'Bank clientul semnează electronic „Acordul de consultare și prelucrare a informațiilor din bazele de date ale Ministerului Finanțelor Publice, Agenția Națională de Administrare Fiscală (ANAF)”, cu semnătură electronică calificată (InfoCert).
    - Lista de documente pe tipuri de venit (arhivă datată 01.2026, documentul intern „Mai 2023”): https://ing.ro/dam/ingro/doc/documente-credit.zip. Pentru drepturi de autor, PFA și altele, cere „Declaratia unica (fisa 212)… semnata si stampilata de catre ANAF” SAU „Adeverinta de venit eliberata de autoritatea fiscala”.
40. **Raiffeisen**:
    - https://www.raiffeisen.ro/ro/persoane-fizice/produsele-noastre/credite/credit-de-nevoi-personale-fara-ipoteca.html: „Ai nevoie doar de actul tău de identitate și să semnezi acordul de consultare a veniturilor din baza de date ANAF.” și „Pot exista situații în care se solicită documente de venit”.
    - https://www.raiffeisen.ro/ro/persoane-fizice/produsele-noastre/credite/credit-imobiliar-casa-ta.html: „Acordul de consultare a veniturilor din baza de date ANAF, atât pentru tine cât și pentru codebitor”.
41. **CEC Bank**, https://www.cec.ro/persoane-fizice/credite/credite-nevoi-personale/credit-online-de-nevoi-personale: „dacă ești salariat/ pensionar, confirmarea veniturilor se va face automat, prin interogarea bazei de date ANAF”.
42. Nu am putut citi BT (HTTP 403). Paginile BRD și UniCredit pe care le-am încercat au întors 404, iar pagina BCR citită nu menționează ANAF.

## Workflow-uri

### W1. Verificarea CUI la onboardingul unei PJ, prin API

1. **Ofițerul KYC sau sistemul** normalizează CUI-ul: elimină prefixul „RO” și trimite un număr întreg, cum cere `"cui": 1234` în doc_WS_V9.txt.
2. **Sistemul** trimite `POST https://webservicesp.anaf.ro/api/PlatitorTvaRest/v9/tva`, JSON `[{"cui":N,"data":"AAAA-LL-ZZ"}]`, cu cel mult 100 de CUI-uri pe cerere și 1 cerere/s (doc_WS_V9.txt). Pentru loturi mari se folosește varianta asincronă, cu descărcarea răspunsului o singură dată, în 3 zile (doc_WS_Async_V8.txt). Câmpul `data` permite verificarea la o dată anterioară, de exemplu data unei tranzacții.
3. **Sistemul** compară `denumire`, `adresa_sediu_social` și `nrRegCom` cu actele constitutive sau extrasul ONRC prezentat. Semnalează ca roșu `stare_inactiv.statusInactivi = true` (CPF art. 92; efecte față de terți din ziua următoare înscrierii, alin. (13)), `dataRadiere` nevidă, iar ca galben `scpTVA=false` cu `mesaj_ScpTVA`, adică o anulare din oficiu cu temei legal.
4. **Opțional**, se interoghează registrul RO e-Factura (`/api/registruroefactura/v1/interogare`, docV1.txt) și bilanțul (`/bilant?an=&cui=`, 1 cerere/s) pentru scoring.
5. **Ofițerul** păstrează în dosar răspunsul JSON brut, cu data interogării, ca probă a verificării. Răspunsul nu poartă semnătură ANAF, deci are valoare de evidență internă, nu de act oficial.
6. **Termen:** sincron, în secunde (100 ms la testul live). **Temei:** registrele sunt publice (CPF art. 92 alin. (11)), iar serviciul este publicat de ANAF fără autentificare.

### W2. Verificarea veniturilor cu acordul clientului (PF sau PJ), prin protocol PJDP

1. **Banca** încheie cu ANAF protocolul după modelul din OPANAF 146/2022: dosar cu audit IT ex ante (art. 2 alin. (3)–(4)), lista utilizatorilor desemnați înrolați la CNIF cu certificate calificate (anexa 4 pct. 1) și formularul de acceptanță semnat după testare (anexa 5; trecerea în producție „în termen de 5 zile lucrătoare de la semnare”). Protocolul este valabil un an și se prelungește automat (anexa, art. 9 alin. (8)).
2. **Clientul** semnează acordul (anexa nr. 1, cu opțiunea DA): olograf, cu semnătură electronică sau prin acceptare expresă. În acest ultim caz, acordul se generează ca PDF cu nume, dată, oră și IP (OPANAF 146/2022, art. 4 alin. (1) lit. a)). Acordul este valabil **5 zile lucrătoare**. În online, banca îl informează că datele sunt disponibile și în SPV (art. 3).
3. **Banca** generează fișierul de solicitare XML F2101 (CNP/CUI) și îl ambalează într-un PDF semnat cu certificat calificat, cu acordul atașat „simultan cu solicitarea” (art. 4 alin. (1) lit. c)). Fișierul se încarcă în „spațiul privat de pe portalul ANAF” (anexa 4 pct. 2: „documente semnate, în format PDF cu fișier xml atașat”).
4. **ANAF** răspunde cu F2102 (veniturile PF sau PFA, pe ultimii 3 ani) sau cu F2103 însoțit de PDF (veniturile PJ, câte un CUI pe cerere; mai multe CUI-uri produc un fișier de eroare). **Termen:** „maximum 3 (trei) zile lucrătoare” în protocol (anexa, art. 7), dar anexa 4 spune „în termen de o zi lucrătoare”.
5. **Banca** descarcă răspunsul în cel mult 2 zile lucrătoare de la afișare. ANAF îl arhivează după 5 zile lucrătoare și trimite o alertă cu o zi înainte (anexa 4).
6. **Banca** pune la dispoziția clientului datele primite (anexa, art. 5 alin. (1) lit. e)), arhivează acordul și îl prezintă ANAF la cerere: în 10 zile lucrătoare pentru cel mult 20 de acorduri, în 25 de zile pentru mai multe (art. 4 alin. (1) lit. b)). Jurnalul de accesări se păstrează 8 ani (art. 8).
7. **Clientul** poate vedea în SPV interogările făcute de bănci: tipul „InterogariBanci” din SPVWS2 (README-ul ClientSPV, GitHub MfpAnaf).
8. **Dacă clientul alege NU sau protocolul nu acoperă cazul**, se aplică W2b. Formularul de acord prevede expres: „dezacordul meu (răspunsul NU) conduce la imposibilitatea [PJDP] de a interoga electronic… accept să fac dovada veniturilor în modalitățile prevăzute de lege”.
9. **Temei:** CPF art. 11 alin. (3) lit. d) și alin. (6^1), art. 70^1 alin. (1) lit. b); OPANAF 146/2022.

### W2b. Alternativa: clientul își aduce singur dovada din SPV

1. **Clientul**, înrolat în SPV (OPANAF 1090/2022, art. 17–18), cere „Adeverință de venit” (anexa 1, partea B pct. 7) sau, pentru PFA și drepturi de autor, descarcă duplicatul D212 sau recipisa. Prin API, motivul „Institutie financiar bancara asigurare etc.” este acceptat (ClientSPV README).
2. **ANAF** emite documentul semnat electronic în SPV, unde rămâne 100 de zile pentru adeverința de venit (anexa 1).
3. **Clientul** trimite banca PDF-ul original (nu o captură sau un print). **Banca** aplică W5.
4. **Practica de piață [secundar]:** ING acceptă „Declaratia unica (fisa 212)… semnata si stampilata de catre ANAF” SAU „Adeverinta de venit eliberata de autoritatea fiscala”.

### W3. CAF pentru un client PJ (de exemplu la creditare), cu acord

1. **Reprezentantul clientului** semnează acordul din anexa nr. 2 la protocolul OPANAF 146/2022.
2. **Banca** depune cererea prin „sistemul informatic pus la dispoziția instituțiilor de credit, în spațiul privat de pe portalul [ANAF]” (OPANAF 3654/2015 art. 1 alin. (3) lit. e), după OPANAF 3008/2019).
3. **ANAF** emite CAF-ul în cel mult 3 zile lucrătoare (CPF art. 158 alin. (5)). CAF-ul electronic este valabil fără semnătură olografă și ștampilă (OPANAF 3654/2015 art. 11 alin. (2)).
4. **Banca** îl folosește cel mult 30 de zile de la eliberare (art. 158 alin. (5)).
5. **Varianta în care clientul aduce CAF-ul:** clientul îl cere prin SPV (anexa 2 la OPANAF 1090, unde se păstrează 60 de zile), iar banca îl verifică după W5.

### W4. Depunerea unei declarații de către bancă, ca plătitor sau contribuabil (de exemplu D100, D112, D205, D207, D300, D394)

1. **Reprezentantul legal sau persoana desemnată** obține un certificat calificat de la un prestator de pe lista ADR (pasul 1 din etape_depunere.htm). Pentru acces SPV PJ, se înregistrează ca reprezentant legal, desemnat sau împuternicit (OPANAF 1090 art. 12–14). Pentru serviciul clasic „Declarații electronice” există și formularul 150 (pașii 4–7, confirmare „în termen de 4-7 zile lucratoare”).
2. **Departamentul de taxe** descarcă PDF-ul inteligent (formular PDF cu XML atașat), îl completează și îl validează. Alternativ, generează XML-ul din ERP și îl împachetează în PDF.
3. **Departamentul de taxe** semnează cu certificatul calificat și transmite prin www.e-guvernare.ro „Depunere declaraţii” (pasul 8).
4. **ANAF** emite recipisa, apoi mesajul „Informaţii după prelucrare”. Recipisele se pot prelua automat cu `SPVWS2/rest/listaMesaje` (tip „RECIPISA”).
5. **Termen:** termenul legal al fiecărei declarații. Data depunerii este data înregistrării pe portal, cu condiția validării; o declarație invalidă se poate corecta până la sfârșitul lunii termenului (CPF art. 103 alin. (4)–(5)).
6. **Temei:** CPF art. 79 alin. (1^1) (PJ obligate să comunice electronic din 01.03.2022), art. 80 alin. (1) lit. a), art. 103; OPANAF 2326/2017 pentru declarațiile obligatoriu electronice.

### W5. Primirea actelor ANAF prin SPV (banca, în calitate de contribuabil)

1. **ANAF** emite actul în formă electronică, semnat sau sigilat de MF (CPF art. 46 alin. (4); OPANAF 1090 art. 7 și art. 24 alin. (4)), și îl pune la dispoziție în SPV.
2. **Echipa de taxe sau un robot** interoghează zilnic `https://webserviced.anaf.ro/SPVWS2/rest/listaMesaje?zile=N` cu certificatul calificat al utilizatorului SPV, apoi descarcă fiecare mesaj cu `/descarcare?id=` (PDF). Răspunsul listează toate CUI-urile pentru care certificatul are drepturi.
3. **Calculul termenelor:** conform CPF art. 47 alin. (15), actul este comunicat „la data punerii la dispoziție”, așa că termenele de contestare și plată încep din acea zi. Textul OPANAF 1090 art. 24 alin. (2) („15 zile”) este anterior OG 31/2022, deci prudent este să se aplice data punerii la dispoziție.
4. **Retenție:** în SPV, actele administrative și de executare se păstrează 60 de zile (anexa 2), apoi „se retrag din SPV și se arhivează” (OPANAF 1090 art. 20 alin. (3)). Punerea lor la dispoziție la cerere este prevăzută expres doar după renunțarea la SPV (art. 25 alin. (8)); în rest, neverificat. Concluzia: banca trebuie să le descarce și să le arhiveze singură, în cele 60 de zile.
5. **Ca terț poprit**, banca primește popririle prin e-Popriri (OPANAF 878/2022), nu prin SPV-ul obișnuit.

### W6. Verificarea autenticității unui document ANAF adus de client (adeverință de venit, CAF, cazier fiscal, CRF)

1. **Clientul** furnizează fișierul PDF original descărcat din SPV, nu o scanare.
2. **Verificarea criptografică:** se validează semnătura electronică a PDF-ului (OPANAF 1090 art. 20 alin. (8)). Lanțul trebuie să ducă la rădăcina „MFP Autoritate Certifier”, publicată de ANAF (autoritate.zip, autosemnată, 2007–2037; instructiuni_validare_certificat_SPV.pdf). Orice modificare a fișierului invalidează semnătura.
3. **Verificarea la sursă:** operatorul introduce în https://www.anaf.ro/VerifAdev/ numărul de înregistrare al documentului și CNP-ul sau CIF-ul titularului, rezolvă captcha și apasă „Obține document”. Compară documentul obținut de la ANAF cu cel prezentat (OPANAF 1090 art. 20 alin. (9)). Lista documentelor verificabile: Lista_verificare_documente.pdf.
4. **Verificarea de valabilitate:** CAF-ul este folosibil 30 de zile pentru PJ și PFA, 90 de zile pentru PF fără activitate (CPF art. 158 alin. (5)). CRF-ul din SPV este verificabil un an de la emitere (OMFP 583/2016 art. 6). Banca verifică și concordanța CNP/CUI cu clientul identificat.
5. **Decizia:** un document este acceptat doar dacă semnătura e validă ȘI documentul de la ANAF coincide cu cel prezentat. Altfel, se escaladează (posibil fals).
6. **Pentru o e-factură XML** prezentată ca probă, se folosește validarea sigiliului MF (swagger `POST /api/validate/signature`; OUG 120/2021 art. 4 alin. (6)).
7. **Termen:** imediat (online, permanent). Captcha-ul face ca pasul 3 să fie manual.

### W7. Certificatul de rezidență fiscală (CRF)

a) **Banca sau un client rezident cere un CRF românesc:**
1. **Solicitantul** completează cererea din OMFP 583/2016 (PF, PF cu activitate independentă, PJ sau asociere), plus formularul statului străin dacă e cazul. Cererea electronică este PDF cu XML atașat, cu arhivă ZIP de cel mult 5 MB (art. 5 alin. (2)).
2. **Canal:** PJ depun doar electronic, prin e-guvernare sau SPV. PF pot depune și la ghișeu sau prin poștă (Catalog 2025, serviciul 7.12, pct. 4).
3. **ANAF** eliberează în 45 de zile de la înregistrarea cererii (Catalog 2025, 7.12 pct. 7). Certificatul se transmite electronic sau, numai pentru PF care nu sunt înregistrate în SPV, pe hârtie (pct. 6). Semnătura este electronică extinsă (OMFP 583/2016 art. 3 și art. 5 alin. (3)).
4. **Terții** îl verifică în VerifAdev timp de un an de la emitere (OMFP 583/2016 art. 6).

b) **Un client nerezident vrea să i se aplice convenția de evitare a dublei impuneri la dobânzi:**
1. **Clientul** prezintă băncii CRF-ul emis de statul său, „în momentul plății venitului” (Codul fiscal art. 230 alin. (2)).
2. **Banca** aplică cota din convenție; certificatul este valabil și în primele 60 de zile ale anului următor (același articol). Dacă CRF-ul vine după plată, banca regularizează în termenul de prescripție.

### W8 (KYC nerezidenți). Atribuirea NIF prin bancă la deschiderea contului

1. **Clientul**, persoană fizică nerezidentă sau PJ fără cod fiscal românesc, cere deschiderea unui cont.
2. **Banca** transmite ANAF cererea de atribuire a NIF/CIF (CPF art. 83 alin. (4), după OG 31/2022). Cererea poate fi făcută și prin aplicația online ANAF; în acest caz, NIF-ul „se comunică instituției de credit prin aplicația online”.
3. **ANAF** comunică băncii NIF-ul „în termen de 5 zile” (Catalog 2025, înregistrare fiscală, pct. 7). Certificatul de înregistrare rămâne la organul fiscal până la ridicare.

## Explicații

- **Ce nu dovedește API-ul PlatitorTvaRest:**
  - identitatea persoanei care reprezintă firma, administratorii sau beneficiarii reali (acestea vin de la ONRC și din registrul BR);
  - lipsa datoriilor (pentru asta e nevoie de CAF sau de lista de la art. 162^1);
  - solvabilitatea;
  - autenticitatea actelor prezentate.
  Răspunsul nu este semnat, deci are valoare de evidență internă, nu de act opozabil. Datele pot întârzia: inactivitatea produce efecte față de terți abia din ziua următoare înscrierii (art. 92 alin. (13)), iar înscrierea se face în cel mult 5 zile de la comunicarea deciziei (alin. (12)). Unele câmpuri, cum ar fi `iban` și `telefon`, sunt adesea goale. Documentația bilanțului este învechită: spune 2014‑2019, dar API-ul a întors date pentru 2024.
- **Limite de rată (risc operațional pentru un agent automat):**
  - registrele publice: 100 de CUI-uri pe cerere și 1 cerere/s, cu amenințare explicită de sancțiuni la suprasolicitare;
  - API-ul asincron: o singură descărcare, în 3 zile;
  - api.anaf.ro (OAuth): 1000 de cereri/minut;
  - e-Factura: limite pe CUI și pe mesaj.
  Pentru un sistem de producție sunt necesare throttling, cache și retry.
- **Acordul pentru datele ANAF nu este o simplă bifă GDPR:**
  - Este o derogare de la secretul fiscal (art. 11 alin. (3) lit. d)), strict limitată la 5 zile lucrătoare și la scopurile din formular: (i) relații contractuale, (ii) interes legitim, (iii) obligații legale, inclusiv administrarea riscului.
  - Formularul trebuie să ofere și varianta NU, cu o cale alternativă (documente). Doar așa consimțământul rămâne liber.
  - Formularul enumeră drepturile GDPR, inclusiv dreptul de a nu fi supus unei decizii automatizate (art. 22). Este relevant dacă un agent AI decide pe baza venitului ANAF.
  - Fiecare parte este „operator independent” (anexa, art. 8 alin. (1)). Responsabilitatea pentru folosirea datelor rămâne la bancă (art. 11 alin. (3) lit. d) teza a doua; art. 4 alin. (2) din ordin).
  - ANAF poate audita prin sondaj și poate suspenda accesul dacă banca nu remediază o neregulă în 5 zile lucrătoare (anexa, art. 9 alin. (4)–(5)).
  - Clientul vede interogările băncilor în SPV („InterogariBanci”).
- **Acoperirea datelor ANAF:**
  - Persoanele fizice salariate sau pensionare apar din D112 (anexa 4: „salariați sau pensionari pe ultimii trei ani”). Veniturile PFA apar din „ultimele trei declarații de impunere” (D212).
  - Veniturile din străinătate, cele nedeclarate sau declarate cu întârziere nu apar. De aceea băncile păstrează clauza „pot exista situații în care se solicită documente de venit” (Raiffeisen).
  - CEC limitează explicit verificarea automată la „salariat/ pensionar”.
- **Contradicția privind comunicarea prin SPV:** CPF art. 47 alin. (15), după OG 31/2022 (din 03.09.2022), spune „la data punerii la dispoziție”, iar OPANAF 1090/2022 art. 24 alin. (2) spune „în termen de 15 zile”. Ordinul este anterior și inferior legii, deci prevalează legea. Pentru calculul termenelor (contestații, plăți), soluția prudentă este data punerii la dispoziție. Cele 15 zile au rămas relevante la art. 47 alin. (16^2), pentru contribuabilii înregistrați din oficiu care nu accesează sistemul.
- **SPV obligatoriu pentru PJ:** nu există o obligație formulată „înregistrează-te în SPV”, ci obligația de a comunica electronic „prin înrolarea în sistemul de comunicare electronică” (art. 79 alin. (1^1), din 01.03.2022), cu sancțiunea că documentele pe hârtie nu se iau în considerare. Se adaugă posibilitatea înregistrării din oficiu (art. 47 alin. (16^1)) și, practic, e-Factura B2B.
- **Riscul de fals documentar:** PDF-urile ANAF se editează ușor. O imprimare sau scanare pierde semnătura. ANAF publică doar o rădăcină MF autosemnată, pe care trebuie să o adaugi manual ca rădăcină de încredere, iar MF nu apare pe lista de încredere eIDAS a României. Așadar un validator generic (Adobe sau DSS) nu va arăta automat „valid” fără configurarea acestei rădăcini, iar un fals poate trece de un operator neinstruit. Contramăsurile sunt verificarea criptografică cu rădăcina MF fixată și verificarea la sursă în VerifAdev (număr de înregistrare plus CNP/CIF). VerifAdev are captcha, deci nu poate fi automatizat legitim. Pentru volume mari, canalul corect este protocolul PJDP (W2/W3), în care datele vin direct de la ANAF.
- **Calitatea juridică a semnăturii:** CPF art. 46 alin. (4) vorbește de „semnătura electronică extinsă a MFP, bazată pe un certificat calificat”, iar OPANAF 1090 permite și „sigiliu electronic calificat” (eIDAS). Pe e-Factura, sigiliul MF este explicit în OUG 120/2021 art. 4 alin. (4). Nu am putut verifica dacă PDF-urile SPV actuale poartă un sigiliu calificat emis de un prestator de pe lista de încredere sau un certificat din rădăcina proprie MFP. Pentru un validator automat, contează.
- **Identitate electronică:** pentru SPV, PF folosesc certificat calificat sau NPOTP, aprobat electronic (cu o informație fiscală cunoscută), la ghișeu sau prin video (OPANAF 1090 art. 17 alin. (6) lit. c)). Nu există o cale „login cu banca”, iar ANAF nu apare printre furnizorii conectați la ROeID. PJ au nevoie obligatoriu de certificat calificat (art. 80 alin. (1) lit. a)). Un certificat calificat obținut prin bancă (de exemplu ING cu InfoCert) este, teoretic, un „certificat calificat” în sensul ordinului, dar nu am verificat dacă poate fi folosit la autentificarea SPV, care cere certificat client TLS pe token.
- **De ce contează pentru Raiffeisen (eficiență operațională):** datele folosibile automat sunt registrele publice prin API (W1), venitul și CAF-ul prin protocolul PJDP (W2/W3, la cel mult 3 zile lucrătoare, cu XML structurat F2102/F2103) și recipisele sau actele proprii prin SPVWS2. Verificarea documentelor aduse de client rămâne semi-manuală (VerifAdev) și poate fi parțial automatizată prin validarea criptografică.

## Incertitudini

- **Termenul de răspuns ANAF în protocolul PJDP:** 3 zile lucrătoare în anexă (art. 7), dar 1 zi lucrătoare în anexa 4. Probabil procedura tehnică e mai strictă; neclarificat.
- **Modificări ale OPANAF 146/2022 și OPANAF 1090/2022 după publicare:** nu am găsit acte modificatoare la căutarea după titlu pe legislatie.just.ro. Nu am verificat fișa „Acțiuni suferite”, care se încarcă dinamic. Status „în vigoare” = probabil.
- **Statutul OPANAF 1090/2022 art. 24 alin. (2)** („15 zile”) după OG 31/2022: neverificat dacă a fost adaptat formal.
- **Protocoalele concrete ale Raiffeisen, ING, CEC etc. cu ANAF** (numere, date): nepublice, neverificate. OPANAF 146/2022 art. 10 confirmă doar că protocoalele cu instituțiile de credit existau înainte de 2022 și trebuiau reîncheiate în 6 luni.
- **Specificațiile XSD F2101, F2102 și F2103:** anexa 4 spune că „se publică… pe platformele dedicate” (în spațiul privat al protocolului). Nu sunt publice, deci conținutul exact al câmpurilor de venit este neverificat.
- **Lanțul de certificate al PDF-urilor SPV actuale** (rădăcina MFP 2007 sau un prestator calificat): neverificat, pentru că nu am avut un PDF SPV real. MF nu figurează ca prestator pe lista de încredere RO (2026-09-28).
- **Serverul exact al endpoint-ului `POST /api/validate/signature`** (e-Factura) și **cât timp rămâne verificabil în VerifAdev un document**, altul decât CRF (pentru CRF: 1 an): neverificate.
- **OPANAF 1923/2022** (lista fără restanțe): citat prin Catalog, textul nu l-am citit.
- **ROeID:** lista furnizorilor poate fi paginată; afirmația „ANAF nu e conectat” se bazează pe pagina vizibilă la 2026-10-28.
- **„Interconectare ANAF–bănci” sau proiecte PNRR/cloud dedicate băncilor (2024–2026):** nu am găsit surse primare. Planul strategic 2025‑2028 nu le menționează. Căutarea web a fost blocată (DuckDuckGo, Bing și Brave au cerut captcha), deci pot exista comunicate de presă pe care nu le-am văzut.
- **SPVWS2 „InterogariBanci”:** sursa este README-ul GitHub MfpAnaf din 2019. Neverificat dacă funcția mai există în SPV-ul actual.
- **Pagina BT** (403) și paginile BRD, BCR și UniCredit despre verificarea ANAF nu au putut fi citate.
