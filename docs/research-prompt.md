# Research: ce problemă alegem la hackathon

> Folosit pentru alegerea challenge-ului (26 oct.). Am fost acceptați la **Raiffeisen, Operational Efficiency**; ideea aleasă e CRS Sentinel: vezi [`RESEARCH-CRS.md`](RESEARCH-CRS.md) și [`PLAYBOOK.md`](PLAYBOOK.md).

Ești un analist de startup-uri și strateg de hackathon. Lucrezi pentru echipa mea la un hackathon de AI agents (28–29 octombrie 2026). Vreau să găsim **problema cea mai valoroasă de rezolvat** din cele 4 challenge-uri: una reală și frustrantă, pentru care un client concret ar plăti, și pe care o putem demonstra convingător în 24 de ore, altfel decât celelalte echipe.

Răspunde în **română**. Lucrează în ordinea fazelor de mai jos. Nu-mi pune întrebări decât dacă ești blocat; altfel notează-ți presupunerile și mergi mai departe.

---

## Challenge-urile

Textul oficial:

1. **Raiffeisen Bank, Scaling & Growth:** *"Explore how AI agents can help customers navigate financial choices and services."*
2. **Raiffeisen Bank, Operational Efficiency:** *"Explore how AI agents can reduce repetitive work involving documents, data and verification."*
3. **Stripe, Financial Confidence:** *"Create experiences that help people feel more confident about saving and preparing for retirement."*
4. **QA DNA, AI Agent Arena:** *"Explore how AI agents can challenge ideas and test each other's reasoning."*

Fiecare challenge are premiu de **2.000 €** pentru echipa câștigătoare, plus beneficii de la parteneri. Preferințele pentru challenge se trimit până **luni, 26 octombrie, 23:59**, deci prioritizează: dacă timpul e scurt, livrează mai întâi rezumatul executiv și matricea de scor (faza 3), apoi detaliile.

Observații de care să ții cont:
- La Stripe, tema e **economisirea și pensia** (persoane fizice), nu plățile. Gândește și cine plătește într-un model real: angajatorul (beneficii pentru angajați), un fond de pensii Pilon II/III, o bancă, o platformă fintech sau utilizatorul. Context românesc: Pilonul II și III, pensiile private facultative, educația financiară scăzută, inflația, dependența de pensia de stat.
- La QA DNA, tema e despre agenți care **contestă idei și își testează reciproc raționamentul** (red teaming, verificare, dezbatere, evaluare). Caută cine are nevoie de asta și ar plăti: echipe de QA, companii care pun LLM-uri în producție (evaluări, halucinații), consultanți, investitori care verifică pitch-uri, compliance etc.
- La Raiffeisen Growth, atenție la granița dintre informare și **consultanță financiară reglementată** (MiFID II, distribuția de asigurări): ce poate face legal un agent pentru clienți.

## Contextul nostru

- **Format:** kickoff miercuri 28 oct, ora 09:00; predare joi 29 oct, ora 11:00. Mentori de business și checkpoint-uri cu juriul pe parcurs; finala pe scenă joi la 17:00.
- **Ce predăm:** codul sursă, o **demonstrație practică a agentului** și un **video de maximum 3 minute** (problema, ce e unic, arhitectura).
- **Echipa:** 4 oameni obișnuiți să lucreze cu AI zilnic; 2 știu C#.
  - **Unelte:** fiecare are Claude Pro și Gemini Pro, unul are și Codex Pro. Lucrăm cu Claude Code / aplicația Claude, T3 Code și Antigravity, deci putem construi repede și în paralel.
  - **Securitate:** un coleg lucrează în **cybersecurity**, deci putem face credibil partea de securitate: prompt injection, fraudă, protecția datelor, threat model.
  - **Server propriu:** același coleg are un server pe care poate rula **modele locale** (open-source, fără ca datele să plece în cloud). E un argument puternic pentru bănci și date sensibile: „merge și on-premise, pe serverele clientului”.
  - **Credite:** Featherless (modele open-source, API compatibil OpenAI) și Lovable (frontend rapid).
  
  Ține cont de aceste avantaje la scorul de fezabilitate și diferențiere: ce idei devin posibile sau mai credibile datorită lor?
