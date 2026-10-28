# Executarea silită asupra conturilor bancare (popriri / e-Popriri) și garanțiile bancare în favoarea ANAF

Cercetare pentru echipa Raiffeisen, provocarea „Operational Efficiency”. Data: 2026-10-28.

**Cum am lucrat (important pentru cât de sigure sunt informațiile):**
- Motoarele de căutare (DuckDuckGo, Bing, Google, Brave, Mojeek, SearXNG) au blocat interogările automate. Am înlocuit căutarea cu (a) indexul Wayback Machine (CDX), interogat după tipare de URL pe `static.anaf.ro` și pe site-urile băncilor, și (b) sitemap-urile băncilor.
- legislatie.just.ro nu a putut fi accesat (timeout / ECONNREFUSED). Textul legii îl citez din **versiunea consolidată publicată de ANAF**: https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm (antet: „ultima actualizare : Legea nr. 170/2026”, „Acest text este actualizat prin mijloace informatice și nu reprezintă o republicare”). Mai jos o numesc **CPF-ANAF**.
- Ordinele le-am descărcat ca PDF de pe `static.anaf.ro/static/10/Anaf/legislatie/` și le-am convertit cu `pdftotext`.
- Notație: în CPF-ANAF exponenții apar lipiți de numere, de exemplu „(21)” înseamnă alin. (2^1). Mai jos scriu „^” (de exemplu art. 233 alin. (2^1)).
- Sursele „secundare” sunt marcate ca atare (site-uri de bănci, legeaz.net).

---

## Documente

### A. Legislație primară

| # | Titlu exact | Nr./dată | Emitent | URL | Status | Ce reglementează |
|---|---|---|---|---|---|---|
| A1 | Legea nr. 207/2015 privind Codul de procedură fiscală (CPF) | 207/2015 | Parlamentul | Consolidarea ANAF: https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm ; arhiva versiunilor: https://static.anaf.ro/static/10/Anaf/cod_procedura/CP_arhiva.htm | În vigoare; consolidarea ANAF ajunge până la Legea nr. 170/2026 | Art. 25 (răspunderea solidară a terțului poprit și a emitentului garanției), art. 61 (bănci → ANAF: conturi), art. 168 (restituiri din popriri), art. 193/202/207 (garanții la eșalonare), art. 210–212 (garanții), art. 213–214 (măsuri asigurătorii), art. 220 (organe de executare), art. 229–236 (somație, suspendare, încetare, poprire), art. 258 (ordinea de distribuire), art. 336 (contravenții). |
| A2 | Aceeași lege, versiunea consolidată ANAF din 08.02.2019 | – | ANAF (consolidare) | https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_08022019.htm | Istoric | Comparația arată că alin. (20)–(21) ale art. 236 (baza legală e-Popriri) **nu existau** la 08.02.2019. Atunci art. 236 alin. (13) spunea „în termen de 3 zile”, nu „3 zile lucrătoare”. |
| A3 | Aceeași lege, consolidarea ANAF „ultima actualizare: Ordonanța Guvernului nr. 11/2021” (copie Wayback) | – | ANAF (consolidare) | https://web.archive.org/web/2021id_/https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_11022020.htm | Istoric | Art. 236 alin. (20)–(21) existau deja în 2021. Comparația cu CPF-ANAF 2026 arată că art. 25, 168, 210–214, 233–236 **nu au suferit modificări de fond între 2021 și Legea 170/2026**. S-au schimbat doar termenii („băncile” → „instituțiile de credit” la art. 236 alin. (3), (13)–(15)). |
| A4 | Legea nr. 134/2010 privind Codul de procedură civilă (republicat) – limitele urmăririi veniturilor și obiectul popririi | art. 729 și 781 în numerotarea republicată | Parlamentul | **Secundar**, cu numerotarea dinainte de republicare: https://legeaz.net/noul-cod-de-procedura-civila/art-728 (= art. 729 rep.) și https://legeaz.net/noul-cod-de-procedura-civila/art-780 (= art. 781 rep.) | În vigoare; forma consolidată actuală **neverificată** pe o sursă primară | Salariul și pensia pot fi urmărite până la 1/2 (întreținere) sau 1/3 (alte datorii). Alocațiile și indemnizațiile cu destinație specială nu pot fi urmărite (art. 729 alin. (7) rep., la care trimite art. 168 alin. (12) CPF). Nu pot fi poprite nici creditele nerambursabile, nici salariile viitoare pe 3 luni (art. 781 alin. (5) rep.). |

### B. Mecanismul e-Popriri (comunicarea electronică a actelor de executare către bănci)

| # | Titlu exact | Nr./dată | Emitent | URL | Status | Ce reglementează |
|---|---|---|---|---|---|---|
| B1 | Ordin nr. 691/2017 privind stabilirea mijloacelor electronice de transmitere la distanță a actelor de executare și a procedurii de comunicare a acestora | 691/12.05.2017, MO nr. 383/23.05.2017 | MFP | https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_691_2017.pdf | Abrogat de OMFP 1665/2020 (art. 14) | Prima versiune: portal în spațiul privat, fișier „PDF cu xml atașat, semnat electronic” (art. 2), protocol ANAF–ARB–bănci (art. 3), comunicarea în „prima zi bancară următoare” (art. 4). |
| B2 | Ordin nr. 1665/2020 privind stabilirea mijloacelor electronice de transmitere la distanță a actelor de executare și a procedurii de comunicare a acestora | 1665/26.02.2020, MO nr. 200/12.03.2020 | MFP | https://static.anaf.ro/static/10/Anaf/legislatie/OMFP_1665_2020.pdf | Abrogat la 11.05.2022 de OMF 1112/2022 | A introdus „ordonanțarea de plată” și termenele 17:00 / 24:00 / 11:00 / 20:00. Pct. 8.4: băncile fără convenție-cadru nu aplicau cap. III (ordonanțarea). |
| B3 | Ordin nr. 128/2022 pentru modificarea și completarea OMFP nr. 1.665/2020 | 128/03.02.2022 | MF | https://static.anaf.ro/static/10/Anaf/legislatie/OMF_128_2022.pdf | Istoric (actul de bază e abrogat) | Contul 50.67.02 de la Trezoreria Operativă Centrală, validarea automată, neacceptarea viramentelor când suma rămasă este „zero”, pct. 8.4 și 8.8. |
| B4 | Ordin nr. 1112/2022 pentru abrogarea OMFP nr. 1.665/2020 | 1112/05.05.2022 | MF | https://static.anaf.ro/static/10/Anaf/legislatie/OMF_1112_2022.pdf | În vigoare (act de abrogare) | Abrogă OMFP 1665/2020. Nota din PDF-ul OPANAF 878: „abrogat la data de 11 mai 2022, prin art. 1 din Ordinul ministrului finanțelor nr. 1112/2022”. |
| **B5** | **Ordin nr. 878/2022 privind stabilirea mijloacelor electronice de transmitere la distanță a actelor de executare și a procedurii de comunicare a acestora** | 878/03.05.2022, MO nr. 466/10.05.2022 (MO-ul este citat în OPANAF 626/2023) | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_878_2022.pdf (forma inițială, PDF din 23.05.2022) | **În vigoare din 11.05.2022** (art. 13 alin. (2) + nota #CIN). Modificat de OPANAF 626/2023. | Procedura e-Popriri (Anexa 1), convenția-cadru (Anexa 2, între timp abrogată), modelele: adresa de actualizare a sumelor (Anexa 3), adresa băncii cu motivele de neînființare (Anexa 4), adresa băncii cu sumele ce pot fi plătite (Anexa 5), ordonanțarea de plată (Anexa 6), referatul de nevirare (Anexa 7). |
| **B6** | **Ordin nr. 626/2023 pentru modificarea și completarea OPANAF nr. 878/2022** | 626/05.05.2023; MO **neverificat** | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_626_2023.pdf | În vigoare. Art. I pct. 1, 2 și 5–7 se aplică de la **1 octombrie 2023** (art. II). | Reconcilierea cu băncile după un calendar (art. 4 alin. (2)). Introduce pct. 4.2^1: în timpul suspendării, Trezoreria nu validează viramentul. Modifică pct. 5.3 (cum se folosesc sumele indisponibilizate în timpul suspendării). Abrogă pct. 8.4 și Anexa 2 (convenția-cadru). Efect: **ordonanțarea devine obligatorie pentru toate băncile.** |
| B7 | Ordin nr. 2153/2019 privind aprobarea înființării organului de executare silită pentru îndeplinirea unor acte de executare silită prin procese electronice masive | 2153/02.08.2019, MO nr. 673/13.08.2019 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2153_2019.pdf | În vigoare (nu am găsit abrogarea) | Art. 1 alin. (2): „Direcția generală de reglementare a colectării creanțelor bugetare are calitatea de organ de executare silită” pentru procesele electronice masive. Aceasta este, practic, structura numită „Punct central” în OPANAF 878, pct. 1.2 lit. b) (Anexele 4 și 5 sunt adresate DGRCCB). |
| B8 | Ordin nr. 3770/2015 pentru aprobarea Procedurii de furnizare de informații conform art. 61 din CPF; modificat de Ordinul nr. 3592/2016 | 3770/23.12.2015, MO 983/30.12.2015; 3592/14.12.2016, MO 1017/19.12.2016 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3770_2015.pdf ; https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_3592_2016.pdf | **Abrogat** prin OPANAF 3746/2020, art. 5 (verificat pe legislatie.just.ro, id 231985). Procedura actuală: OPANAF 3746/2020, modificat prin 393/2026; vezi [02-informatii.md](02-informatii.md). OPANAF 878 art. 2 alin. (2) trimite în continuare la ordinul abrogat. | Istoric: băncile trimiteau zilnic conturile deschise/închise, ca declarație „PDF cu fișier xml atașat”, cu certificat digital calificat. Termene la cererile de rulaje: 5/10 zile lucrătoare. OPANAF 878 art. 2 alin. (2) trimite la acest ordin pentru identificarea băncilor și accesul la sistem. |

