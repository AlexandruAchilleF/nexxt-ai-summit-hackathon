# 02 – Informații transmise de bănci către ANAF (intern, la cerere și periodic), registrul conturilor, secretul bancar, cereri de control/antifraudă

Data cercetării: 2026-10-28. Cluster: bancă → ANAF (intern). Popririle, schimbul internațional (CRS/FATCA/DAC) și plățile sunt tratate doar tangențial (alte clustere).

Notă de metodă: motoarele de căutare (DuckDuckGo, Bing, Mojeek, Brave) au blocat interogările automate. Sursele au fost găsite prin căutarea internă legislatie.just.ro (`/Public/RezultateCautare`), prin paginile ANAF (static.anaf.ro) și ONPCSB, plus Google News RSS pentru presă. Textul consolidat al Codului de procedură fiscală (CPF) de pe legislatie.just.ro nu s-a încărcat (pagina de 60 KB nu conține corpul actului). Am folosit forma consolidată publicată de ANAF („ultima actualizare: Legea nr. 170/2026”, nu este republicare oficială) și am verificat istoricul articolelor-cheie direct în actele modificatoare. În textul ANAF exponenții apar lipiți: „ART. 611” înseamnă art. 61^1.

---

## Documente

Legendă: [P] = sursă primară, [S] = sursă secundară.

### A. Codul de procedură fiscală și actele care l-au modificat

1. **Legea nr. 207/2015 privind Codul de procedură fiscală** (Parlamentul; MO nr. 547/23.07.2015; în vigoare din 01.01.2016). [P]
   - URL legislatie: https://legislatie.just.ro/Public/DetaliiDocument/170005 (ultima consolidare listată acolo: 08.05.2026, https://legislatie.just.ro/Public/DetaliiDocument/309358).
   - Text consolidat ANAF, actualizat până la Legea nr. 170/2026: https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm
   - Status: în vigoare, modificat de multe ori. Articole relevante:
     - **art. 11** secretul fiscal. Alin. (2) include explicit „plăți, conturi, rulaje, transferuri de numerar, solduri”. Alin. (6) prevede schimbul de date prin PatrimVen, iar alin. (10) temeiul GDPR.
     - **art. 58** obligația terților de a furniza informații; „Cererea de furnizare a informațiilor se formulează în scris” (alin. 2).
     - **art. 59** furnizare periodică (pentru contribuabil, nu pentru bănci).
     - **art. 61** obligația instituțiilor de credit, de plată și emitente de monedă electronică.
     - **art. 61^1** Registrul central electronic pentru conturi.
     - **art. 62** CRS/FATCA, raportare anuală până la 15 mai.
     - **art. 62^1** acces la informațiile AML ale entităților raportoare.
     - **art. 64** prezentarea de înscrisuri de către terți.
     - **art. 66–67** dreptul de a refuza.
     - **art. 68–70^1** colaborare și PatrimVen.
     - **art. 127 alin. (1) lit. f)** suspendarea inspecției pentru informații de la terți.
     - **art. 136–137^1** controlul antifraudă.
     - **art. 138–147** verificarea situației fiscale personale (VSFP).
     - **art. 148–149** verificarea documentară.
     - **art. 336** contravenții.

2. **OUG nr. 111/2020** privind modificarea și completarea Legii nr. 129/2019 (Guvernul; MO nr. 620/15.07.2020). [P] https://legislatie.just.ro/Public/DetaliiDocument/227817
   - Status: în vigoare. Declară că „transpune Directiva (UE) 2018/843” (AMLD5).
   - Art. VI a rescris **art. 61 CPF**: a extins obligația la instituțiile de plată și la cele emitente de monedă electronică, a introdus raportarea zilnică cu IBAN, beneficiar real și împuterniciți, și transmiterea lunară de către ONPCSB. Tot art. VI a introdus **art. 61^1** (Registrul central).
   - Art. V a completat art. 218 din OUG 99/2006.
   - Art. VIII prevede: 90 de zile pentru raportarea inițială de către instituțiile de plată și cele emitente de monedă electronică, plus emiterea ordinului ANAF în 30 de zile.

3. **Legea nr. 296/2023** privind unele măsuri fiscal-bugetare (MO nr. 977/27.10.2023). [P] https://legislatie.just.ro/Public/DetaliiDocument/275745
   - Art. LX pct. 2 modifică **art. 61^1 alin. (4)**: accesul la datele din registru este extins și la „instituțiilor publice sau de interes public”.
   - Art. III pct. 13 modifică **art. 117 din Codul fiscal**: veniturile cu sursă neidentificată se impozitează cu **70%**, de la 01.07.2024.

4. **OUG nr. 71/2025** pentru modificarea și completarea Legii nr. 207/2015 (MO nr. 1146/10.12.2025). [P] https://legislatie.just.ro/Public/DetaliiDocument/305122
   - Pct. 26 reformulează **art. 336 alin. (1) lit. i) și j)**, adică sancțiunea pentru încălcarea art. 61.
   - Pct. 27 și 30 introduc lit. ț^5) și k^5): amendă pentru încălcarea art. 62^1.
   - Restul actului privește DAC8 (criptoactive, art. 291^6).

5. **Alte acte modificatoare ale CPF din 2023–2026**, verificate pe lista articolelor modificate. Niciunul nu atinge art. 58, 61, 61^1 sau 138: [P]
   - OG nr. 16/2023 (MO 86/31.01.2023): https://legislatie.just.ro/Public/DetaliiDocument/264508
   - OG nr. 11/2025 (MO 695/24.07.2025), aprobată prin Legea nr. 52/2026 (MO 309/17.04.2026): https://legislatie.just.ro/Public/DetaliiDocument/300006 și https://legislatie.just.ro/Public/DetaliiDocument/309645
   - OG nr. 1/2026 (MO 75/30.01.2026): https://legislatie.just.ro/Public/DetaliiDocument/306795
   - OG nr. 6/2026 (MO 77/30.01.2026): https://legislatie.just.ro/Public/DetaliiDocument/306803
   - Conținutul Legii nr. 170/2026 nu a fost verificat (vezi Incertitudini).

### B. Ordinele ANAF de aplicare a art. 61 / 61^1 (raportarea conturilor și registrul)

6. **OPANAF nr. 3746/2020** privind organizarea și funcționarea Registrului central electronic pentru conturi de plăți și conturi bancare identificate prin IBAN și pentru aprobarea procedurii art. 61 (ANAF; MO nr. 1015/02.11.2020). [P] https://legislatie.just.ro/Public/DetaliiDocument/231985
   - Status: în vigoare, modificat de OPANAF 393/2026. **Abrogă OPANAF 3770/2015** (art. 5).
   - Anexa 1 tratează registrul. Gestionarea este ANAF, iar sistemul informatic este pus la dispoziție de „Centrul Național pentru Informații Financiare” (art. 1). Păstrarea datelor este de 10 ani (art. 6). Accesul se face pe bază de protocol, iar pentru ONPCSB este „direct accesibil[e], fără întârziere și fără a fi filtrate” (art. 7).
   - Anexa 2 este procedura art. 61: termene (art. 4), canal (art. 5), raportare zilnică (art. 6–7) și caselor centrale ale cooperativelor de credit (art. 8).
   - Anexa 3 tratează înrolarea.