- **Ce avem deja gata** (template .NET 10 / Blazor):
  - agenți cu unelte, cu fiecare pas vizibil live;
  - aprobare umană înainte de acțiuni;
  - mascare automată a datelor personale (CNP, IBAN, card, email, telefon) înainte de model;
  - citire de PDF-uri și poze, inclusiv din camera telefonului;
  - dezbatere multi-agent cu un judecător;
  - calculatoare financiare testate (credit, economii, IBAN);
  - audit trail, API HTTP, aplicație instalabilă pe telefon (PWA);
  - fallback pe răspunsuri înregistrate dacă pică modelul.
  
  Deci partea tehnică de bază nu e riscul: **riscul e să alegem o problemă banală sau un caz de business slab**.
- **Lecția de la hackathon-ul trecut:** am pierdut pe partea de business: ROI neclar, întrebări despre GDPR/NDA la care nu aveam răspuns, pitch nepregătit. De data asta, cazul de business trebuie să fie cel mai puternic punct.

---

## Faza 1: Cele mai frustrante probleme, pentru fiecare challenge

Pentru fiecare challenge, găsește **minimum 5 probleme reale**, cu surse. Caută în primul rând:
- **Vocea clienților:**
  - recenziile aplicațiilor (Raiffeisen Smart Mobile, aplicațiile băncilor concurente, Stripe Dashboard) în Google Play și App Store;
  - Trustpilot, G2, Capterra;
  - Reddit (r/Romania, r/stripe, r/smallbusiness, r/QualityAssurance, r/softwaretesting);
  - forumuri românești, plângeri ANPC/ANSPDCP;
  - Stripe Community și GitHub issues.
- **Vocea angajaților și a industriei:**
  - rapoarte McKinsey, BCG, EBA, ECB, BNR;
  - State of Testing, World Quality Report;
  - Glassdoor (ce urăsc angajații băncilor la munca lor);
  - LinkedIn posts ale oamenilor din operațiuni, compliance, QA.
- **Sponsorii înșiși:**
  - strategia publică Raiffeisen România și Raiffeisen Bank International (rapoarte anuale, comunicate, interviuri);
  - ce lansează Stripe în 2025–2026;
  - cine e QA DNA, ce vinde și cui.
  
  Sponsorul e și în juriu, și primul client posibil: ce problemă *l-ar durea pe el*?
- **„De ce acum”:** reglementări și schimbări care creează durere nouă în 2025–2026, de exemplu:
  - Verification of Payee și Instant Payments Regulation;
  - PSD3/PSR, DORA, EU AI Act, European Accessibility Act;
  - e-Factura/SAF-T în România;
  - fraudele cu plăți instant și social engineering.
  
  Verifică ce se aplică și cu ce termene.

Pentru fiecare problemă: **cine suferă, cât de des, cât costă** (timp, bani, clienți pierduți; cu cifre și sursă), cum o rezolvă oamenii azi (workaround-ul) și un **citat real** al cuiva care se plânge.

## Faza 2: Clientul care plătește

Pentru fiecare problemă (sau doar pentru cele mai bune 3–4 ale fiecărui challenge), spune:
- **Clientul plătitor vs. utilizatorul:** cine semnează contractul sau plătește abonamentul și cine folosește produsul. De exemplu: șeful de operațiuni plătește, analistul folosește.
- **Job-to-be-done:** ce încearcă de fapt să obțină.
- **Dovezi că ar plăti:** există concurenți care încasează bani pentru asta? Bugete, prețuri publice, runde de finanțare în domeniu?
- **Ipoteză de preț și model de business** (per utilizator, per dosar, procent din economii etc.).
- **Dimensiunea pieței, estimată grosier:** România, apoi UE. Arată calculul.
- **Concurenți și golul lor:** ce fac prost, ce nu acoperă.
- **Cum ar arăta ca startup real:** primii 10 clienți, cum ajungi la ei.

## Faza 3: Comparație între toate problemele

Fă o **matrice de scor** pentru toate problemele candidate, din toate challenge-urile, cu criteriile de mai jos (scor 1–5 și ponderi propuse de tine, justificate):

| Criteriu | Ce înseamnă |
| --- | --- |
| Durere | Cât de frustrant, frecvent și costisitor e |
| Disponibilitate de plată | Dovezi că cineva plătește deja pentru ceva similar |
| Potrivire cu sponsorul | Raiffeisen/Stripe/QA DNA ar vrea să adopte asta mâine |
| Demonstrabil în 24h | Un agent care face ceva vizibil, cu un moment „wow” pe scenă, pe date sintetice |
| Diferențiere | Cât de diferit e de ce vor face celelalte echipe |
| ROI demonstrabil | Putem arăta un calcul simplu și credibil în pitch |
| Fezabilitate și reglementare | GDPR, AI Act, secret bancar: avem un răspuns bun (ex.: mascare de date, om în buclă, model care poate rula on-premise) |