### C. Ordine conexe privind colectarea, poprirea și garanțiile

| # | Titlu exact | Nr./dată | Emitent | URL | Status | Ce reglementează |
|---|---|---|---|---|---|---|
| C1 | Ordin nr. 727/2019 pentru aprobarea Procedurii privind emiterea și comunicarea unor acte administrative și de executare pentru debitorii care înregistrează obligații fiscale restante sub o anumită limită… | 727/13.03.2019, MO 220/21.03.2019 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_727_2019.pdf | Cap. III înlocuit de OPANAF 2098/2019 | Praguri sub care somațiile se emit doar o dată la 180 de zile. |
| C2 | Ordin nr. 2098/2019 pentru modificarea Procedurii aprobate prin OPANAF nr. 727/2019 | 2098/30.07.2019 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2098_2019.pdf | În vigoare (modificări ulterioare neverificate) | **Când se emite adresa de poprire bancară**, pe categorii de debitori (pct. 6.1–6.4). Exemple: mare contribuabil peste 40.000 lei nestinși la expirarea celor 15 zile de la somație; persoană fizică peste 2.000 lei la 15 zile, peste 1.000 lei la 90 de zile, peste 100 lei la 180 de zile. |
| C3 | Ordin nr. 63/2017 privind aprobarea modelelor unor formulare utilizate în domeniul colectării creanțelor fiscale… | 63/09.01.2017, MO 32/11.01.2017 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_63_2017.pdf | Modificat, printre altele, de OPANAF 362/2022 și 6.942/2024 | Formulare: adresa de poprire asigurătorie pe conturi (anexa 10), adresa de poprire executorie pe conturi (anexa 20), adresa de înființare a popririi pe conturi (anexa 24), decizia de ridicare a popririi asigurătorii (anexa 14) etc. (art. 1). |
| C4 | Ordin nr. 362/2022 pentru modificarea și completarea unor ordine ale președintelui ANAF privind aprobarea modelelor unor formulare utilizate în domeniul colectării creanțelor fiscale | 362/08.03.2022 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_362_2022.pdf | În vigoare | Pentru tipărirea prin UIR (OPANAF 967/2017) adaugă: „Adresă de suspendare, temporară, totală sau parțială a indisponibilizării conturilor” (anexa 16) și „Adresă privind continuarea măsurilor de executare silită prin poprire asupra disponibilităților bănești” (anexa 18). |
| C5 | Ordin nr. 967/2017 pentru aprobarea modelelor unor formulare … emise și tipărite prin intermediul Unității de imprimare rapidă | 967/20.03.2017, MO 208/27.03.2017 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_967_2017.pdf | Modificat de 362/2022 și 1.961/2023 | Anexa 6 este modelul „Adresă de înființare a popririi asupra disponibilităților bănești” (cu temei art. 236 alin. (12)–(13), „în limita sumei de … lei”, „utilizând numărul de evidență a plății”). Anexa 8 este decizia de ridicare. |
| C6 | Ordin nr. 2546/2016 pentru aprobarea Procedurii de aplicare a măsurilor asigurătorii … | 2546/05.09.2016, MO 703/09.09.2016 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2546_2016.pdf | În vigoare (modificări neverificate) | Măsurile asigurătorii posibile includ „poprirea asigurătorie asupra disponibilităților bănești din conturile bancare” (pct. 19 lit. d)). Ridicarea se face la constituirea garanției de la art. 211 (pct. 49, 52–53, 81); după decizia de ridicare, executarea se face „în termen de cel mult două zile” (pct. 83). |
| C7 | Modele de formulare din 16.12.2016: „Adresă de înființare a popririi asigurătorii asupra disponibilităților bănești” (Anexa 10) și „… asupra sumelor datorate debitorului de către terți” (Anexa 11) | 16.12.2016 | ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/10_Adresa_poprire_16122016.pdf ; https://static.anaf.ro/static/10/Anaf/legislatie/11_Adresa_poprire_tert_16122016.pdf | Ordinul de aprobare **neidentificat** (probabil o variantă de anexă la OPANAF 63/2017) | Textul trimis băncii: „se indisponibilizează de îndată, în limita sumei estimate/stabilite, sumele existente, precum și cele viitoare…”. |
| C8 | Ordin nr. 1757/2019 pentru aprobarea Procedurii de mediere… | 1757/28.06.2019, MO 549/04.07.2019 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1757_2019.pdf | În vigoare | Medierea de la art. 230^1 CPF. Dacă debitorul notifică intenția de mediere, executarea silită nu începe. |
| C9 | Ordin nr. 6.138/2024 privind stabilirea cazurilor speciale de executare silită, precum și a structurilor abilitate … | 6138/14.08.2024 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_6138_2024.pdf | În vigoare; **abrogă OPANAF 3744/2015** | Competența pentru cazurile speciale de executare silită (art. 220 alin. (2^1) lit. a) CPF). Conținutul nu l-am analizat în detaliu. |
| C10 | Ordin nr. 1090/2022 privind aprobarea Procedurii de comunicare prin mijloace electronice de transmitere la distanță între organul fiscal central și persoanele fizice, persoanele juridice… | 1090/09.06.2022 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1090_2022.pdf | În vigoare (modificări neverificate) | Prin SPV debitorul primește „Acte de executare, cum ar fi: somație, titlu executoriu, adresă de înființare a popririi …, decizie de ridicare…”. Documentele se păstrează în SPV „60 de zile” (Anexa 1 pct. 3; Anexa 2 pct. 4). |
| C11 | Ordin nr. 90/2016 pentru aprobarea conținutului cererii de acordare a eșalonării la plată … precum și a Procedurii de aplicare a acordării eșalonării la plată de către organul fiscal central | 90/14.01.2016, MO 45/20.01.2016 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_90_2016.pdf | Modificat de OPANAF 1960/2019 (https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_1960_2019.pdf). Vigența actuală **neverificată**. | Procedura din Anexa 2, art. 6 alin. (7): **ce trebuie să conțină o scrisoare de garanție sau o poliță de asigurare de garanție** (vezi W12). |
| C12 | Ordin nr. 159/2024 privind abrogarea OPANAF nr. 1.889/2016 (Normele tehnice privind titlurile de garanție izolată, garanția globală și exonerarea de garanție în tranzitul unional/comun) | 159/30.01.2024 | Președintele ANAF | https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_159_2024.pdf | În vigoare (act de abrogare) | Garanții vamale: abrogă normele naționale din 2016. Ce le-a înlocuit este **neverificat**. |

### D. Comunicate și rapoarte ANAF (primare), inclusiv protocoale cu băncile