7. **OPANAF nr. 393/2026** privind modificarea OPANAF 3746/2020 (ANAF; MO nr. 259/01.04.2026). [P] https://legislatie.just.ro/Public/DetaliiDocument/308978
   - Status: în vigoare.
   - Raportarea zilnică se face „în format structură XML generat și validat, exclusiv în format electronic”, după structura publicată pe site-ul ANAF.
   - Se adaugă datele din art. 15 alin. (1^1) din Legea 129/2019 (sediul real al persoanei juridice).
   - Art. II: „În termen de 10 zile lucrătoare … instituțiile raportoare vor transmite informații exclusiv prin utilizarea versiunii actualizate a structurii XML”.

8. **OPANAF nr. 3770/2015** pentru aprobarea Procedurii de furnizare de informații conform art. 61 (MO nr. 983/30.12.2015). [P] https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3770_2015.pdf (pe legislatie: https://legislatie.just.ro/Public/DetaliiDocument/174503)
   - Status: **abrogat** prin OPANAF 3746/2020. Are valoare istorică și descrie canalul tehnic.
   - Băncile transmiteau „în fiecare zi lucrătoare … informaţiile aferente zilei bancare anterioare”, prin „conexiunii securizate, asigurată de către Ministerul Finanţelor Publice prin Serviciul de Telecomunicaţii Speciale”.
   - Raportarea inițială a acoperit conturile închise în perioada 01.01.2010 – 31.03.2016.

9. **OPANAF nr. 1106/2016** (MO nr. 239/31.03.2016): https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1106_2016.pdf [P]
   - A amânat formatul complet la 30.06.2016. Presa a relatat amânarea [S]: economica.net, 31.03.2016, https://www.economica.net/raportarea-zilnica-la-anaf-a-tuturor-celor-ce-deschid-sau-inchid-conturi-in-banci-amanata-pana-la-1-iulie_117328.html (Google News îl datează greșit „01 Jul 2026”).
   - **OPANAF nr. 3592/2016** (MO nr. 1017/19.12.2016): https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3592_2016.pdf [P]. Organele fiscale locale primesc datele despre conturi „exclusiv în format electronic, prin intermediul sistemului informatic PatrimVen”. Ambele ordine sunt abrogate implicit odată cu 3770/2015.

10. **OMFP nr. 2632/2016** pentru dezvoltarea serviciilor MFP puse la dispoziția autorităților prin sistemul informatic propriu (PatrimVen) (MFP; 08.11.2016; MO nr. 917/15.11.2016). [P] https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_2632_2016.pdf
    - Reglementează protocolul de aderare la PatrimVen, folosit și pentru datele din registru (art. 61^1 alin. 4 și art. 70^1 CPF).

### C. Accesul autorităților penale la registru (Directiva (UE) 2019/1153)

11. **OG nr. 9/2021** privind stabilirea unor măsuri de facilitare a utilizării informațiilor financiare și a analizelor financiare în scopul prevenirii, depistării, investigării sau urmăririi penale a anumitor infracțiuni (MO nr. 831/31.08.2021). [P] https://legislatie.just.ro/Public/DetaliiDocument/245921
    - Aprobată prin **Legea nr. 169/2022** (MO 548/06.06.2022): https://legislatie.just.ro/Public/DetaliiDocument/255939
    - Obiecția de neconstituționalitate a fost respinsă „ca inadmisibilă” prin **Decizia CCR nr. 247/2022** (MO 533/31.05.2022): https://legislatie.just.ro/Public/DetaliiDocument/255822
    - Art. 2 lit. a) definește „registru centralizat de conturi bancare” ca registrul de la art. 61^1 CPF.
    - Art. 3 enumeră autoritățile cu acces: Poliția Română, Poliția de Frontieră, DGA, PÎCCJ, DNA, DIICOT, parchetele de pe lângă curți și tribunale, DLAF și ANABI.
    - Art. 4–6 reglementează accesul „în mod direct și fără întârziere” pentru infracțiuni grave, analiza „de la caz la caz”, jurnalul accesărilor ținut de ANAF timp de 5 ani și verificarea trimestrială făcută de responsabilii cu protecția datelor.
    - Context [S]: Europa Liberă, 30.08.2021: „vor avea acces la locul de depozitare al banilor, nu și la rulajul sumelor”, https://romania.europalibera.org/a/procurori-acces-la-conturi-bancare/31435460.html. Același publicație, 29.06.2021, despre blocajul anterior și cererile punctuale ale procurorilor: https://romania.europalibera.org/a/control-conturi-bancare-parchet/31331807.html

### D. Combaterea spălării banilor și secretul bancar

12. **Legea nr. 129/2019** pentru prevenirea și combaterea spălării banilor și finanțării terorismului (MO nr. 589/18.07.2019). [P]
    - Act de bază: https://legislatie.just.ro/Public/DetaliiDocument/216157. Consolidarea din 26.10.2025: https://legislatie.just.ro/Public/DetaliiDocument/303053
    - Status: în vigoare, modificată între altele de OUG 111/2020, OUG 53/2022, OUG 10/2025 (MO 225/13.03.2025) și **Legea nr. 86/2025** (MO 483/23.05.2025).
    - **art. 1 alin. (1) lit. b)** include în cadrul național AML „autorități cu atribuții de control financiar/fiscal”. Această includere este relevantă pentru accesul ANAF la registru prin art. 61^1 alin. (5).
    - **art. 7**: tranzacții în numerar de minimum 10.000 euro (alin. 1); transferuri externe în și din conturi de minimum 10.000 euro (alin. 3); remitere de bani de minimum 2.000 euro (alin. 5). Raportarea se face „în cel mult 3 zile lucrătoare” (alin. 7). ANAF transmite către ONPCSB declarațiile de numerar la frontieră (alin. 6).
    - **art. 15 alin. (1), (1^1)** stabilește datele de identificare preluate în registrul conturilor.
    - **art. 33 alin. (2)–(4)**: entitățile răspund ONPCSB în maximum 15 zile; au obligația de a institui sisteme pentru a răspunde complet și rapid dacă au avut o relație de afaceri în ultimii 5 ani; secretul bancar „nu [este] opozabil Oficiului”.
    - **art. 34** reglementează diseminarea către parchet, SRI, organele de urmărire penală și autoritățile competente (alin. 4).
    - **art. 43** contravenții și amenzi.
    - **art. 55** a modificat art. 61 alin. (4) CPF: de la intrarea în vigoare a legii, ONPCSB transmite lunar rapoartele către ANAF, în locul raportării concomitente făcute de entități.

