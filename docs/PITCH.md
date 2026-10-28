# CRS Sentinel: pitch și scenariul video (draft)

> Proprietar: Persoana 5 (cu toată echipa). Video ≤ 3:00, filmat pe `AgentMode = Replay`.
> Regula: orice cifră vine dintr-o rulare reală (`/metrics`, testele) sau dintr-o sursă citată. Ce e NEVERIFICAT nu se spune.
> Verifică textul final față de `docs/PLAN.md` secțiunea 24 („Ce nu spunem”).

## Fraza de deschidere (10 secunde)

„Când un client își schimbă adresa, banca are 90 de zile să afle din nou unde plătește el taxe. CRS Sentinel transformă fiecare
modificare de date într-o decizie CRS/FATCA, înainte să expire termenul.”

## Scenariul (≤ 3:00)

| Timp | Ce se vede | Ce se spune |
| --- | --- | --- |
| 0:00–0:20 | Titlul, apoi coada de cazuri | Problema: fiecare modificare trebuie evaluată; doar cele relevante fiscal pornesc un termen de 90 de zile (Ghid ANAF, p. 35–36). Cifrele ANAF despre verificări: doar după ce le citim din sursă (azi NEVERIFICAT). |
| 0:20–0:50 | Maria se mută la München; caz deschis cu termen | Codul găsește două indicii, fiecare cu regula și pagina. Termenul pornește singur. |
| 0:50–1:20 | Pagina cazului: dovezi cu „citat verificat”, semnalul „salariu de la o firmă germană” separat | AI-ul citește documentele și citează; codul verifică fiecare citat. Salariul e doar semnal, fără alertă: nu e indiciu legal. |
| 1:20–1:40 | Cererea către client, butonul de aprobare; data simulată +85 zile, memento și escaladare | Omul aprobă. Sistemul ține minte termenul și escaladează la timp. |
| 1:40–1:50 | Trei capcane în 10 secunde: Cluj → Iași, ordin din cont curent, telefon străin + RO | Fără alarme false, fiecare cu explicația de ce nu e indiciu. |
| 1:50–2:10 | PDF cu text alb ascuns; semnal roșu de la scut | Un document poate încerca să manipuleze modelul. Scutul scoate textul ascuns înainte ca modelul să-l vadă. |
| 2:10–2:30 | Diagrama: cod / AI / om; jurnalul, „Tamper”, intrarea ruptă | Codul decide, AI-ul explică, omul aprobă. Orice modificare a jurnalului se vede. |
| 2:30–2:50 | `/metrics` | Cifrele reale pe cele 18 cazuri-etalon și cei 50 de clienți sintetici: prinse, ratate, alarme false. Ce nu prindem încă. |
| 2:50–3:00 | Logo | Fraza de final. |

## Fraza de final (variante)

- „Banca nu mai află la final de an că a raportat greșit. Află în ziua în care se schimbă adresa.”
- „Fiecare decizie are o regulă, o pagină din ghid și un om care a aprobat-o.”

## Ce ne diferențiază (pentru juriu și întrebări)

1. **Nucleul e după onboarding**: schimbările de situație, unde termenul de 90 de zile se pierde cel mai ușor.
2. **Deciziile sunt cod verificabil**, cu ID și pagina din Ghidul ANAF; AI-ul nu decide nimic.
3. **Capcanele sunt testate**: excepția conturilor de depozit, telefonul străin cu telefon RO, mutarea în țară, venitul din străinătate.
4. **Încredere**: scut împotriva textului ascuns, citate verificate, jurnal cu lanț de hash-uri, aprobare umană.
5. **Merge fără internet**: replay și mock, aceleași unelte reale.

## Întrebări probabile și răspunsuri

| Întrebare | Răspuns |
| --- | --- |
| „Raiffeisen are deja asta?” | Întrebarea pusă mentorului (P5). Răspunsul îl trecem aici înainte de filmare. |
| „Ce se întâmplă dacă modelul greșește?” | Modelul nu decide: regulile sunt cod testat, citatele sunt verificate, acțiunile cer aprobare. |
| „GDPR?” | Obligație legală (art. 6 alin. 1 lit. c, trimiterea exactă NEVERIFICAT); date personale mascate înainte de model; doar date sintetice. |
| „De ce us-west-2?” | Contul de workshop blochează Bedrock în UE; datele sunt sintetice. În producție: regiune UE sau on-prem. |
| „Ce nu prindeți?” | Text acoperit de forme în PDF; persoanele juridice; generarea F3000. Spus deschis. |

## De completat înainte de filmare

- [ ] Cifrele din `/metrics` (rulare reală, cu data rulării).
- [ ] Răspunsul mentorului.
- [ ] Cifrele ANAF, doar dacă le citim din raportul original (altfel scoatem).
- [ ] Textul final citit cu voce tare și cronometrat.