| # | Titlu | Nr./dată | URL | Conținut util |
|---|---|---|---|---|
| D1 | „Poprirea electronică sprijină contribuabilii în dificultate” (PDF scanat, citit prin OCR) | A_RPC 674/10.07.2019 | https://static.anaf.ro/static/3/Anaf/20190710172941_poprireaelectronica.pdf | Anunță mecanismul: indisponibilizarea „în limita sumelor înscrise în actele de executare silită”. Ridicarea se va face „de la nivelul central al A.N.A.F., pe baza schimbului rapid de informații cu instituțiile de credit”, fără deplasarea debitorului la ghișeu. |
| D2 | „Precizări ANAF” despre popririle „fără înștiințare” | A-RPC 978_1/10.11.2023 | https://static.anaf.ro/static/3/Anaf/20231110125654_compopriri.pdf | „începând cu anul 2021, a fost pusă în funcțiune aplicația informatică e-Popriri”. Aplicația sortează sumele în ordine descrescătoare și cere virarea „numai acelor bănci care dețin disponibilități bănești ce acoperă suma poprită”. Înștiințarea ajunge prin SPV dacă debitorul e înrolat, altfel prin poștă (de aici decalajul de timp). |
| D3 | Raport de performanță ANAF 2023 | 2023 | https://static.anaf.ro/static/10/Anaf/Informatii_R/Raport_de_performanta_2023.pdf | „În cursul anului 2023, prin intermediul acestei noi aplicații informatice e-Popriri, au fost comunicate un număr de 2.168.482 acte de executare silită”. Popriri la terți: 562.600 adrese, 9,98 mld. lei. |
| D4 | Raport de performanță ANAF 2024 | 2024 | https://static.anaf.ro/static/10/Anaf/Informatii_R/Raportperformanta2024.pdf | e-Popriri 2024: **2.528.710 acte**, adică 1.260.034 adrese de înființare (22,17 mld. lei), 75.945 adrese de suspendare, 25.282 adrese de continuare, 1.167.449 decizii de ridicare. Popriri la terți: 346.178 (8,77 mld. lei). Încasări din executare silită: 21,53 mld. lei (+36,44%). Somații: 2.986.765. Mai menționează UIR Râmnicu-Vâlcea și OPANAF 6.138/2024. |
| D5 | Raport de activitate ANAF, semestrul I 2024 | S1 2024 | https://static.anaf.ro/static/10/Anaf/Informatii_R/20.09.2024%20-%20Raport%20activitate%20ANAF%20semestrul%20I%202024%20-%20Var_OI.pdf | 1.710.993 acte prin e-Popriri: 1.179.374 înființări (12,23 mld. lei), 24.309 suspendări, 13.210 continuări, 494.100 ridicări. |
| D6 | Raport de activitate ANAF, semestrul I 2025 | S1 2025 | https://static.anaf.ro/static/10/Anaf/Informatii_R/18.08.2025%20-%20Raport%20de%20activitateANAF_2025S1_V7.pdf | 2.250.489 acte prin e-Popriri: 1.079.509 înființări, 26.631 suspendări, 18.997 continuări, 1.125.352 decizii de ridicare (raportul notează: comunicate „până la finalul anului 2024 cât și în cursul anului 2025”). Încasări din executare silită 12,33 mld. lei, din care **2,92 mld. lei din popriri pe conturi bancare** (+22,89%). Popriri asigurătorii: 422,01 mil. lei indisponibilizați pe conturi. |
| D7 | Prezentare DGRFP Ploiești / AJFP Dâmbovița, „Stingerea creanțelor fiscale prin executare silită” | 08.08.2024 | https://static.anaf.ro/static/10/Ploiesti/Stingerea_Creantelor_Fiscale_prin_Executare_Silita.pdf | Pe 2023: încasări din executare silită 15.780 mil. lei (4% din totalul încasat), din care popriri bancare 3.909 mil. lei (25%), popriri la terți 358 mil. lei (2%). Mai conține „PROIECT”-e de modificare a CPF (art. 236 alin. (12), plafoane de numerar la Legea 70/2015), vezi Incertitudini. |
| D8 | Comunicat: Protocolul ANAF–ARB din 2019 (credit, acordul clientului pentru interogarea datelor fiscale) | A PRS 2376/2019; ARB 123/2019 | https://static.anaf.ro/static/10/Anaf/Informatii_R/ANAF_As_Rom_Banci_110322.pdf | Context pentru cooperarea ANAF–ARB; **nu** este protocolul e-Popriri. |
| D9 | Comunicat: Protocolul ANAF–ARB–bănci pentru înregistrarea fiscală a nerezidenților | semnat 18.08.2022; ARB 343/2022 | https://static.anaf.ro/static/10/Anaf/Informatii_R/Comunicat_ARB_09022023.pdf | Arată canalul tehnic comun: „aplicația informatică pusă la dispoziție de către Centrul Național pentru Informații Financiare”. Context. |
| D10 | Comunicat: protocoalele de aderare la serviciul „Registrul central electronic pentru conturi de plăți și conturi bancare” din PatrimVen (PASS), iulie 2023 | 07/2023 | https://static.anaf.ro/static/10/Anaf/Informatii_R/Comunicat_Protocol.pdf | Autoritățile locale și instanțele aderă la registrul central de conturi. Așa află ele la ce bănci să trimită popriri. |

### E. Explicațiile publice ale băncilor (surse SECUNDARE, dar arată fluxul operațional)

| # | Bancă | URL | Conținut util |
|---|---|---|---|
| E1 | ING (PF) | https://ing.ro/persoane-fizice/info/popriri | „Începând cu 01.10.2023, devine aplicabil noul flux electronic … conform Ordinului Președintelui ANAF nr. 878/2022”. Soldul popribil se trimite „până la maxim ora 17:00”. După raportare, soldul „nu mai poate fi, pentru niciun motiv, diminuat/pus la dispozitia clientului”. Cererile de deblocare se depun „pana cel tarziu la ora 13:00”. |
| E2 | ING (IMM) | https://ing.ro/imm/info/popriri | Salariile pot fi plătite „pe termen nelimitat” la popririle fiscale și doar 3 luni la cele ale executorilor judecătorești. Fondurile nerambursabile se deblochează pe bază de documente, până la 13:00 la ANAF și 17:00 la celelalte popriri. Ridicarea se face doar pe adresă de sistare. |
| E3 | Libra Internet Bank (FAQ PF și PJ) | https://www.librabank.ro/Intrebari_frecvente/Persoane_fizice_popriri | Din octombrie 2023 băncile transmit zilnic soldurile, iar „ANAF va decide din ce conturi să fie plătite datoriile”. Înainte, „se întâmpla frecvent ca două bănci să vireze aceeași sumă”. Termenul pentru client (sume neurmăribile) este „ora 16:00 a zilei înființării popririi”. Clientul „nu poate decide” banca plătitoare. La popririle ANAF, banca „nu mai poate ține cont de dovezile de plată”. Ordinea plăților între creditori, suspendare vs. sistare. |
| E4 | Libra (blog juridic) | https://www.librabank.ro/blog/Episodul_1._Popririle,_explicate_de_un_avocat_%C8%99i_de_ce_popririle_clien%C8%9Bilor_Revolut_nu_se_adreseaz%C4%83_c%C4%83tre_Libra/2222 | Banca primește popriri lunar de la executori judecătorești, ANAF și primării. Cazul popririlor greșit adresate (clienți Revolut cu IBAN-uri virtuale emise prin Libra). |
| E5 | BCR (pagină și Help Center, 15 URL-uri) | https://www.bcr.ro/ro/persoane-fizice/informatii-utile/popriri ; formulare: …/formular-de-contact-popriri și …/formular-dovada-plata-popriri | Clientul primește SMS „în ziua instituirii popririi”. Formularul de deblocare: „aștepți 45 de minute … timp 120 de minute poți retrage sumele”, cereri procesate în aceeași zi până la 16:30. „În cazul popririlor de la ANAF, emise în format electronic prin portalul e-Popriri, adresele de ridicare sunt transmise … exclusiv în format electronic”. Prioritatea: întâi executorii fiscali, apoi cei judecătorești, în ordine cronologică. |
| E6 | BRD | https://www.brd.ro/contacteaza-ne/popriri | Formular online „Cerere de deblocare poprire”, cu documente atașate de maximum 20 MB. |
| E7 | CEC Bank | https://www.cec.ro/contact-popriri | Formular pentru „sesizări privind instituirea de popriri” și „solicitări pentru suspendarea sau sistarea popririlor”. |
| E8 | Raiffeisen Bank | https://www.raiffeisen.ro/ro/persoane-fizice/in-sprijinul-tau/informatii-utile/informare-privind-platile-efectuate-in-executarea-unei-instructiuni-de-poprire.html | Informarea nr. 250212: din 20.10.2023 banca percepe „un comision de 25 lei” pentru fiecare plată făcută în executarea unei popriri. În sitemap-ul raiffeisen.ro **nu am găsit** o pagină publică de FAQ despre popriri. |
| – | Banca Transilvania, UniCredit | – | BT: 403 la orice acces automat. UniCredit: nicio pagină de popriri în sitemap. **Neverificate.** |