13. **OUG nr. 99/2006** privind instituțiile de credit și adecvarea capitalului (MO nr. 1027/27.12.2006). [P] Consolidarea din 20.03.2025: https://legislatie.just.ro/Public/DetaliiDocument/295420
    - **art. 111** confidențialitatea, inclusiv „informații referitoare la conturile clienților - solduri, rulaje, operațiuni derulate”.
    - **art. 113 alin. (2) lit. c)** permite furnizarea „la solicitarea scrisă a altor autorități sau instituții … dacă prin lege specială aceste autorități sau instituții sunt îndrituite … și sunt identificate clar informațiile care pot fi furnizate”, și doar „în măsura în care acestea sunt justificate de scopul pentru care sunt cerute”.
    - **art. 113 alin. (3)**: cererea trebuie să precizeze „temeiul legal …, identitatea clientului …, categoria informațiilor solicitate și scopul”.
    - **art. 114** prevede furnizarea către procuror sau instanță.
    - Notă: RIL ÎCCJ nr. 9/2024 (MO 637/04.07.2024) privește accesul moștenitorilor.

### E. Control: VSFP, metode indirecte, antifraudă

14. **OPANAF nr. 2117/2018** privind formularele VSFP (MO nr. 766/05.09.2018). [P] https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2117_2018.pdf
    - Modelul de aviz de verificare cere persoanei să prezinte „sub sancțiunea decăderii, extrasele conturilor bancare” în termen de 60 de zile.

15. **OPANAF nr. 2778/2020** privind competența VSFP (MO nr. 565/29.06.2020). [P] https://legislatie.just.ro/Public/DetaliiDocument/227433
    - Modificat de OPANAF 1286/2022 (https://legislatie.just.ro/Public/DetaliiDocument/257694).
    - Modificat de **OPANAF nr. 768/2026** (MO nr. 553/06.07.2026), https://legislatie.just.ro/Public/DetaliiDocument/312044. Acesta adaugă la art. 2 lit. d): „Direcția generală antifraudă fiscală” devine organ competent pentru VSFP.
    - Activitățile preliminare (analiza de risc) rămân la Direcția generală control venituri persoane fizice (art. 1).
    - [S] Presa a relatat schimbarea: „Antifrauda poate verifica acum veniturile persoanelor fizice” (Republica.ro, 14.07.2026, titlu din Google News RSS; textul nu a fost citit).

16. **OPANAF nr. 675/2018** privind metodele indirecte de stabilire a veniturilor (MO nr. 257/23.03.2018). [P]
    - Consolidarea din 07.04.2023: https://legislatie.just.ro/Public/DetaliiDocument/265963. Modificat de OPANAF 417/2023 (MO 292/07.04.2023) și **OPANAF 91/2026** (MO 69/29.01.2026, https://legislatie.just.ro/Public/DetaliiDocument/306749).
    - Metodele sunt: „a) metoda sursei și utilizării fondului; b) metoda fluxurilor de trezorerie; c) metoda patrimoniului net” (art. 1 alin. 2).
    - Anexa 2 conține modelul notificării de conformare (art. 140^1 CPF).

17. **OPANAF nr. 3076/2017** privind formularele structurilor de informații fiscale (MO nr. 911/21.11.2017). [P] https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3076_2017.pdf
    - Anexa 3.a este „Cerere de furnizare a informațiilor/înscrisurilor”, adresată terților în baza art. 58 și 64 CPF, cu termen fixat în cerere.

18. **OUG nr. 74/2013** privind reorganizarea ANAF (MO nr. 389/29.06.2013). [P, forma publicată inițial] https://static.anaf.ro/static/10/Anaf/legislatie/OUG_74_2013.pdf
    - **Art. 6 alin. (2)** stabilește drepturile inspectorilor antifraudă, între care lit. k) „să solicite, în condițiile legii, date sau, după caz, documente, de la orice entitate privată şi/sau publică” și lit. n) baze de date „inclusiv ale altor instituții”, pe bază de protocol. Forma consolidată nu a fost verificată.

### F. Raportare periodică a prestatorilor de plăți (nu AML)

19. **Codul fiscal (Legea nr. 227/2015)**, consolidat: https://legislatie.just.ro/Public/DetaliiDocument/171282 [P]
    - **art. 321^2** „Obligațiile generale ale prestatorilor de servicii de plată” (CESOP, Directiva (UE) 2020/284), introdus prin **Legea nr. 33/2024** (MO 179/05.03.2024), în vigoare din 08.03.2024.
    - Se raportează plățile transfrontaliere, când sunt „peste 25 de plăți transfrontaliere către același beneficiar” într-un trimestru, „până cel târziu la sfârșitul lunii care urmează după expirarea trimestrului”.
    - **art. 117**: impozit de 70% pe veniturile cu sursă neidentificată.

20. **OPANAF nr. 825/2024** privind formularul (396) „Declarație informativă privind plățile transfrontaliere efectuate de prestatorii de servicii de plată” (MO nr. 398/29.04.2024). [P] https://legislatie.just.ro/Public/DetaliiDocument/282330
    - Se aplică de la trimestrul 1/2024. Punerea în aplicare revine DGAF și Unității de management al informației, cu suport CNIF.
    - Formularul (709) pentru prestatorii nerezidenți este aprobat prin Ordinul nr. 6.508/2024, https://legislatie.just.ro/Public/DetaliiDocument/289823 (emitentul nu a fost verificat).

### G. Protocoale, strategie, statistici, presă

21. **OPANAF nr. 3731/2016**, model-cadru de protocol ANAF – instituții financiare nebancare (MO nr. 17/09.01.2017). [P] https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3731_2016.pdf
    - Fluxul este invers (ANAF → creditor), în baza art. 11 alin. (3) lit. d) CPF, cu consimțământul persoanei.
    - **Abrogat** prin OPANAF 146/2022, art. 10 alin. (3). OPANAF 146/2022 este actul în vigoare pentru bănci, IFN-uri și alte persoane juridice de drept privat. Detalii în [05-digital.md](05-digital.md), W2.

22. **ANAF – Plan strategic 2025–2028**: https://static.anaf.ro/static/10/Anaf/Informatii_R/240917_%20Plan_%20StrategicANAF2025-2028.pdf [P]
    - Menționează „Proiectul Big Data” și „registrului electronic al riscurilor” (p. 5).
    - Menționează și „Adoptarea unei noi legi prin care au fost revizuite competențele organelor de inspecție fiscală, organelor de control antifraudă și organelor responsabile cu verificarea situației fiscale personale”. Actul nu este numit.

23. **ANAF – Strategia de creștere a conformării voluntare la impozitul pe venit 2023–2025**: https://static.anaf.ro/static/10/Anaf/Informatii_R/Strategia_PIT_23112022.pdf [P]
    - Măsura 4.1.a: valorificarea în analizele de risc a „datelor din conturile bancare din țară”.
    - Măsura 4.1.f: „Valorificarea datelor privind soldurile conturilor bancare interne, soldurile conturilor din străinătate, volumul de tranzacții efectuate cu cardul…”.
    - Măsura 4.1.b: propunerea de raportare periodică pentru emitenții de carduri. Este doar o propunere, nu am găsit că ar fi fost legiferată.