Înainte de matrice, listează explicit **ce vor construi 80% din echipe** pentru fiecare challenge, de exemplu:
- chatbot de FAQ pentru clienți;
- „OCR + LLM” pe documente;
- aplicație de buget cu categorisirea cheltuielilor;
- doi agenți care „discută”.

Penalizează ideile care seamănă cu acestea, dacă nu au un unghi clar diferit.

## Faza 4: Top 3 concepte, detaliate

Pentru cele mai bune 3 probleme (pot fi din challenge-uri diferite):
1. **Pitch într-o propoziție** și numele produsului.
2. **Clientul exact** (persona, rol, companie tip) și **problema** în cuvintele lui.
3. **Momentul „wow” din demo:** ce vede juriul în primele 30 de secunde.
4. **Ce face agentul:**
   - uneltele lui (ce date citește, ce calculează, ce propune);
   - unde aprobă omul;
   - ce rămâne simulat și ce e real;
   - de ce e nevoie de un *agent* și nu doar de un formular.
5. **Calculul ROI** cu cifre și surse (ex.: minute economisite × volum × cost orar), plus ce ar plăti clientul pe an.
6. **De ce noi / de ce acum / de ce nu există deja.**
7. **Riscuri** (tehnice, de business, de reglementare) și cum le contracarăm.
8. **Povestea pentru video-ul de 3 minute**, pe secunde: problemă → soluție → demo → arhitectură → business.
9. **3–5 întrebări** pe care să le punem mentorilor sau sponsorului la kickoff, ca să validăm repede ipoteza.

## Faza 5: Avocatul diavolului

Joacă rolul unui jurat sceptic de la Raiffeisen, Stripe sau QA DNA și atacă recomandarea numărul 1 cu cele mai dure 10 întrebări (date, securitate, „există deja”, „cine plătește”, „de ce ar cumpăra o bancă de la un startup”). Dă și răspunsul cel mai bun la fiecare. Dacă recomandarea nu rezistă, spune-o și treci la a doua.

---

## Cum să lucrezi

- **Folosește skill-urile și uneltele disponibile acolo unde ajută cel mai mult** (dacă le ai):
  - un skill de deep research / web search pentru faza 1 (multe căutări în paralel, surse primare);
  - un skill de competitive brief / analiză de concurență pentru faza 2;
  - user research / research synthesis pentru gruparea plângerilor în teme (faza 1);
  - analiză de date pentru matricea de scor (faza 3);
  - brainstorming pentru conceptele din faza 4;
  - grilling / stress-test pentru faza 5;
  - un skill sau tool de documente pentru raportul final.
- **Surse:** dă link la fiecare afirmație importantă. Separă clar **fapte (cu sursă)**, **estimări (cu calcul)** și **presupuneri**. Dă un nivel de încredere (mare/mediu/mic) cifrelor-cheie. Nu inventa statistici: dacă nu găsești o cifră, spune asta și estimează transparent.
- **Context local:** prioritizează România și UE (secret bancar, GDPR, BNR, ANPC, comportamentul clienților români), apoi exemple globale.
- **Ce poate rula în paralel:** echipa poate rula același prompt și în Gemini Deep Research. Dacă e util, propune ce să verifice fiecare instrument (de exemplu, unul vocea clienților, altul piața și concurenții), ca să comparăm rezultatele.
- **Obiectivul e câștigul la hackathon,** nu un plan de afaceri perfect. Contează durerea reală, diferențierea și un demo care convinge în 3 minute.

## Livrabilul final

1. **Rezumat executiv** (maximum 10 rânduri): problema recomandată, clientul, de ce câștigăm cu ea.
2. Tabelele din fazele 1–3.
3. Fișele celor 3 concepte (faza 4) și avocatul diavolului (faza 5).
4. **Recomandarea finală și planul B:** ce alegem dacă la kickoff aflăm că challenge-ul e formulat altfel.
5. Lista de surse.

Dacă poți, pune raportul într-un document pe care să-l pot trimite echipei.