---

## Workflow-uri

Abrevieri folosite mai jos:
- **OFC**: organul fiscal central competent, cel care administrează creanța (A.J.F.P., DGAMC etc., art. 30, 33, 35 CPF).
- **PC**: Punctul central, adică DGRCCB conform OPANAF 2153/2019.
- **UIR**: Unitatea de Imprimare Rapidă.
- **TOC**: Trezoreria Operativă Centrală.
- **IC**: instituția de credit (banca).
- **878**: Anexa 1 la OPANAF 878/2022 (forma modificată de 626/2023).

Pentru detaliile fiecărui pas, vezi documentele din tabelele A–E.

### W0. Premisele: somația și criteriile pentru emiterea popririi
1. OFC → debitor: comunică **somația** împreună cu titlul executoriu. Canalul este SPV pentru cei înrolați, altfel poșta prin UIR (D2; D4: „funcții automate de comunicare masivă prin SPV … și prin poștă, prin intermediul Unității de Imprimare Rapidă de la Râmnicu-Vâlcea”). Termen: **15 zile** pentru plată sau pentru notificarea intenției de mediere. Temei: art. 230 alin. (1) CPF, https://static.anaf.ro/static/10/Anaf/cod_procedura/Cod_Procedura_Fiscala_2023.htm
2. Debitorul poate notifica medierea în 15 zile. Din acel moment „Procedura de executare silită nu începe” (art. 230^1 alin. (1) și (6) CPF; OPANAF 1757/2019).
3. OFC verifică restanțele rămase nestinse și emite adresa de poprire bancară dacă sunt depășite pragurile: mare contribuabil peste 40.000 lei la expirarea celor 15 zile; mijlociu peste 40.000 lei; alte persoane juridice peste 10.000 lei; persoane fizice peste 2.000 lei. Pragurile scad la 90 și la 180 de zile de la somație (OPANAF 2098/2019, pct. 6.1–6.4, https://static.anaf.ro/static/10/Anaf/legislatie/OPANAF_2098_2019.pdf). Sub plafonul de anulare nu se emit acte (pct. 5; art. 266 alin. (5) CPF: restanțele sub 40 lei se anulează).
4. Conținutul actelor: actele de poprire bancară indică „doar cuantumul total al obligațiilor fiscale ce fac obiectul popririi” (art. 229 alin. (2) CPF), cu excepția înștiințării trimise debitorului.

### W1. Înființarea popririi bancare electronice (e-Popriri)
1. **OFC → UIR**: în fiecare zi, OFC generează adresele de înființare a popririi asupra disponibilităților bănești din aplicațiile de gestiune a creanțelor, „pentru fiecare instituție de credit, unde debitorul are deschise conturi”, și le transmite la UIR (878 pct. 1.3, 2.1). OFC știe ce conturi are debitorul din raportarea zilnică a băncilor (art. 61 alin. (2) lit. a) CPF; OPANAF 3746/2020, care a înlocuit OPANAF 3770/2015).
2. **UIR**: procesează și validează adresele (pct. 1.4, 2.2).
3. **Sistemul e-Popriri (creat de CNIF, administrat de PC) → platforma e-Popriri**: preia adresele și le încarcă în spațiul privat al fiecărei IC **până la ora 24:00** (pct. 1.2 lit. a), l), 2.3). Formatul este fișier „.pdf cu .xml atașat, semnat electronic” (art. 2 alin. (1) 878). Semnătura este „electronică extinsă a Ministerului Finanțelor” sau o semnătură calificată (art. 9 878; art. 46 alin. (4) CPF).
4. **Momentul comunicării**: actul se consideră comunicat în „prima zi bancară următoare zilei încărcării”, la „ora de începere a zilei bancare” (art. 6 878; pct. 2.3). Ora o declară fiecare bancă (fosta convenție-cadru, art. 3.2; între timp abrogată de 626/2023, fără un înlocuitor public identificat).
5. **IC**: descarcă fișierul. Poprirea „se consideră înființată din momentul primirii adresei”, iar banca înregistrează ziua și ora (art. 236 alin. (8) CPF). Din acel moment indisponibilizează sumele existente și viitoare, în lei și în valută, „în limita sumei necesare” (art. 236 alin. (13)). Nu mai decontează plăți din cont, cu excepțiile de la art. 236 alin. (14): salarii (pe declarație pe propria răspundere), accize și obligațiile de care depinde o înlesnire.
6. **IC → e-Popriri, dacă nu poate înființa poprirea**: trimite un mesaj cu numărul de evidență a plății, CIF-ul debitorului (sau al sediului secundar) și motivul: 1 nu are cont, 2 insolvență, 3 cont închis, 4 decedat/radiat, 5 alt motiv convenit cu PC. Termenul este cel de la pct. 3.2, adică 17:00 în ziua bancară următoare (pct. 2.5; formularul din Anexa 4).
7. **Cont nou la altă bancă**: OFC emite o adresă nouă, la nivelul soldului din adresele deja comunicate altor bănci (pct. 2.6).
8. **Debitorul** este înștiințat separat prin „adresa de înștiințare privind înființarea popririi” (art. 236 alin. (5); SPV prin OPANAF 1090/2022 sau poștă, D2). Băncile trimit și ele SMS sau e-mail (BCR E5, ING E2, Libra E3).

### W2. Raportarea sumelor ce pot fi plătite („sold popribil”)
1. **IC → e-Popriri**: trimite un singur fișier .pdf cu .xml, semnat electronic, cu „sumele pe care le pot plăti”:
   - a) până la **ora 17:00 a zilei bancare următoare** celei în care s-a comunicat adresa;
   - b) până la 17:00 a zilei bancare următoare oricărei încasări în conturile debitorului (878 pct. 3.2; art. 236 alin. (20) CPF).
2. **Câmpuri minime**: numărul de evidență a plății, CIF-ul debitorului, suma care poate fi plătită și alte informații de identificare (pct. 3.3). Formularul din Anexa 5 are coloanele „ID poprire | Numărul de evidență a plății | Codul de identificare fiscală | Data (ZZ.LL.AAAA) | Suma care poate fi plătită – lei”.
3. **Efectul raportării**: din momentul transmiterii, banca „nu procedează la decontarea documentelor de plată primite … și nu acceptă alte plăți … până la realizarea plății efective”. Plățile excepționate (art. 236 alin. (14)) se mai pot face doar **până la transmiterea** sumei disponibile (878 pct. 3.4; art. 236 alin. (20) teza a 3-a). Varianta băncii, secundar: ING spune că după raportare soldul nu mai poate fi diminuat (E1), Libra cere anunțul clientului până la 16:00 (E3).
4. **Dacă nu există sume**: poprirea rămâne înființată și ciclul se reia la următoarea încasare (pct. 3.7).
5. **Conturi în valută**: banca are dreptul să convertească fără acordul clientului, la cursul afișat (art. 236 alin. (3)). Diferențele de curs dintre ziua raportării și ziua viramentului nu atrag răspunderea solidară, dar restul rămas în valută trebuie raportat în ziua viramentului (pct. 3.13).

### W3. Ordonanțarea: alegerea băncii care plătește
1. **e-Popriri**: după expirarea termenului de 17:00, sortează automat sumele confirmate de bănci **în ordine descrescătoare** (pct. 3.5).
2. **Reguli de alocare** (pct. 3.6):
   - dacă o bancă a confirmat întreaga sumă rămasă de recuperat, ordonanțarea merge doar la ea (3.6.1);
   - dacă suma tuturor confirmărilor nu acoperă restul, fiecare bancă primește ordonanțare pentru suma confirmată (3.6.2);
   - dacă suma confirmărilor depășește restul, se alocă în ordinea descrescătoare până la acoperire. Ultima bancă primește ordonanțare doar pentru diferență, „şi nu în limita sumei ce a fost confirmată” (3.6.3).
3. **e-Popriri → IC**: încarcă zilnic, **până la ora 24:00**, mesajele de ordonanțare de plată (pct. 3.5). Formularul din Anexa 6 („ORDONANȚARE DE PLATĂ”, cu temei art. 236 alin. (20)–(21) CPF) conține numărul de evidență a plății, ID-ul popririi, CIF-ul debitorului, suma de virat și IBAN-ul RO08TREZ999506702XXXXXXX.
4. **Băncile fără ordonanțare**: suma rămâne indisponibilizată. Dacă e-Popriri a actualizat suma rămasă la zero, poprirea se ridică (W6, W8).