24. **ONPCSB – Raport de activitate 2025** (PDF scanat, citit prin OCR și verificat vizual pe paginile 9–10): https://www.onpcsb.ro/uploads/articole/attachments/6a392cbe767d9833303636.pdf [P]
    - Statisticile sunt detaliate la secțiunea „Statistici” din Explicații.

25. **CNIL – „FICOBA : Fichier national des comptes bancaires et assimilés”**: https://www.cnil.fr/fr/ficoba-fichier-national-des-comptes-bancaires-et-assimiles [P]
    - Confirmă că **FICOBA este fișierul francez (DGFiP)**. În sursele primare românene NU apare denumirea „FICOBA”. Denumirea oficială în România este „Registrul central electronic pentru conturi de plăți și conturi bancare identificate prin IBAN” (art. 61^1 CPF). În presă apare și ca „Registrul IBAN” sau „registrul conturilor bancare”.

26. **Directive UE**. Textul nu a fost citit în sesiune: EUR-Lex a blocat accesul automat. Conținutul este confirmat indirect de actele românești de transpunere.
    - Directiva (UE) 2018/843 (AMLD5, art. 32a, mecanisme centralizate de conturi): https://eur-lex.europa.eu/legal-content/RO/TXT/?uri=CELEX:32018L0843
    - Directiva (UE) 2019/1153: https://eur-lex.europa.eu/legal-content/RO/TXT/?uri=CELEX:32019L1153
    - Directiva (UE) 2020/284 (CESOP): https://eur-lex.europa.eu/legal-content/RO/TXT/?uri=CELEX:32020L0284

27. **Presă 2025–2026** [S]:
    - EVZ, 06.11.2025, „ANAF va ști exact câte conturi avem…” (despre proiectul devenit OPANAF 393/2026). Citează proiectul ANAF: erori de tipul „conturi fără CNP, conturi fără beneficiar real, … data de naștere înlocuită cu data de deschiderii contului … conturi ale nerezidenților fără NIF”. https://evz.ro/anaf-va-sti-exact-cate-conturi-avem-cum-vor-fi-urmarite-toate-tranzactiile.html
    - economica.net, 15.06.2026, rezultatele VSFP: https://www.economica.net/anaf-mari-averi-nejustificate-creante-fiscale-de-peste-540-de-milioane-de-lei-stabilite-in-ultimul-an-la-persoane-fizice-care-nu-au-putut-justifica-provenienta-unor-venituri_950365.html
    - Economedia, 08.10.2026, președintele ANAF Adrian Nica: „Noi vă verificăm banii încasați, conturile bancare…”. https://economedia.ro/video-harta-de-impact-presedintele-anaf-adrian-nica-mesaj-pentru-cei-care-vand-fara-factura-va-vedem-prin-conturile-bancare-si-curier-faceti-facturi-ca-va-fi-mai-usor.html
    - Titluri văzute doar în Google News RSS (https://news.google.com/rss/search?q=ANAF+conturi+bancare&hl=ro&gl=RO&ceid=RO:ro), cu textul necitit:
      - profit.ro, 05.11.2025: „ANAF schimbă modul în care băncile transmit date despre clienți și conturi…”
      - profit.ro, 12.09.2026: „Băncile vor verifica la ANAF persoanele fizice și firmele după ce acestea își dau acordul prin SPV”
      - profit.ro/news.ro, 11.09.2024: Cătălin Lefter (ARB), „Băncile s-au transformat într-un fel de procurori fiscali. Raportăm la Fisc ce depășește 25 de tran[zacții]”, trimitere la CESOP
      - Agerpres/ProTV, 12.05.2026: „80.000 de firme … nu mai au cont bancar … nu pot avea popriri”
      - Digi24/Economica, 11.01.2021: registrul a devenit operațional („De astăzi, ANAF are acces la toate conturile bancare”)

---

## Workflow-uri

### W1. Declararea zilnică a conturilor deschise/închise și a casetelor de valori (registrul IBAN)
1. **Banca, instituția de plată sau cea emitentă de monedă electronică** își înrolează un administrator. Administratorul se prezintă personal la organul fiscal de pe raza sediului social și are certificat digital calificat. Apoi înregistrează utilizatorii. Temei: OPANAF 3746/2020 anexa 3 art. 4 alin. (1) (modificat de 393/2026). Procedura istorică detaliată se găsește în OPANAF 3770/2015 anexa A și A1–A3.
2. **Banca** generează **zilnic** fișierul XML cu: lista titularilor (persoane fizice, persoane juridice, entități fără personalitate juridică) care deschid sau închid conturi bancare ori de plăți; persoanele cu drept de semnătură; persoanele care pretind că acționează în numele clientului; beneficiarii reali. Pentru fiecare se transmit datele din art. 15 alin. (1) și (1^1) din Legea 129/2019 (sau un număr unic de identificare), IBAN-ul și data deschiderii și închiderii. Se raportează și chiriașii casetelor de valori, cu data încetării contractului. Temei: art. 61 alin. (2) CPF; OPANAF 3746/2020 anexa 2 art. 6.
   - Istoric (OPANAF 3770/2015): datele erau „aferente zilei bancare anterioare” și se raportau și modificările survenite după deschidere. Ordinul în vigoare spune doar „zilnic”.
3. **Canal**: încărcare prin „mijloace de transmitere la distanță” a unei „raportări în format structură XML generat și validat, exclusiv în format electronic”, după structura și specificațiile publicate pe site-ul ANAF. Temei: OPANAF 3746/2020 anexa 2 art. 3 și 7, după 393/2026.
   - Istoric: conexiune securizată STS și extranet ANAF, „Spațiul meu de lucru” (OPANAF 3770/2015 anexa A).
4. **Termen**: zilnic. După OPANAF 393/2026, din 01.04.2026 plus 10 zile lucrătoare se folosește exclusiv noua versiune a XML (art. II).
5. **ANAF/CNIF**: registrul se actualizează automat la fiecare transmitere (anexa 1 art. 5). Datele se păstrează 10 ani de la încetarea relației de afaceri (art. 61^1 alin. 7; anexa 1 art. 6).
6. **Banca** păstrează informațiile 10 ani de la încetarea relației sau de la tranzacția ocazională (art. 61 alin. 5).
7. **Cooperativele de credit** raportează centralizat, prin casa centrală (anexa 2 art. 8).
- Sancțiune: art. 336 alin. (1) lit. i)/j) și alin. (2) lit. d) CPF (vezi Explicații).

