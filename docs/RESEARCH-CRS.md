# Research: CRS/FATCA după deschiderea contului (Raiffeisen, AI for Operational Efficiency)

Faptele pe care construim regulile, demo-ul și pitch-ul. Fiecare are sursa lângă; paginile sunt din
[Ghidul ANAF DAC2/CRS/FATCA, varianta 29.12.2025](https://static.anaf.ro/static/10/Anaf/AsistentaContribuabili_r/AEOI/Ghid%20DAC2%20CRS%20FATCAVARIANTA%2029.12.2025.pdf)
(„ghidul”), citite pe 28 oct. Ce e marcat **de verificat** nu intră în video până nu e confirmat.

## 1. Problema, pe scurt

Banca trebuie să știe unde plătește taxe fiecare client și să raporteze la ANAF conturile celor rezidenți fiscal în alte
țări: **CRS** (Directiva DAC2) pentru statele UE și OCDE, **FATCA** pentru SUA. Clientul declară rezidența într-o
autocertificare („declarația pe propria răspundere”). Problema apare **după** deschiderea contului: o modificare de date
(adresă, telefon, ordin permanent, împuternicire) poate face autocertificarea nesigură, iar banca are un termen să
clarifice situația. Altfel raportează greșit, pe fiecare cont.

## 2. Ce spune ghidul (regulile pe care le punem în cod)

### Modificarea circumstanțelor

| Fapt | Pagina |
| --- | --- |
| O „modificare a circumstanțelor” e **orice modificare care adaugă informații relevante despre situația unei persoane sau intră în contradicție cu ea**, inclusiv la conturile asociate. | p. 30 |
| Dacă o modificare asociază un indiciu cu contul, banca îl tratează pe titular ca **rezident fiscal în fiecare jurisdicție pentru care are un indiciu**. Ghidul recomandă contactarea clientului, pentru că pot exista indicii „false”. | p. 30 |
| Cont nou: autocertificarea **își pierde valabilitatea** la data la care banca știe sau are motive să știe că circumstanțele s-au schimbat. | p. 35–36 |

### Termenul diferă pe tipul de cont

| Tip de cont | Termen | Dacă banca nu obține nimic | Pagina |
| --- | --- | --- | --- |
| **Cont nou** (CRS: deschis la 1 ian. 2016 sau după) | **90 de zile calendaristice** de la pierderea valabilității. Până atunci, banca poate păstra statutul vechi. | Clientul e rezident **și** în țara din autocertificarea inițială, **și** în țara rezultată din modificare. | p. 20–21, 35–36 |
| **Cont preexistent** (CRS: existent la 31 dec. 2015) | Ultima zi a anului calendaristic (sau a perioadei de raportare) **sau** 90 de zile de la notificarea/descoperirea modificării. | Banca aplică căutarea în evidența electronică și îl tratează ca rezident cel puțin în noua jurisdicție (exemplul 2). | p. 29 |

**Decis:** ghidul spune „sau” între cele două termene pentru conturile preexistente. Aplicăm „cea mai târzie dintre
ele”, cum scrie legea pentru contul documentat prin adresa de rezidență
([Codul de procedură fiscală](https://legislatie.just.ro/Public/DetaliiDocument/312603), Anexa nr. 2 pct. 1: „oricare
dintre acestea este mai recentă”) și
[Comentariul OCDE la CRS](https://www.oecd.org/content/dam/oecd/en/publications/reports/2017/03/standard-for-automatic-exchange-of-financial-account-information-in-tax-matters-second-edition_g1g73eb6/9789264267992-en.pdf)
(Secțiunea III, pct. 13, p. 115: „by the later of”). La indiciile din căutarea electronică legea spune doar „ori”
(Anexa nr. 1, secțiunea III, subsecțiunea B, pct. 16); acolo „cea mai târzie” e interpretarea noastră, ca în
Comentariul OCDE (pct. 16, p. 116). Afișăm pagina.
Consecința pentru demo: o modificare din martie, pe un cont preexistent, are termen 31 decembrie, nu 90 de zile.
De aceea **contul Mariei e un cont nou** (deschis după 2016), ca termenul de 90 de zile să fie cel care contează.

### Indiciile CRS (căutarea în evidența electronică)

| # | Indiciu | Capcana |
| --- | --- | --- |
| a | Titularul e identificat ca rezident al unei jurisdicții raportabile | |
| b | Adresă de corespondență sau de reședință actuală (inclusiv căsuță poștală) într-o jurisdicție raportabilă | Cluj → Iași nu e indiciu |
| c | Unul sau mai multe telefoane dintr-o jurisdicție raportabilă **și niciun telefon în România** | Telefon german lângă unul românesc: **nu** e indiciu |
| d | Instrucțiuni permanente de transfer către un cont dintr-o jurisdicție raportabilă, **altele decât cele privind un cont de depozit** | Contul curent e cont de depozit în sensul CRS: ordinul permanent către un IBAN german **nu** e indiciu; din contul de custodie, **este** |
| e | Împuternicire sau delegare de semnătură valabilă, acordată unei persoane cu adresă într-o jurisdicție raportabilă | |
| f | Adresă „post-restant” sau „în atenția” într-o jurisdicție raportabilă, **dacă banca nu are altă adresă** | |

Sursa: p. 29. În Legea 207/2015 (Anexa 1, Secțiunea III, subsecțiunea B), lista e la pct. 12 (ghidul, p. 42:
„indiciile descrise la pct. 12 subsecţiunea B din secţiunea III”), iar cerințele pentru indicii la pct. 13 (ghidul, p. 30).

### Indiciile FATCA (conturi individuale preexistente)

| Indiciu | Diferența față de CRS |
| --- | --- |
| Cetățean sau rezident al SUA | Cetățenia contează (singura excepție de la „naționalitatea nu e semnal”) |
| Loc al nașterii în SUA, indicat fără ambiguitate | Nu există la CRS |
| Adresă actuală de corespondență sau reședință în SUA | |
| **Număr de telefon actual din SUA** | **Fără** condiția „niciun telefon în România”: telefonul din SUA e indiciu chiar dacă există și unul românesc |
| **Ordine permanente de transfer către un cont din SUA** | **Fără** excepția pentru conturile de depozit: ordinul permanent din contul curent către SUA **este** indiciu FATCA |
| Procură sau drept de semnătură pentru o persoană cu adresă în SUA | |
| Adresă „în grija” sau „păstrare de corespondență”, singura adresă la dosar | La conturile cu valoare mai mică, o adresă „în grija” din afara SUA nu e indiciu |

Sursa: p. 31 (Acordul FATCA, Anexa I, Secțiunea II, paragraful B). Pentru FATCA, „cont preexistent” = existent la
30 iunie 2014 (p. 20).

### Testul de rezonabilitate și explicațiile acceptate

| Fapt | Pagina |
| --- | --- |
| **Discrepanțele** dintre autocertificare și alte informații din dosarul clientului **nu sunt o eroare neimportantă**. | p. 36 |
| Adresa din autocertificare în contradicție cu documentele AML/KYC → autocertificarea pică testul de rezonabilitate. | p. 36–38 |
| O autocertificare **fără TIN** nu e de încredere dacă portalul OCDE arată că jurisdicția emite TIN tuturor rezidenților. Dacă nu emite tuturor, lipsa TIN-ului cu motiv poate fi acceptată. | p. 37 |
| Explicații rezonabile când adresa nu corespunde rezidenței declarate: **student** (cu viza corespunzătoare), **profesor/stagiar** sau participant la un program educațional/cultural, **diplomat** sau angajat al unui consulat/ambasade, **funcționar UE**. | p. 37 |
| O eroare neimportantă (de exemplu jurisdicția prescurtată, dar care se potrivește cu actul de identitate) nu invalidează autocertificarea. Lipsa jurisdicției de rezidență nu e eroare neimportantă. | p. 36 |

### TIN

| Fapt | Sursă |
| --- | --- |
| Comisia Europeană are un serviciu public de verificare a **formatului** TIN pentru statele UE. | [TIN on Europa](https://taxation-customs.ec.europa.eu/online-services/online-services-and-databases-taxation/taxpayer-identification-number-tin_en) |
| OCDE publică, pe jurisdicții, formatul TIN și dacă jurisdicția emite TIN tuturor rezidenților. | [OCDE, portalul AEOI](https://www.oecd.org/en/networks/global-forum-tax-transparency/resources/aeoi-implementation-portal/tax-identification-numbers.html) |
| Validarea formatului nu e același lucru cu potrivirea TIN–persoană. De aceea spunem „format valid”, nu „TIN valid”. | [TAINA](https://www.taina.tech/resources-news-and-awards/why-tin-validation-and-tin-matching-are-not-the-same-thing) |

## 3. Dovezi că problema e reală

| Ce | Cifra | Sursă |
| --- | --- | --- |
| Verificări ANAF pe datele raportate, S1 2025 | 3.204 verificări la 28 de instituții; **891 de înregistrări corectate**; 7 controale specifice privind CRS, inițiate și coordonate (p. 36) | [Raport de activitate ANAF S1 2025](https://static.anaf.ro/static/10/Anaf/Informatii_R/18.08.2025%20-%20Raport%20de%20activitateANAF_2025S1_V7.pdf) |
| Inadvertențe FATCA | IRS a semnalat inadvertențe în datele FATCA raportate pentru 2023 (p. 44) | [Raport de performanță ANAF 2024](https://static.anaf.ro/static/10/Anaf/Informatii_R/Raportperformanta2024.pdf) |
| Amenzi potențiale | 2.000–5.000 lei **pe fiecare cont raportabil** raportat incorect, incomplet sau cu întârziere; 5.000–10.000 lei **pe fiecare cont raportabil** dacă procedurile de diligență nu sunt aplicate | [Codul de procedură fiscală](https://legislatie.just.ro/Public/DetaliiDocument/312603), art. 336 alin. (1) lit. ț) și ț^1), alin. (2) lit. k^1) și k^2), introduse prin [OUG 102/2022](https://legislatie.just.ro/Public/DetaliiDocument/256922) |
| Sancțiuni aplicate în 2024 | 102.000 lei în total, 3 avertismente, 92 de scrisori de avertizare; 6 controale tematice la brokeri și 1 control de fond la o instituție de credit (p. 44–45) | [Raport de performanță ANAF 2024](https://static.anaf.ro/static/10/Anaf/Informatii_R/Raportperformanta2024.pdf) |
| Volum | ~2,4 mil. clienți Raiffeisen România, persoane fizice și juridice; fiecare actualizare de date trebuie evaluată, doar cele relevante fiscal pornesc un termen | [Comunicat Raiffeisen Bank România, 28.10.2026](https://www.raiffeisen.ro/ro/despre-noi/comunicate-de-presa/2026/raiffeisen-bid-finantare-imm-credite-revolving-garantii.html) |
| Poziția Raiffeisen despre AI | CEO-ul: „AI poate pregăti o decizie bancară, dar nu ar trebui să aibă ultimul cuvânt” | [SpotMedia, aug. 2026](https://spotmedia.ro/stiri/economie/zdenek-romanek-ceo-raiffeisen-bank-ai-poate-pregati-o-decizie-bancara-dar-nu-ar-trebui-sa-aiba-ultimul-cuvant) |

**Atenție la cifra de 102.000 lei:** e mică (cam 20.000 € pe tot sectorul) și lucrează împotriva noastră. Argumentul e
**riscul pe fiecare cont** și costul operațional al clarificărilor, nu amenzile aplicate până acum.

## 4. Ce există deja

Furnizorii de software de conformare au module de verificare a autocertificărilor (de exemplu TAINA validează
autocertificările și TIN-urile) și, în general, de „change in circumstances”. Nu spunem „nimeni n-a mai făcut asta”.
Spunem: **metodă cunoscută, aplicată pe fluxul de după onboarding, cu regulile din ghidul ANAF și omul la decizie**.
Partea pe care o automatizăm noi e ce urmează după semnal: dovezile, cererea către client, evaluarea răspunsului,
termenul și închiderea cu dovada confirmării.

## 5. Întrebări pentru mentorul Raiffeisen (prima oră, înainte de cod)

1. Ce se întâmplă azi, pentru CRS, când un client își schimbă adresa sau telefonul într-o altă țară? Cine verifică și în
   cât timp? Aveți o unealtă pentru asta?
2. Câte astfel de cazuri aveți pe lună și câte minute de operator cere unul (verificare, cerere, urmărire, închidere)?
3. Ce parte e cea mai repetitivă: găsirea modificărilor relevante, adunarea dovezilor, scrierea cererii sau urmărirea
   răspunsului?
4. Cum primiți azi autocertificarea nouă de la client (sucursală, online, poștă) și cum dovediți confirmarea la control?
5. Ați accepta un model care rulează pe serverele băncii? Ce restricții aveți pentru modele în cloud?

Răspunsurile la 1 și 2 intră în calculul din [`PLAYBOOK.md`](PLAYBOOK.md#7-business-cine-plătește-și-cât). Dacă
răspunsul la 1 e „totul e automatizat cap-coadă”, mutăm accentul pe evaluarea răspunsurilor și pe situațiile neprevăzute.

## 6. De verificat înainte de pitch

- [x] Termenul pentru conturile preexistente: cea mai târzie dintre 31.12 și +90 de zile: lege la contul documentat prin adresa de rezidență (Codul de procedură fiscală, Anexa nr. 2 pct. 1), ca în Comentariul OCDE (Secțiunea III, pct. 13); interpretarea noastră la indiciile din căutarea electronică (Anexa nr. 1, secț. III, subsecț. B, pct. 16 spune doar „ori”), aliniată la Comentariul OCDE (pct. 16); ghidul (p. 29, la testul adresei de rezidență) spune „sau”.
- [x] Cuantumul amenzilor: Codul de procedură fiscală, art. 336 alin. (2) lit. k^1) și k^2), introduse prin OUG 102/2022 (tabelul din secțiunea 3).
- [x] Termenul de depunere F3000: 15 mai, în [Codul de procedură fiscală](https://legislatie.just.ro/Public/DetaliiDocument/312603), art. 62 alin. (7). [OPANAF 1711/2022](https://legislatie.just.ro/Public/DetaliiDocument/259750) doar aprobă formularele F3000 DAC2/CRS și F3000 FATCA.
- [x] Riscul FATCA (reținerea de 30%): nu îl spunem în pitch. România are [acord FATCA Model 1](https://home.treasury.gov/system/files/131/FATCA-Agreement-Romania-5-28-2015.pdf): o bancă conformă nu e supusă reținerii (art. 4(1)). Doar o neconformare semnificativă nerezolvată în 18 luni de la notificare o poate face instituție nonparticipantă (art. 5(2)(b)), iar reținerea e de 30% ([26 U.S.C. 1471(a)](https://www.govinfo.gov/content/pkg/USCODE-2023-title26/html/USCODE-2023-title26-subtitleA-chap4-sec1471.htm)).
- [x] Datele OCDE pentru Austria, pentru cazul 3: fișa OCDE AT spune că Austria nu emite TIN tuturor persoanelor fizice
  (Q1 „Individuals No”), salvată cu URL, data accesării și citatele în [`surse/oecd-tin-at.md`](surse/oecd-tin-at.md).
- [x] Regula concursului despre codul scris înainte de hackathon: avem voie. [Rulebook-ul](https://nexxtsummit.com/rulebook) nu îl interzice și cere doar ca lucrul pe temă să înceapă la startul oficial (28 oct., 09:00); organizatorii au confirmat pe 28 oct. O spunem deschis în README (secțiunea 13).