### W4. Virarea, validarea, returul și actualizarea sumei rămase
1. **IC**: la deschiderea programului descarcă ordonanțările și **în aceeași zi** virează suma în contul 50.67.02 de la TOC (pct. 3.8, 4.1). Ordinul de plată se completează astfel: Beneficiar = denumirea debitorului; CIF = CIF-ul debitorului; IBAN = RO08TREZ999506702XXXXXXX; „La” = „Trezoreria Operativă Centrală”; „Nr. de evidență a plății” = cel din adresa de înființare (pct. 3.8 lit. a)–e)). Cooperativele de credit trec CIF-ul cooperativei la plătitor (pct. 4.1). Temei legal: art. 236 alin. (21) CPF, plata în „contul special deschis la Trezoreria Operativă Centrală, cu indicarea numărului de evidență a plății”.
2. **TOC**: validează automat, cu un program CNIF, corespondența dintre numărul de evidență a plății și CIF-ul din e-Popriri. **Nu validează** viramentul dacă suma rămasă în e-Popriri este „zero” lei (pct. 4.2) sau dacă suspendarea popririi a fost încărcată pe platformă (pct. 4.2^1, introdus de 626/2023).
3. **Retur**: viramentele nevalidate se returnează în contul din care au venit, „în următoarea zi lucrătoare înregistrării în contabilitate” (pct. 4.3). Același lucru se întâmplă dacă numărul de evidență este greșit (art. 236 alin. (21) teza a 2-a).
4. **Distribuire**: sumele validate se distribuie în aceeași zi către contul 50.67.02 deschis pe CIF-ul debitorului la Trezoreria la care este arondat OFC. Se înregistrează a doua zi lucrătoare, iar OFC primește fișierul încasărilor (pct. 4.4).
5. **TOC → e-Popriri**: trimite CIF-ul beneficiarului, suma, numărul și data documentului, numărul de evidență, SWIFT/BIC-ul băncii, data extrasului, data plății, CIF-ul plătitorului și explicațiile (pct. 4.5). Pe baza lor se actualizează automat suma rămasă (pct. 4.6). Dacă transmiterea nu reușește până la ora 20:00, informațiile trec în ziua lucrătoare următoare (pct. 8.7).
6. **Plata excedentară**: sumele încasate prin poprire peste creanță se restituie **din oficiu, în cel mult 5 zile lucrătoare** de la încasare (art. 168 alin. (4) lit. b) CPF). Erorile materiale din viramente se corectează conform art. 164 CPF (pct. 8.3).

### W5. Nevirare sau virare întârziată după ordonanțare
1. **e-Popriri**: dacă banca nu virează sau virează parțial în ziua descărcării, suma rămasă se actualizează cu ce s-a virat. Confirmarea neonorată „se anulează automat” și sortarea se reia. Banca retransmite suma disponibilă actualizată (pct. 3.9–3.10).
2. **PC → OFC**: trimite un „Referat privind nevirarea” (Anexa 7, pct. 3.11).
3. **OFC**:
   - pentru nevirare totală sau parțială, analizează **răspunderea solidară** a băncii „în limita sumei rămase nestinsă” (pct. 3.12 lit. a)–b); art. 25 alin. (1) lit. b) CPF: terții popriți răspund pentru încălcarea art. 236 alin. (9), (11), (13), (14), (18), (20) și (21), „în limita sumelor sustrase indisponibilizării”);
   - pentru întârziere, analizează **contravenția**: art. 336 alin. (1) lit. k) („nerespectarea obligațiilor ce îi revin terțului poprit, potrivit art. 236 alin. (9) - (11) și (21)”), cu amenda de la alin. (2) lit. d): 1.000–5.000 lei pentru persoanele juridice mari și mijlocii, 500–1.000 lei pentru celelalte. Banca își poate susține poziția (pct. 3.12 lit. c)).
4. **Nulitate**: încălcarea art. 236 alin. (9), (11), (13), (14), (18), (20) și (21) „atrage nulitatea oricărei plăți” (art. 236 alin. (16)).

### W6. Actualizarea sumelor după stingeri (plăți directe, compensări)
1. **OFC → e-Popriri**: în fiecare zi, până la **ora 11:00**, transmite numărul de evidență, CIF-ul și suma rămasă de recuperat (pct. 6.1).
2. **e-Popriri**: păstrează **cea mai mică** valoare dintre suma pe care o are și cea comunicată de OFC (pct. 6.2).
3. **PC → IC**: trimite „Adresa pentru înștiințarea instituțiilor de credit privind sumele rămase de recuperat” (Anexa 3), către toate băncile care au primit adresa de înființare (pct. 6.3, modificat de 626/2023). Adresa „produce efecte din momentul primirii” și banca înregistrează ziua și ora. Temei: art. 230 alin. (5) și art. 233 alin. (3) CPF.
4. **Dacă suma rămasă este 0 lei**: poprirea se ridică (pct. 6.4, trimite la 7.2).
5. **Plata de către client prin altă bancă sau la ghișeu**: banca terț poprit nu poate ridica singură poprirea. Trebuie să vină actualizarea sau decizia de ridicare prin e-Popriri (BCR E5: „sumele rămân blocate până la primirea adresei de ridicare”; Libra E3: „Banca nu mai poate ține cont de dovezile de plată”).

### W7. Suspendarea, continuarea și sistarea (suspendarea indisponibilizării)
1. **Motive de suspendare** (art. 233 CPF): instanța sau creditorul (alin. (1) lit. a)); comunicarea aprobării unei înlesniri (lit. b)); cerere de restituire/rambursare cel puțin egală cu creanța (alin. (2)); scrisoare de garanție la contestație (alin. (2^1), art. 235); poprirea care împiedică activitatea, cu efecte sociale deosebite: suspendare totală sau parțială, o singură dată în 2 ani, maximum 6 luni, cu garanții (alin. (7)–(10)); eșalonare simplificată (art. 209^11).
2. **OFC → e-Popriri**: transmite starea „suspendare totală/parțială” sau „continuare” (878 pct. 5.1; stările de la pct. 1.2 lit. i)).
3. **PC → IC**: emite prin e-Popriri adresa de suspendare (temporară, totală sau parțială a indisponibilizării) sau adresa de continuare. Încărcarea se face până la 24:00 și se consideră comunicată la începutul zilei bancare următoare (pct. 5.2 + 2.3). Pentru aceste adrese **banca nu mai trimite mesaj de neînființare**, starea se actualizează săptămânal din bazele ANAF (pct. 5.2). Pe hârtie, prin UIR, se folosesc anexele 16 și 18 din OPANAF 967/2017 (modificat de 362/2022).
4. **Efectul la bancă**:
   - sumele **viitoare** nu se mai indisponibilizează, de la data și ora comunicării (art. 233 alin. (4));
   - sumele **existente** rămân blocate, dar se pot folosi doar pentru plata obligațiilor fiscale și a salariilor (art. 233 alin. (5); 878 pct. 5.3: banca le virează în conturile de disponibil/venituri ale obligațiilor fiscale). La eșalonarea simplificată regula este aceeași (art. 209^11 alin. (2)–(4)).
5. **TOC** nu validează viramentele făcute în timpul suspendării (pct. 4.2^1).
6. **Continuarea**: comunicarea adresei duce la virarea sumelor indisponibilizate și la reluarea indisponibilizării, cu aplicarea cap. III, VI și VII (pct. 5.5).
7. **Suspendare cu garanție (art. 235)**: suma pentru care s-a înființat poprirea „se actualizează în cel mult două zile de la data intervenirii suspendării” (art. 233 alin. (3)).
8. **Dacă banca nu primește notificarea de suspendare**: e obligată să continue conform art. 236 alin. (13), (14), (20) și (21) (art. 236 alin. (17)).
9. **Terminologie folosită de bănci** (secundar, Libra E3): „Sistarea înseamnă încetarea oricăror acte de executare … și presupune deblocarea conturilor”, iar suspendarea înseamnă că măsurile se mențin în anumite condiții.