### W2. Cererea punctuală ANAF către bancă: rulaje, solduri, extrase, documente (art. 61 alin. 1)
1. **Structura ANAF** care are nevoie de date (inspecție fiscală, VSFP, antifraudă, informații fiscale) identifică băncile la care persoana are conturi. Identificarea se face consultând registrul IBAN (W6), aceasta fiind o deducție logică din art. 61^1 alin. (1)–(3). Nu este o etapă prescrisă explicit.
2. **ANAF → bancă**: solicitarea se transmite „prin portal, sub forma unui fișier PDF cu xml atașat, semnat electronic” (OPANAF 3746/2020 anexa 2 art. 5). Solicitarea vizează „pentru fiecare titular … toate rulajele și/sau soldurile conturilor … precum și informațiile și documentele privind operațiunile” (art. 61 alin. 1 CPF). Conținutul minim cerut de legea bancară: temeiul legal, identitatea clientului, categoria informațiilor și scopul (OUG 99/2006 art. 113 alin. 3).
3. **Banca → ANAF**: răspunsul este o „declarație în format PDF cu fișier xml atașat”, iar documentele privind operațiunile se trimit „sub forma unui fișier xml atașat la formularul PDF”. Este necesar certificat digital calificat (anexa 2 art. 5).
4. **Termene, calculate de la data solicitării** (anexa 2 art. 4):
   - **5 zile lucrătoare** pentru rulaje sau solduri pe o perioadă de până la 3 ani;
   - **10 zile lucrătoare** pentru rulaje sau solduri pe o perioadă mai mare de 3 ani;
   - **25 de zile lucrătoare** pentru informații și documente suplimentare privind operațiunile.
5. **ANAF** poate folosi datele „doar în scopul îndeplinirii atribuțiilor specifice ale organului fiscal central” (art. 61 alin. 4). Datele devin secret fiscal (art. 11 alin. 2).
- Consimțământul clientului **nu** este necesar: temeiul este legea specială (art. 61 CPF și OUG 99/2006 art. 113 alin. 2 lit. c).

### W2b. Cererea generală de informații sau înscrisuri către un terț (art. 58, 64)
1. **Organul fiscal** emite formularul „Cerere de furnizare a informațiilor/înscrisurilor” (OPANAF 3076/2017, anexa 3.a). Cererea este scrisă și precizează natura informațiilor (art. 58 alin. 2 CPF).
2. **Termenul** este fixat în cerere („până la data de …”). Codul nu prevede un termen general.
3. **Terțul** depune la registratură, prin poștă sau prin mijloace electronice (art. 64 alin. 2).
4. Nerespectarea se sancționează: art. 336 alin. (1) lit. r) pentru art. 58 și lit. c) pentru art. 64 (vezi Explicații). Formularul însuși menționează aceste sancțiuni.

### W3. Verificarea situației fiscale personale (VSFP) – cum ajung datele bancare în dosar
1. **Analiza de risc**, la nivel central (DGCVPF, conform OPANAF 2778/2020 art. 1). „Diferența este semnificativă dacă … este o diferență mai mare de 10% din veniturile declarate, dar nu mai puțin de 50.000 lei” (art. 138 alin. 2 lit. a CPF).
   - Sursele de date nu sunt enumerate de lege. Ordinul de metode indirecte și Strategia PIT menționează datele din conturile bancare din țară și soldurile (Strategia PIT 4.1.a și 4.1.f).
2. **Notificarea de conformare**: persoana are 30 de zile pentru a depune sau corecta declarații, timp în care ANAF nu o selectează (art. 140^1 alin. 2; modelul în OPANAF 675/2018 anexa 2). Persoanele cu risc ridicat care nu remediază situația „sunt supuse obligatoriu” VSFP sau unei verificări documentare (art. 140^1 alin. 4).
3. **Selecția**: după risc, cu maximum 10% selecție aleatorie (art. 138 alin. 2^1).
4. **Avizul de verificare**: persoana trebuie să prezinte în cel mult **60 de zile**, „sub sancțiunea decăderii”, documente justificative, inclusiv **extrasele conturilor bancare**, și declarația de patrimoniu și venituri. Termenul se poate prelungi o dată cu 30 de zile. Temei: art. 138 alin. (6)–(7) și art. 141 CPF; OPANAF 2117/2018 anexa 1.a.
5. **Date de la terți și bănci, fără consimțământ**: art. 139 lit. d) („de la persoane cu care aceasta a avut ori are raporturi economice”), art. 138 alin. (15) (dacă informațiile persoanei „sunt insuficiente … se poate adresa și altor persoane”), art. 61 alin. (1) cu procedura W2. Verificarea se poate suspenda pentru informații de la terți (art. 142 alin. 1 lit. d).
6. **Metode indirecte**: sursa și utilizarea fondurilor, fluxurile de trezorerie, patrimoniul net (OPANAF 675/2018 art. 1 alin. 2). Proporționalitatea este impusă de art. 138 alin. (11).
7. **Durata** este de maximum 270 de zile (art. 140 alin. 6). Urmează raportul (art. 145), cu punctul de vedere al persoanei în 5 zile lucrătoare, și decizia de impunere (art. 146). Veniturile cu sursă neidentificată se impozitează cu 70% (Codul fiscal art. 117).
8. **De la 06.07.2026, și DGAF** poate efectua VSFP (OPANAF 768/2026). Verificarea documentară poate fi făcută de inspecție, antifraudă și VSFP (art. 148 alin. 3 și 5) și are termen de 30 de zile pentru documente (art. 149 alin. 2).

### W4. Inspecția fiscală (persoane juridice sau fizice)
1. Organul de inspecție solicită date de la bancă prin W2 (art. 61 alin. 1) sau W2b (art. 58, 64).
2. Poate suspenda inspecția „pentru solicitarea de informații sau documente de la autorități, instituții ori terți” (art. 127 alin. 1 lit. f), pentru maximum 6 luni (alin. 2). Contribuabilul are dreptul să fie informat despre probele obținute, cu excepții pentru interes general (alin. 9).

### W5. Cererea antifraudă (DGAF)
1. **DGAF** efectuează control operativ, fără informarea prealabilă a contribuabilului, „în baza analizei de risc” (art. 136 alin. 1–2 CPF). Excepțiile de la analiza de risc sunt prevăzute la alin. (3).
2. **Cererea către bancă**: drepturile inspectorilor antifraudă de a solicita date și documente oricărei entități private (OUG 74/2013 art. 6 alin. 2 lit. k, aplicabil prin art. 137 alin. 1 CPF). DGAF face parte din „organul fiscal central”, deci poate folosi art. 61 alin. (1) și canalul W2. Nu am găsit un canal dedicat DGAF (neverificat).
3. **Rezultatul**: proces-verbal de control, cu punctul de vedere al contribuabilului în 5 zile lucrătoare (art. 137^1). Sesizarea penală se face prin art. 132 sau art. 6 alin. (2) lit. e) din OUG 74/2013.
4. **Fluxuri de date gestionate de DGAF**: formularul (396) CESOP (OPANAF 825/2024 art. 6) și, de la 06.07.2026, VSFP.