### W8. Ridicarea (încetarea) popririi
1. **Când**: creanța s-a stins integral, titlul a fost desființat sau alt caz legal (art. 234 alin. (1)). Poprirea se ridică și pentru excedent, dacă sumele indisponibilizate acoperă creanța (alin. (4)), sau dacă a rămas „fără obiect” (alin. (5)).
2. **Termen**: decizia de ridicare se întocmește „în cel mult două zile de la data la care a încetat executarea silită”, iar întârzierea atrage răspunderea funcționarului (art. 234 alin. (2), art. 341 alin. (2)).
3. **Cum ajunge la bancă**:
   - dacă stingerea s-a reflectat în e-Popriri și suma rămasă este 0, PC emite automat decizia de ridicare (878 pct. 7.2);
   - în alte cazuri de încetare, OFC transmite starea „ridicare” și PC emite decizia (pct. 7.1–7.2).
   Încărcarea se face până la 24:00, iar efectul e la începutul zilei bancare următoare (pct. 2.3).
4. **Popririle vechi**, înființate înainte de OMFP 1665/2020, se ridică tot prin e-Popriri: PC preia deciziile de la UIR (art. 11 alin. (3) 878).
5. **Partea băncii** (secundar): deblocarea se face doar pe adresa electronică, „fără a emite o adresa de sistare în format letric” (BCR E5).

### W9. Sumele exceptate sau neurmăribile (operațiunea zilnică a băncii)
1. **Ce nu se poate popri din cont**:
   - creditele nerambursabile și finanțările pentru programe/proiecte (art. 236 alin. (2) CPF; art. 781 alin. (5) lit. b) CPC rep.);
   - salariile și pensiile, care se urmăresc la plătitor „numai în condițiile Codului de procedură civilă” (art. 236 alin. (4) CPF). În cont, băncile aplică limitele din art. 729 CPC rep.: 1/3 la o poprire, 1/2 la mai multe; alocațiile și indemnizațiile cu destinație specială nu se popresc deloc (legeaz art. 728 vechi, secundar; aplicarea practică: BCR E5 „poți retrage 2/3 din venit…”, Libra E3 punctul 19).
2. **Plăți permise din contul poprit** (art. 236 alin. (14) lit. a)–d)): salarii și contribuțiile reținute la sursă (pe declarație pe propria răspundere că debitorul nu are alte disponibilități; declarația dată cu rea-credință atrage răspunderea solidară a reprezentantului, art. 25 alin. (1) lit. c)), accize și obligații de care depinde o înlesnire.
3. **Fereastra de timp la e-Popriri**: aceste plăți se pot face doar până când banca raportează suma disponibilă (art. 236 alin. (20); 878 pct. 3.4). Concret: banca trebuie să identifice și să excludă sumele neurmăribile **înainte de 17:00 în ziua bancară următoare**. Băncile cer documente mai devreme: ING până la 13:00, Libra până la 16:00 în ziua înființării, BCR procesează până la 16:30 (E1, E3, E5).
4. **Client → bancă**: cerere de deblocare cu acte (fluturaș de salariu, talon de pensie, adeverință de la angajator, documente pentru fonduri UE), prin formularele BCR, BRD, CEC sau e-mail la Libra (E3, E5–E7).
5. **Dacă banca a virat din greșeală** sume nerambursabile sau sume de la art. 729 alin. (7) CPC: se restituie „la cererea instituției de credit sau a contribuabilului/plătitorului, chiar dacă acesta înregistrează obligații restante” (art. 168 alin. (12) CPF).

### W10. Poprirea asigurătorie (măsura asigurătorie pe conturi)
1. **Organul de control → decizie**: întocmește referatul justificativ (OPANAF 2546/2016 pct. 24–27) și emite decizia de instituire, motivată și semnată de conducător. Condiția este „pericolul ca acesta să se sustragă, să își ascundă ori să își risipească patrimoniul” (art. 213 alin. (2), (4), (5) CPF). Măsura nu se dispune pentru debitorii în insolvență sau fără active (alin. (2^1)). Decizia trebuie să spună că măsura se ridică dacă debitorul constituie o garanție (alin. (4)).
2. **Organul de executare → IC**: trimite „Adresa de înființare a popririi asigurătorii asupra disponibilităților bănești” (OPANAF 63/2017 anexa 10; C7). Efectul: indisponibilizare fără virare. Banca acceptă totuși plățile de la art. 236 alin. (14) și plățile obligațiilor fiscale către organul care a înființat poprirea (art. 236 alin. (15)). Excepție: la poprirea asigurătorie pe sume datorate de terți (nu pe conturi), terțul virează suma în contul de garanții (art. 236 alin. (9) lit. c)).
3. **Durată**: dacă titlul de creanță nu e emis și comunicat în 6 luni (prelungibil până la un an), măsura încetează. Decizia de ridicare se dă în 2 zile (art. 213 alin. (7)). Dacă s-au sesizat organele penale, măsura rămâne 3 luni (alin. (8)).
4. **Transformarea în executorie**: la scadență și în caz de neplată, măsura devine executorie (art. 213 alin. (2)–(3)). OFC trimite „adresă de înființare a popririi executorii” (art. 236 alin. (7)). În e-Popriri există starea „transformare” (878 pct. 1.2 lit. i1) și pct. 2.8).
5. **Ridicarea**: prin decizie motivată, când au încetat motivele sau după constituirea garanției de la art. 211. Decizia se comunică tuturor destinatarilor (art. 214 alin. (1)–(2)), iar executorul o pune în aplicare în cel mult 2 zile (OPANAF 2546/2016 pct. 83). Decizia de instituire se poate ataca în 30 de zile la contencios (art. 213 alin. (13)).

### W11. Garanțiile constituite la ANAF prin scrisoare de garanție bancară (SGB) sau poliță de asigurare de garanție
1. **Când se cere o garanție** (art. 210 CPF): suspendarea executării (art. 233 alin. (2^1) și (8)), ridicarea măsurilor asigurătorii, angajamentul de plată (art. 24) și alte cazuri. Mai apare la eșalonare (art. 193 alin. (6) lit. b)) și la înlocuirea sechestrului (art. 241 alin. (1) lit. b)).
2. **Formele admise** (art. 211): consemnare la Trezorerie; „scrisoare de garanție emisă de o instituție de credit sau poliță de asigurare de garanție”. O SGB emisă din afara UE trebuie confirmată de o bancă sau un asigurător din România. Din UE se acceptă dacă s-a respectat notificarea prevăzută de Directiva 2013/36/UE sau de Solvabilitate II. Mai sunt admise ipoteca și gajul.
3. **Conținutul obligatoriu al SGB** (OPANAF 90/2016, Anexa 2, art. 6 alin. (7)): emitentul; data emiterii și perioada de valabilitate, „cu cel puțin 3 luni mai mare decât scadența ultimei rate” la eșalonare; valoarea; obiectul; semnăturile autorizate; „angajamentul ferm al entității emitente de a plăti suma stabilită, în mod necondiționat și irevocabil, la solicitarea organului fiscal competent”. Pentru garanțiile de la art. 235 nu am găsit un model aprobat prin ordin separat (vezi Incertitudini).
4. **Suspendarea executării cu SGB, pe durata contestației** (art. 235 și 233 alin. (2^1)):
   - a) debitorul notifică intenția după comunicarea deciziei de impunere, iar executarea se suspendă sau nu începe;
   - b) are **45 de zile** să depună SGB sau polița, altfel executarea începe și se aplică amenda de la art. 336 alin. (3^2);
   - c) garanția trebuie să fie la nivelul obligațiilor contestate și neachitate, cu valabilitate de **minimum 6 luni** (art. 235 alin. (1));
   - d) măsurile asigurătorii devenite executorii se ridică (art. 235 alin. (8)), iar suma din poprire se actualizează în 2 zile (art. 233 alin. (3));
   - e) cât timp durează suspendarea nu se calculează accesorii (art. 235 alin. (6));
   - f) debitorul poate cere eșalonare, iar SGB devine garanție de eșalonare dacă i se prelungește valabilitatea (alin. (5^1)).
5. **Executarea SGB** (art. 235 alin. (2)): după respingerea definitivă a acțiunii în contencios, ANAF execută garanția „după 30 de zile …, dar nu mai târziu de ultima zi de valabilitate”, dacă: debitorul nu plătește, nu depune o garanție nouă și instanța nu a suspendat actul. SGB rămâne fără obiect dacă contestația sau acțiunea e admisă, dacă instanța suspendă actul sau dacă debitorul plătește integral (alin. (3)). La plată parțială garanția se poate redimensiona (alin. (4)).
6. **Cererea către bancă** (art. 212 alin. (2)): organul fiscal „dispune instituției de credit … emitente … virarea, în termen de cel mult 15 zile de la data solicitării, a sumei” în conturile de venituri. Dacă emitentul (sau banca care a confirmat garanția) nu virează, răspunde **solidar** cu debitorul (art. 25 alin. (1) lit. d)). Art. 2.321 alin. (3) din Codul civil rămâne aplicabil.
7. **Executarea la ultima zi de valabilitate**: la garanțiile pentru ridicarea măsurilor asigurătorii, dacă valabilitatea nu e prelungită, garanția se execută „în ultima zi de valabilitate”, iar suma devine consemnare de garanție (art. 213 alin. (8^1)). Regula e aceeași la înlocuirea sechestrului cu SGB (art. 241 alin. (1) lit. b)).
8. **La eșalonare**: SGB trebuie să acopere ratele și dobânzile (art. 193 alin. (14)) și să fie valabilă cu cel puțin 3 luni peste ultima rată (alin. (17)). La pierderea eșalonării, garanțiile se execută (art. 202 alin. (1)). La cererea debitorului, ANAF poate cere emitentului executarea SGB pentru plata ratelor la scadență (art. 207 alin. (7)). O copie a deciziei de eșalonare se comunică emitentului SGB (OPANAF 90/2016, Anexa 2, art. 7 alin. (2); art. 13 alin. (8)).

### W12. Clientul cere deblocarea (partea băncii; surse secundare)
1. **Client → bancă**: formular online (BCR „Formular de contact popriri”, BRD „Cerere de deblocare poprire”, CEC „Contact popriri”), e-mail (Libra) sau ING Office. Atașează documente: fluturaș de salariu, talon de pensie, state de plată, documente pentru fonduri UE, dovada plății (E2, E3, E5–E7).
2. **Banca**:
   - verifică dacă sumele sunt neurmăribile și dacă soldul popribil **nu a fost deja raportat** la ANAF (ING E1);
   - deblochează temporar: la BCR după 45 de minute, pentru 120 de minute, cu procesare în aceeași zi până la 16:30 (E5);
   - la popririle ANAF, dovada plății nu ridică poprirea. E nevoie de decizia de ridicare sau de actualizare din e-Popriri (E3, E5). La executorii judecătorești, BCR verifică direct cu executorul autenticitatea adresei de ridicare (E5).
3. **Ce poate face clientul legal**: contestație la executare în 15 zile (art. 260–261 CPF; mențiunea din Anexa 3 a 878), suspendarea de la art. 233 alin. (7) sau o înlesnire la plată (W7). Pe partea băncii, Raiffeisen percepe 25 lei pentru fiecare plată făcută în executarea unei popriri (E8).

### W13. Mecanisme paralele (pe scurt)
- **Organele fiscale locale** (primăriile) aplică tot art. 236 CPF, dar **fără e-Popriri**: alin. (20)–(21) se aplică doar „în cazul popririlor înființate de organul fiscal central”. Rămâne regula generală a plății în 3 zile lucrătoare (art. 236 alin. (13)). Conturile le află din Registrul central de conturi din PatrimVen (D10). Libra spune că pentru primării plata se face în interiorul celor 3 zile lucrătoare (E3, secundar).
- **Executorii judecătorești** aplică CPC, art. 781 și următoarele (rep.). Libra (secundar) indică plata în „5 zile calendaristice”. Salariile pot fi plătite din contul poprit doar 3 luni (art. 781 alin. (5) lit. c) CPC rep.; ING E2).
- **Mai mulți creditori pe același cont**: banca îi anunță în scris și distribuie conform ordinii de preferință de la art. 258 CPF (art. 236 alin. (11)). Creanțele fiscale sunt la lit. e), după cheltuielile de executare, salarii și pensii și obligațiile de întreținere. Între popririle e-Popriri plătește „în ordinea vechimii primirii adreselor” (878 pct. 8.5). În practică (secundar): întâi fiscul, apoi executorii judecătorești, iar între organe fiscale diferite proporțional (BCR E5, Libra E3).

### W14. Garanțiile vamale (pe scurt; neverificat aproape în întregime)
- Garanțiile pentru datoria vamală (garanție izolată sau globală, angajamentul garantului emis de o bancă sau de un asigurător) sunt reglementate în principal de **Regulamentul (UE) nr. 952/2013 (Codul Vamal al Uniunii), art. 89–100**, și de actele delegate și de punere în aplicare (UE) 2015/2446 și 2015/2447. **Neverificat**: EUR-Lex a răspuns cu o pagină challenge, iar customs.ro cu 403.
- La nivel național, normele tehnice OPANAF 1889/2016 (garanții în tranzit) au fost **abrogate prin OPANAF 159/2024** (C12, verificat). Rapoartele ANAF din 2024 arată ordine comune AVR–ANAF (de ex. nr. 5/5/2024, D5), deci partea vamală e acum la Autoritatea Vamală Română. Competența exactă asupra garanțiilor vamale este **neverificată**.

---

## Explicații

1. **De ce există ordonanțarea.** Înainte de OMFP 1665/2020, și pentru băncile fără convenție până la 30.09.2023, fiecare bancă a debitorului primea adresa de poprire și trebuia să plătească în 3 zile (art. 236 alin. (13) CPF). Rezultatul era că se plătea dublu sau triplu, iar banii se recuperau abia prin restituire. Libra (secundar, E3) descrie problema: „se întâmpla frecvent ca două bănci să vireze aceeași sumă”. ANAF (D2) prezintă soluția: sumele confirmate sunt sortate „în ordine descrescătoare” și se cere virarea „numai acelor bănci care dețin disponibilități bănești ce acoperă suma poprită”. Legal, asta se face separând **indisponibilizarea** (de la primirea adresei, art. 236 alin. (8), (13)) de **plată**, care vine doar la ordonanțare (art. 236 alin. (20)–(21); 878 pct. 3.4–3.8). Prețul plătit de bănci: conturile debitorului rămân blocate la toate băncile până vine ordonanțarea sau actualizarea la zero. Plățile excepționate (salarii etc.) se pot face doar până la raportare. Asta comprimă munca de clasificare a încasărilor într-o fereastră de circa o zi bancară.

2. **Cronologia cadrului e-Popriri.**
   - OMFP 691/2017: portal, PDF+XML semnat, protocol cu ARB.
   - OMFP 1665/2020, în vigoare la 15 zile după MO 200/12.03.2020: introduce ordonanțarea, aplicabilă doar băncilor care semnau convenția-cadru (pct. 8.4).
   - e-Popriri intră în funcțiune „începând cu anul 2021” (D2; Libra: „ulterior lunii Ianuarie 2021”, E3).
   - OMF 128/2022: contul TOC 50.67.02 și validarea automată.
   - OPANAF 878/2022, din 11.05.2022: competența trece la președintele ANAF.
   - OPANAF 626/2023: din **01.10.2023** abrogă pct. 8.4 și convenția-cadru, deci fluxul cu ordonanțare devine obligatoriu pentru toate băncile. ING (E1) confirmă: „Ordinul este obligatoriu pentru toate instituțiile de credit din Romania”.
   Baza legală din CPF (art. 236 alin. (20)–(21)) a apărut între 08.02.2019 (A2) și 2021 (A3). Actul modificator exact este neidentificat.

3. **Ceasul unei popriri e-Popriri.** Cazul tipic, cu D = ziua bancară în care banca primește adresa:
   - D-1, până la 24:00: ANAF încarcă adresa;
   - D, la ora de start a zilei bancare: poprirea e înființată și conturile sunt blocate;
   - D+1, până la 17:00: banca raportează suma ce poate fi plătită;
   - D+1, până la 24:00: ANAF încarcă ordonanțarea;
   - D+2, la deschidere: banca descarcă ordonanțarea și virează în aceeași zi;
   - D+2/D+3: TOC validează și distribuie, e-Popriri actualizează restul (la 20:00 se închide colectarea viramentelor, la 11:00 vin actualizările de la OFC);
   - dacă restul ajunge la 0, decizia de ridicare se încarcă până la 24:00 și deblochează la începutul zilei bancare următoare.
   Totul se bazează pe 878 pct. 2.3, 3.2, 3.5, 3.8, 4.2–4.6, 6.1, 7.2 și 8.7. Libra (E3) rezumă: banca plătește „în 2 sau mai multe zile de la înființare”, iar încasările ulterioare „cel târziu a doua zi lucrătoare”.