### W6. Interogarea registrului central de conturi (cine, cum, ce date)
1. **Date disponibile** (art. 61^1 alin. 3 CPF):
   - a) titularul, persoanele cu drept de semnătură și persoanele care acționează în numele clientului, cu datele de identificare din art. 15 alin. (1) Legea 129/2019;
   - b) beneficiarul real;
   - c) IBAN-ul, data deschiderii și data închiderii;
   - d) casetele de valori: concesionarul și durata.
   - **Nu conține solduri sau rulaje.**
2. **ANAF** are acces intern, ca gestionar (OPANAF 3746/2020 anexa 1 art. 1).
3. **ONPCSB** are acces direct, „fără întârziere și fără a fi filtrate” (art. 61^1 alin. 5). Pe baza informațiilor din registru, ONPCSB răspunde și altor unități de informații financiare (art. 9^1 Legea 129/2019).
4. **Autoritățile din cadrul național AML** (art. 1 Legea 129/2019: organe de urmărire penală, autorități de control fiscal sau financiar, autoritatea vamală, servicii de informații, BNR, ASF, ONJN) au acces pentru sarcinile AML/CFT, prin sistem informatic, pe bază de protocol publicat pe site-urile părților (art. 61^1 alin. 5–6).
5. **Organele fiscale locale și autoritățile sau instituțiile publice ori de interes public**: ANAF le transmite „la cererea justificată” **doar datele de la lit. a) și c)** (art. 61^1 alin. 4, după Legea 296/2023). Canalul este PatrimVen, după protocolul de aderare (OMFP 2632/2016; OPANAF 3592/2016; art. 70^1 CPF).
6. **Autoritățile penale din OG 9/2021**: poliție, parchete, DNA, DIICOT, DLAF și ANABI accesează „direct și fără întârziere” pentru infracțiuni grave, „de la caz la caz”, prin sistemul de la art. 70^1 CPF, pe bază de protocol cu ANAF (art. 4–5). ANAF ține un jurnal cu numărul dosarului, data și ora, utilizatorul și altele, pe care îl păstrează 5 ani. Jurnalul este verificat trimestrial de responsabilii cu protecția datelor și pus la dispoziția ANSPDCP la cerere (art. 6).
7. Toate solicitările și răspunsurile se fac „exclusiv în format electronic” (OPANAF 3746/2020 anexa 1 art. 8).

### W7. Fluxurile ONPCSB → ANAF (și invers)
1. **Banca → ONPCSB**: raportează tranzacțiile în numerar de cel puțin 10.000 euro, transferurile externe de cel puțin 10.000 euro (inclusiv operațiunile legate între ele) și remiterile de cel puțin 2.000 euro, „în cel mult 3 zile lucrătoare” (Legea 129/2019 art. 7 alin. 1, 3, 5, 7). Formatul este stabilit prin Ordinul ONPCSB 191/2023 (MO 625/10.07.2023), menționat în nota la art. 7. Textul ordinului nu a fost citit.
2. **ONPCSB → ANAF**: „transmite lunar către A.N.A.F. rapoartele pentru tranzacții cu sume în numerar, rapoartele privind transferurile externe în și din conturi și rapoartele privind activitățile de remitere de bani” (art. 61 alin. 3 CPF, după Legea 129/2019 art. 55 și OUG 111/2020). ANAF poate folosi aceste date doar pentru atribuțiile proprii (art. 61 alin. 4).
3. **ONPCSB → ANAF, informări spontane**, după analiză (art. 34 alin. 4 și art. 36 alin. 1, conform raportului ONPCSB). În 2025: **221 de informări** către ANAF din etapa de evaluare a riscului și **21 de informări** din analiza operațională (Raport ONPCSB 2025, p. 9–10).
4. **ANAF → ONPCSB**: rapoarte privind declarațiile de numerar la frontieră (art. 7 alin. 6 și 8 Legea 129/2019) și rapoarte de tranzacții suspecte în aplicarea Regulamentului (UE) 2018/1672 (art. 6 alin. 4).
5. Istoric (2016 – iulie 2019): entitățile raportoare transmiteau „concomitent şi pe acelaşi format” rapoartele și către ANAF (OPANAF 3770/2015, secțiunea III).

### W8. CESOP – raportarea trimestrială a plăților transfrontaliere (periodică, nu AML)
1. **Prestatorul de servicii de plată** (bancă, instituție de plată, instituție emitentă de monedă electronică) ține evidența beneficiarilor care primesc **peste 25 de plăți transfrontaliere** pe trimestru (Codul fiscal art. 321^2 alin. 2–4).
2. Depune formularul **396** electronic, „până cel târziu la sfârșitul lunii care urmează după expirarea trimestrului” (art. 321^2 alin. 7 lit. b; OPANAF 825/2024 anexa 1 pct. 2). Obligația există din T1/2024.
3. ANAF (DGAF și Unitatea de management al informației) primește datele. Folosirea lor în UE se face prin CESOP (Regulamentul (UE) 904/2010 art. 24b).

### W9. Distincția față de cererile penale (pe scurt)
- Procurorul și instanța cer informații sub secret bancar în baza OUG 99/2006 art. 114, cu aplicarea Codului de procedură penală („metodele speciale de supraveghere sau cercetare”). Textul art. 153 CPP (obținerea datelor privind situația financiară, cu încuviințarea judecătorului de drepturi și libertăți) **nu a fost verificat** în sesiune: pagina legislatie a CPP nu s-a încărcat.
- Accesul la registru al autorităților penale este reglementat separat (W6 pct. 6, OG 9/2021). Accesul la solduri și rulaje rămâne pe calea cererilor punctuale (Europa Liberă, 30.08.2021).

### W10. Fluxul invers ANAF → bancă, cu consimțământul clientului (context)
- Bazele legale sunt art. 11 alin. (3) lit. d) și alin. (6^1) și art. 70^1 alin. (1) lit. b) CPF. Actul în vigoare este OPANAF 146/2022, care a abrogat modelul IFN din OPANAF 3731/2016 ([05-digital.md](05-digital.md), W2).
- Un proiect de ordin din septembrie 2026 ar muta acordul în SPV (pentru firme exclusiv în SPV) și ar adăuga D100, D101 și D300. Presa (Financiarul, 12.09.2026) spune că nu este încă în vigoare; detalii în [05-digital.md](05-digital.md), punctul 15b.

---

## Explicații

### Secretul bancar față de ANAF
- Secretul acoperă explicit „solduri, rulaje, operațiuni derulate” (OUG 99/2006 art. 111 alin. 1). Excepția pentru ANAF este art. 113 alin. (2) lit. c): solicitare scrisă, lege specială care îndrituiește autoritatea și informații „identificate clar”.
- Legea specială este **art. 61 CPF**. Pentru registru este art. 61^1, iar pentru informațiile AML art. 62^1.
- Față de ONPCSB, secretul bancar „nu [este] opozabil” (Legea 129/2019 art. 33 alin. 4).
- Odată ajunse la ANAF, datele devin **secret fiscal** (CPF art. 11 alin. 1–2). Datele primite prin art. 61 alin. (1) și (3) nu pot fi retransmise prin excepțiile generale ale art. 11 alin. (3); se folosesc „doar în scopul îndeplinirii atribuțiilor specifice ale organului fiscal central” (art. 61 alin. 4).
- Excepție: datele din registru de la lit. a) și c) pot fi transmise autorităților publice sau de interes public, la cerere justificată (art. 61^1 alin. 4).

### Ce poate refuza banca
- CPF nu prevede un drept al băncii de a refuza cererile ANAF întemeiate pe art. 61. Drepturile de refuz din art. 66–67 privesc rudele și anumite profesii (avocați, consultanți fiscali, auditori, medici etc.), nu instituțiile de credit.
- *Interpretare, neverificată în jurisprudență*: banca poate cere completarea unei solicitări care nu cuprinde elementele art. 113 alin. (3) din OUG 99/2006 (temei, identitate, categorie, scop) sau care nu precizează „natura informațiilor” (art. 58 alin. 2 CPF). Poate furniza doar ce este „justificat de scopul pentru care sunt cerute” (art. 113 alin. 2).
- Autoritățile locale și alte autorități publice nu pot obține din registru beneficiarul real sau casetele de valori, doar datele de la lit. a) și c) (art. 61^1 alin. 4).
- ONPCSB poate refuza diseminarea către autorități, cu motivare (Legea 129/2019 art. 35 alin. 2 și 5). Aceasta nu este o prerogativă a băncii.

### Minimizarea datelor, GDPR și proporționalitate
- Temeiul prelucrării este obligația legală, cu trimiterea expresă la GDPR în CPF art. 11 alin. (10) și OPANAF 3746/2020 anexa 1 art. 9.
- Registrul conține doar identitatea, IBAN-ul și datele de deschidere și închidere, fără solduri. Soldurile și rulajele se obțin numai punctual (art. 61 alin. 1), pe titular și perioadă.
- În VSFP, organul fiscal alege metoda indirectă „în limitele rezonabilității și echității, asigurând o proporție justă” (art. 138 alin. 11).
- Pentru autoritățile penale: acces „de la caz la caz”, jurnalizare și ștergerea jurnalului după 5 ani (OG 9/2021 art. 5–6).
- Retenție: 10 ani la bancă (art. 61 alin. 5) și 10 ani în registru (art. 61^1 alin. 7). ONPCSB păstrează și distruge după 10 ani informațiile nevalorificate (Legea 129/2019 art. 34 alin. 5).

### „Acces aproape în timp real”
- ANAF află **zilnic** (D+1 în versiunea din 2015) existența conturilor, nu și mișcările din ele.
- Mișcările ajung la ANAF pe trei căi:
  - (i) cereri punctuale (5/10/25 zile lucrătoare);
  - (ii) rapoartele ONPCSB, lunar, pentru numerar și transferuri externe de cel puțin 10.000 euro;
  - (iii) CESOP, trimestrial, doar pentru plăți transfrontaliere repetitive.
- Nu am găsit în CPF sau Codul fiscal, în variantele la zi, o obligație a băncilor de a raporta periodic soldurile sau rulajele conturilor interne ale rezidenților. Strategia PIT 2023–2025 doar propunea astfel de raportări pentru carduri (măsura 4.1.b).
- Afirmațiile publice ale președintelui ANAF (Economedia, 08.10.2026: „vă verificăm banii încasați, conturile bancare”) nu indică un temei nou. Le pot explica căile (i)–(iii) plus DAC7 și datele de la curieri (art. 59 alin. 2^1 CPF). Este o inferență, neverificată.
- Titlurile din presa din 2020–2021 de tipul „ANAF va ști câți bani au românii în conturi” sunt inexacte: registrul nu conține sume (art. 61^1 alin. 3; Europa Liberă, 30.08.2021).

### Termene
| Flux | Termen | Temei |
|---|---|---|
| Raportare conturi deschise/închise și casete | zilnic | art. 61 alin. 2 CPF; OPANAF 3746/2020 anexa 2 art. 6–7 |
| Rulaje/solduri, perioadă de până la 3 ani | 5 zile lucrătoare | OPANAF 3746/2020 anexa 2 art. 4 lit. a |
| Rulaje/solduri, perioadă de peste 3 ani | 10 zile lucrătoare | idem lit. b |
| Documente privind operațiunile | 25 de zile lucrătoare | idem lit. c |
| Cerere generală art. 58/64 | termenul din cerere | OPANAF 3076/2017 anexa 3.a |
| Raport numerar/transfer extern către ONPCSB | max. 3 zile lucrătoare | Legea 129/2019 art. 7 alin. 7 |
| Răspuns la cererea ONPCSB | max. 15 zile | Legea 129/2019 art. 33 alin. 2 |
| ONPCSB → ANAF | lunar | art. 61 alin. 3 CPF |
| CESOP (F396) | sfârșitul lunii de după trimestru | CF art. 321^2 alin. 7; OPANAF 825/2024 |
| Informații CRS/FATCA (instituții raportoare) | 15 mai, anual; documente justificative în 45 de zile de la cerere | art. 62 alin. 7 și 12 CPF |
| Trecerea la noul XML (OPANAF 393/2026) | 10 zile lucrătoare de la 01.04.2026 | art. II |

### Răspundere și amenzi
- **CPF art. 336 alin. (1) lit. i)**: „nerespectarea de către instituțiile de credit … a oricărei obligații privind furnizarea informațiilor prevăzute la art. 61 sau transmiterea de informații incorecte sau incomplete”. **Lit. j)** se aplică „orice[i] entitate” (deci și instituțiilor de plată sau EMI). Textul a fost reformulat prin OUG 71/2025.
  - Sancțiune, **alin. (2) lit. d)**: **1.000–5.000 lei** pentru persoanele juridice contribuabili mijlocii și mari, **500–1.000 lei** pentru celelalte. *Amenda este pe faptă; textul nu precizează „pe raport” sau „pe cont”.*
- Pentru cererile întemeiate pe art. 58 (furnizarea de informații de către terți):
  - **art. 336 alin. (1) lit. r)** se sancționează conform **alin. (2) lit. e)**: 4.000–10.000 lei (mijlocii și mari), respectiv 2.000–5.000 lei;
  - pentru art. 64 (înscrisuri), **lit. c)** se sancționează conform **lit. a)**: 25.000–27.000 lei, respectiv 6.000–8.000 lei.