4. **Ce NU poate face banca.**
   - Să plătească altă sumă decât cea din ordonanțare, sau unui alt creditor fiscal, la popririle e-Popriri (878 pct. 3.6, 3.8).
   - Să lase clientul să aleagă banca plătitoare (Libra E3).
   - Să deblocheze pe baza dovezii de plată la popririle ANAF; e nevoie de decizia sau actualizarea prin e-Popriri (BCR E5, ING E2, Libra E3).
   - Să mai pună la dispoziție soldul după ce l-a raportat (878 pct. 3.4; ING E1).
   - Să plătească peste suma din adresă (art. 236 alin. (13)).
   - Să ignore o suspendare comunicată (art. 233 alin. (4)–(5)) sau să continue indisponibilizarea sumelor viitoare după suspendare.
   - Să închidă contul poprit (BCR E5, secundar).
   Riscurile: nulitatea plății (art. 236 alin. (16)), răspunderea solidară (art. 25 alin. (1) lit. b)), amenda (art. 336 alin. (1) lit. k)).

5. **Mai multe bănci și mai multe popriri.** Între băncile aceluiași debitor decide algoritmul de alocare descrescătoare (pct. 3.5–3.6). Pe aceeași bancă, popririle ANAF se plătesc „în ordinea vechimii primirii adreselor” (pct. 8.5). Dacă pe cont există și popriri ale altor creditori, se aplică art. 236 alin. (11) și ordinea de la art. 258 CPF. Dacă debitorul deschide un cont nou la altă bancă, se trimite o adresă nouă la nivelul soldului (pct. 2.6).

6. **Modelul de date** (util pentru prototip; sursa este 878 cu anexele):
   - cheia este „numărul de evidență a plății”, unic pentru fiecare adresă (pct. 1.2 lit. d), 1.5). Formularele mai folosesc și un „ID poprire” (Anexele 4–7);
   - stări: transformare, înființare, suspendare totală sau parțială, continuare, ridicare, actualizare sume (pct. 1.2 lit. i));
   - mesaje de la bancă la ANAF: neînființare cu codurile 1–5 (Anexa 4); sumă ce poate fi plătită (Anexa 5);
   - mesaje de la ANAF la bancă: adresă de înființare (OPANAF 967/2017 anexa 6), actualizare (Anexa 3), ordonanțare (Anexa 6), suspendare/continuare (967/2017 anexele 16/18, prin 362/2022), decizie de ridicare;
   - de la TOC la e-Popriri: cele 10 câmpuri de la pct. 4.5;
   - arhivare 5 ani după încetarea executării (pct. 8.6); canal de rezervă pentru schimb masiv de date (pct. 8.2).
   Schema XML **nu este publică** (neverificat).

7. **Volume** (D3–D6): circa **2,2–2,5 milioane de acte e-Popriri pe an** (2023: 2.168.482; 2024: 2.528.710). În 2024 au fost circa 1,26 milioane de înființări și circa 1,17 milioane de ridicări, adică aproape fiecare poprire generează și o ridicare. Popririle bancare aduc circa 25% din încasările din executare silită (2023: 3,909 mld. din 15,78 mld. lei, D7). În S1 2025 au adus 2,92 mld. lei. Raportat la piață, fiecare bancă procesează zilnic mii de mesaje (înființare, raportare, ordonanțare, plată, actualizare, ridicare, suspendare), cu termene la oră fixă.

8. **Unde se pierde eficiența la bancă** (analiză proprie, nu sursă):
   - clasificarea încasărilor (salarii, pensii, alocații, fonduri UE) înainte de raportarea de la 17:00;
   - cererile de deblocare venite pe canale diferite (formular, e-mail, ghișeu) și verificate manual (ING: „presupune și analiza umana”, E1);
   - reconcilierea dintre viramente, retururile TOC (zero sau suspendare) și actualizări;
   - dovezile de plată pe care banca nu le poate folosi la ANAF;
   - popririle greșit adresate (cazul Revolut/Libra, E4);
   - urmărirea valabilității SGB emise în favoarea ANAF și cererile de executare cu termen de 15 zile (art. 212 alin. (2)).
   Raiffeisen nu are în sitemap o pagină FAQ publică despre popriri (E8). E un gol de comunicare față de BCR, ING și Libra.

9. **Garanțiile: rolul băncii ca emitent.** SGB în favoarea ANAF trebuie să fie necondiționată și irevocabilă (OPANAF 90/2016, Anexa 2, art. 6 alin. (7) lit. f)). Momentele critice sunt: valabilitatea minimă (6 luni la art. 235; cu cel puțin 3 luni peste ultima rată la eșalonare), executarea „după 30 de zile” de la respingerea definitivă „dar nu mai târziu de ultima zi de valabilitate” (art. 235 alin. (2)), executarea în ultima zi la garanțiile pentru măsuri asigurătorii (art. 213 alin. (8^1)) și virarea în 15 zile de la cerere (art. 212 alin. (2)). Altfel banca devine **solidar răspunzătoare** (art. 25 alin. (1) lit. d)).

---

## Incertitudini

1. **legislatie.just.ro a fost inaccesibil.** Nu am verificat formele consolidate oficiale. Textul CPF provine din consolidarea ANAF („nu reprezintă o republicare”). Istoricul fiecărui alineat (ce act l-a modificat) este neverificat. Știu doar că art. 25, 168, 210–214, 233–236 sunt identice în fond între consolidarea din 2021 (OG 11/2021) și cea din 2026 (Legea 170/2026).
2. **Modificări ale OPANAF 878/2022 după 626/2023.** Nu am găsit niciuna: nici în PDF-urile OPANAF 2023–2026 arhivate pe static.anaf.ro (85 de fișiere verificate cu grep „878/2022”), nici în listele de acte normative din rapoartele ANAF 2023, S1 2024, 2024 și S1 2025. **Nu pot exclude** ordine din 2025–2026 nearhivate. MO-ul OPANAF 626/2023 este neverificat.
3. **Protocolul ANAF–ARB–bănci pentru e-Popriri** (menționat la art. 12 din 878): numărul, data și conținutul nu au fost găsite. Nu am găsit nici o poziție publică ARB sau BNR despre e-Popriri. Ce înlocuiește convenția-cadru abrogată la stabilirea „orei de începere a zilei bancare” este neclar (neverificat).
4. **Ordinul care a introdus art. 236 alin. (20)–(21) CPF**: neidentificat. A apărut între 02/2019 și 2021.
5. **Codul de procedură civilă**: am folosit legeaz.net (secundar, numerotare veche). Forma actuală a art. 729 și 781 (rep.) și eventualele modificări 2023–2026 privind sumele protejate în cont (un „prag minim neurmăribil”) sunt **neverificate**.
6. **„PROIECT”-ele din prezentarea ANAF Ploiești (08.2024)**: (a) art. 236 alin. (12), care ar fi interzis poprirea salariilor sau pensiilor încasate în cont după înființarea popririi; (b) plafoanele zilnice de numerar pentru firmele cu popriri (5.000/2.500/1.000 lei), prin derogare de la Legea 70/2015. Nu apar în CPF-ANAF (Legea 170/2026), deci par neadoptate. Separat de Legea 70/2015, statutul lor este **neverificat**.
7. **Modele SGB**: nu am găsit un model aprobat prin ordin pentru SGB sau polița de la art. 235 CPF. Am găsit doar elementele obligatorii pentru eșalonare (OPANAF 90/2016). Nici vigența actuală a OPANAF 90/2016 (alte modificări după 1960/2019) nu am verificat-o.
8. **Garanțiile vamale**: temeiurile UE (Reg. 952/2013 art. 89–100, Reg. 2015/2446 și 2015/2447) le cunosc din surse generale. EUR-Lex și customs.ro au fost inaccesibile, deci sunt neverificate. Competența actuală a AVR asupra garanțiilor vamale este neverificată.
9. **Băncile**: paginile BT (403) și UniCredit (nu există pagină în sitemap) nu au fost verificate. Paginile BCR, ING și Libra sunt interpretări ale băncilor (secundar). Libra spune că pentru ANAF se folosește „adresa de sistare … în original”; formularea pare depășită pentru e-Popriri.
10. **Modelul pentru ordinul C7** (anexele 10 și 11 din 16.12.2016): ordinul de aprobare nu e identificat. Probabil e o formă de anexă la OPANAF 63/2017.
11. **Statistici**: cifrele din S1 2025 la ridicări includ acte „comunicate … până la finalul anului 2024”, deci nu sunt strict semestriale. Nu am găsit date despre numărul de ordonanțări, retururi TOC sau referate de nevirare.
12. **OPANAF 6.138/2024** (cazuri speciale, abrogă 3744/2015): l-am descărcat, dar nu l-am analizat. Nu am verificat dacă afectează competența Punctului central (OPANAF 2153/2019).