- **Art. 62^1** (refuzul de a pune la dispoziție, la cerere, informații din dosarele AML: proceduri KYC, beneficiar real, evidențe ale tranzacțiilor): lit. ț^5) se sancționează conform **lit. k^5)** cu **20.000–100.000 lei** pentru persoanele juridice (introdus prin OUG 71/2025).
- Poprire, art. 336 alin. (1) lit. k): amenda de la alin. (2) lit. d). Detalii în clusterul de popriri.
- Prescripția aplicării amenzii este de **5 ani** (art. 339 alin. 1).
- **Legea 129/2019 art. 43**: nerespectarea art. 7 (rapoartele de numerar și transfer extern) și a art. 33 alin. (2)–(4) (lit. c) se sancționează cu avertisment sau amendă de **10.000–90.000 lei** (alin. 2). Pentru persoanele juridice, maximul crește cu 10% din veniturile totale (alin. 3). Sancțiunile se pot aplica și membrilor conducerii.
- Personalul ANAF care divulgă datele răspunde penal. OPANAF 3770/2015, secțiunea IV, trimitea la Codul penal (divulgarea informațiilor secrete de serviciu); textul ordinului abrogat nu a fost reluat în forma actuală. Pentru angajații băncii se aplică obligația de confidențialitate din OUG 99/2006 art. 112.

### Statistici publice
- **ONPCSB 2025** (citit prin OCR; cifrele din paginile 9–10 verificate vizual) [P]:
  - **29.082** rapoarte de tranzacții suspecte (+30% față de 2024);
  - **27.644** rapoarte de numerar, cu **13.974.005** operațiuni, din care 13.800.725 raportate de bănci; în 2024 au fost 29.656 de rapoarte;
  - **8.745** rapoarte de transferuri externe, cu **11.482.674** operațiuni, din care 11.424.389 raportate de bănci; în 2024 au fost 8.429;
  - către ANAF: **221 + 21** informări în 2025.
- **VSFP iulie 2025 – mai 2026** [S, economica.net, 15.06.2026, care reia comunicatul ANAF]:
  - **1.102** acțiuni de control;
  - creanțe suplimentare de **540.243.299 lei**;
  - măsuri asigurătorii de **123.514.438 lei**;
  - constatări tipice: „depuneri de numerar în conturile bancare personale și/sau ale societăților”.

### Evoluție 2015–2026 (pe scurt)
- **01.01.2016**: intră în vigoare art. 61 (raportare zilnică și cereri punctuale). Băncile raportează simplificat până la 30.06.2016 (OPANAF 1106/2016).
- **07.2019**: Legea 129/2019 mută fluxul de numerar și transferuri externe pe calea ONPCSB, care transmite lunar către ANAF.
- **15.07.2020**: OUG 111/2020 transpune AMLD5. Obligația se extinde la instituțiile de plată și EMI, iar raportarea include IBAN și beneficiar real. Se creează registrul (art. 61^1).
- **02.11.2020**: OPANAF 3746/2020. Registrul este operațional din ianuarie 2021, conform presei.
- **31.08.2021**: OG 9/2021 dă acces autorităților penale.
- **2023**: Legea 296/2023 extinde accesul autorităților publice și ridică impozitul pe veniturile nejustificate la 70%.
- **2024**: CESOP (Legea 33/2024, OPANAF 825/2024).
- **12.2025**: OUG 71/2025 reformulează sancțiunile.
- **01.04.2026**: noul XML (OPANAF 393/2026).
- **06.07.2026**: DGAF devine competentă și pentru VSFP (OPANAF 768/2026).

### Denumirea „FICOBA”
- FICOBA este fișierul francez (CNIL). În România denumirea corectă este „Registrul central electronic pentru conturi de plăți și conturi bancare identificate prin IBAN” (art. 61^1 CPF; OPANAF 3746/2020). Pentru produs/demo recomand termenul oficial sau „registrul conturilor (IBAN)”.

---

## Incertitudini
1. **CPF consolidat oficial**: pagina legislatie.just.ro a CPF (170005 și consolidarea 309358) nu s-a încărcat. Textul citat provine din consolidarea ANAF („nu reprezintă o republicare”), verificată pe actele modificatoare pentru art. 61, 61^1 și 336. Conținutul **Legii nr. 170/2026** (ultima actualizare ANAF) nu a fost verificat. Nu știu dacă atinge aceste articole.
2. **Actul care a introdus pragul VSFP** (10% și 50.000 lei) și notificarea de conformare (art. 140^1) nu a fost identificat. Am verificat doar textul în vigoare. Planul strategic ANAF pomenește o „nouă lege” de revizuire a competențelor, fără să o numească.
3. **Canalul exact** pentru cererile din art. 61 alin. (1): OPANAF 3746/2020 spune „prin portal” și „platforma web dedicată”, fără să numească portalul. Sub OPANAF 3770/2015 era spațiul privat de pe portalul ANAF și conexiunea STS (extranet). Este probabil același mecanism, dar **neverificat**. Anexa 3 a OPANAF 3746/2020 (înrolarea) nu a fost citită integral.
4. **Interdicția de a informa clientul** („tipping-off”) pentru cererile ANAF din art. 61: nu am găsit o prevedere expresă în CPF sau în OPANAF 3746/2020. Pentru fluxul AML interdicția există: Legea 129/2019 art. 38 alin. (2) obligă banca „de a nu divulga clienților vizați sau terților faptul că informațiile sunt în curs de transmitere … în conformitate cu art. 6”. Ea privește însă rapoartele de tranzacții suspecte, nu cererile ANAF.
5. **OUG 74/2013 consolidată**: am citit doar forma din 2013. Art. 6 alin. (2) poate fi modificat.
6. **Art. 153 din Codul de procedură penală**: neverificat textual.
7. **Lista protocoalelor** din art. 61^1 alin. (6) (care trebuie publicată pe site-ul ANAF) nu a fost găsită. Nici un protocol de date ANAF–ARB sau ANAF–BNR nu a fost găsit în surse primare. Pentru fluxul invers (ANAF → bancă, cu acord) există modelul-cadru OPANAF 146/2022 și un proiect din septembrie 2026 ([05-digital.md](05-digital.md)).
8. **Statisticile ONPCSB** provin din OCR pe un PDF scanat. Doar cifrele de la p. 9–10 (221 și 21 informări) au fost verificate vizual. Restul pot conține erori de OCR. Sumele în euro nu au fost preluate.
9. **Presa** (EVZ, economica.net, Economedia, Europa Liberă) este sursă secundară. Pentru titlurile din Google News (profit.ro, Republica.ro, Agerpres) textul nu a fost citit: decodarea linkurilor a fost blocată (HTTP 429).
10. **Directivele UE** nu au fost citite pe EUR-Lex (acces blocat). Transpunerea este confirmată de preambulul OUG 111/2020 și de definițiile din OG 9/2021.
11. Ordinul nr. 6.508/2024 (formularul 709): emitentul nu a fost verificat.
12. Căutarea web a fost limitată de blocaje. Pot exista proiecte de acte normative din 2026 (de exemplu, raportarea periodică a soldurilor) nedetectate.
