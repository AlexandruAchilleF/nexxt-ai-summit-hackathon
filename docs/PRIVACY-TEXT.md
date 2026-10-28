# Textul pentru `/privacy` (RO/EN)

Textul final al paginii `/privacy` (`Components/Pages/Privacy.razor`), pregătit ca document, ca R3 să-l aplice pe
măsură ce intră pe `main` codul descris, fără să mai caute surse. Secțiunile urmează ordinea paginii de pe `main`
(4c8addb; pagina e neschimbată de la PR #10, 76c9acc, care a adăugat secțiunea „Temeiul legal”). Stare la 28.10.2026.

Cum se citesc tabelele:

- **Cheie EN (@T)** e textul exact din `@T("...")`; **RO (Lang.Ro.cs)** e valoarea din dicționar (cheile existente
  stau în `Lang.Ro.cs`, cele noi intră în `Lang.Ro.Audit.cs`, vezi secțiunea 8).
- **Stare:** „cheie existentă” (e deja în `Services/Lang.Ro.cs` cu exact acest RO), „cheie nouă” sau „acum Lang.Pick (din
  #10)” (textul e scris acum cu `Lang.Pick(ro, en)`; coloanele dau cele două argumente).
- „după R1”, „după R2”, „după R3”, „după R4”, „după R5”, „după #5”: textul descrie cod care nu e încă pe `main`. Faza 0
  (#13, 874ba83) a adus doar contracte și înlocuitori, deci nu deblochează niciun rând. Nu se pune pe pagină înainte să
  existe codul. #9 (PDF-urile sintetice) e pe `main` din 4c8addb: rândul care depindea de el se poate pune acum.
- Fiecare trimitere la lege are citatul exact în secțiunea 7. Nu e un text juridic: e ce face aplicația, spus simplu.

## 0. Antetul paginii

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| Data and privacy | Date și confidențialitate | cheie existentă |
| What happens to personal data in this app, read from the live configuration: what reaches a model, what is stored, and for how long. | Ce se întâmplă cu datele personale în aplicație, citit din configurația curentă: ce ajunge la un model, ce se salvează și cât timp. | cheie existentă |

## 1. Ce ajunge la un model

Ce face codul acum (`Services/PiiVault.cs`, `Mask`): înlocuiește cinci tipuri de date, fiecare validat ca să nu
mascheze numere la întâmplare:

| Tip | Marcaj în rulare | Cum îl recunoaște |
| --- | --- | --- |
| E-mail | `[EMAIL_1]` | expresie regulată |
| IBAN, din orice țară | `[IBAN_1]` | cifra de control mod 97 (`Iban.Validate`) |
| CNP | `[CNP_1]` | 13 cifre, cifra de control |
| Card | `[CARD_1]` | 13–19 cifre, algoritmul Luhn |
| Telefon **românesc** | `[PHONE_1]` | prefix `+40`, `0040` sau `0`, apoi 2, 3 sau 7 și 8 cifre |

Nu sunt mascate: numele, adresele, datele de naștere, TIN-urile străine și **numerele de telefon străine**. Am verificat
expresia `PhoneRegex` pe `+49 151 23456789`, `+4915123456789`, `+43 664 1234567` și `+1 212 555 0100`: niciuna nu e
recunoscută. Numerele E.164 străine din `SyntheticData.PhoneE164` (PR #8: DE, AT, IT, ES, FR, US) ajung deci la model
în clar; cele românești (`+407…`) sunt mascate. Textul de mai jos spune asta. Dacă R3 extinde `PiiVault` la numerele
E.164 străine (PLAN 13.1 presupune deja că modelul vede `[PHONE-1]`, nu `+49 …`; în rulare marcajul e `[PHONE_1]`,
`[PHONE-1]` e forma de stocare), se scot „numerele de telefon străine” din cheia „Not masked” și „Romanian phone
numbers” devine „phone numbers”.

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| What reaches a model | Ce ajunge la un model | cheie existentă |
| CNPs, IBANs, card numbers, emails and Romanian phone numbers are replaced with placeholders such as [CNP_1] before any text leaves the app, and restored only in the answer shown to you. Tools receive the real values; the model never does. | CNP-urile, IBAN-urile, numerele de card, emailurile și numerele de telefon românești sunt înlocuite cu marcaje precum [CNP_1] înainte ca textul să iasă din aplicație și refăcute doar în răspunsul afișat ție. Uneltele primesc valorile reale; modelul, niciodată. | cheie nouă (înlocuiește cheia existentă „CNPs, IBANs, card numbers, emails and phone numbers are replaced…”) |
| Not masked: names, addresses, dates of birth, foreign TINs and foreign phone numbers. This is why the demo uses synthetic data only. | Nu sunt mascate: numele, adresele, datele de naștere, TIN-urile străine și numerele de telefon străine. De aceea demo-ul folosește doar date sintetice. | cheie nouă |
| Masking is off | Mascarea e oprită | cheie existentă |
| text is sent to the model as typed. | textul ajunge la model exact cum a fost scris. | cheie existentă |
| From uploaded PDFs, only the visible text reaches the model: white, tiny, invisible or off-page text is set aside and shown to the operator. Text covered by a shape is not detected yet. | Din PDF-urile încărcate ajunge la model doar textul vizibil: textul alb, microscopic, invizibil sau din afara paginii e pus deoparte și arătat operatorului. Textul acoperit de o formă nu e detectat încă. | cheie nouă · după R3 (scutul, PLAN 10.1) |
| Next to a masked value, tools add the country it belongs to (for example ibanCountry: DE), so the model can explain an indicium without seeing the number. | Lângă o valoare mascată, uneltele pun țara de care ține (de exemplu ibanCountry: DE), ca modelul să poată explica un indiciu fără să vadă numărul. | cheie nouă · după R4 (uneltele cu `ibanCountry`, PLAN 13.1) |
| No model is configured: agents replay recorded runs or run scripted mocks, so nothing leaves the app. | Niciun model nu e configurat: agenții redau rulări înregistrate sau simulări scriptate, deci nimic nu iese din aplicație. | cheie existentă |
| Role | Rol | cheie existentă |
| Model | Model | cheie existentă |
| Runs on | Rulează pe | cheie existentă |
| your infrastructure | infrastructura voastră | cheie existentă |
| external provider | furnizor extern | cheie existentă |
| In the demo the model runs on Amazon Bedrock in us-west-2 (United States), because the workshop account denies eu-central-1, and it sees synthetic data only. In production it would run in an EU region or on the bank's own servers. | În demo, modelul rulează pe Amazon Bedrock în us-west-2 (SUA), pentru că contul de workshop refuză eu-central-1, și vede doar date sintetice. În producție ar rula într-o regiune UE sau pe serverele băncii. | cheie nouă · după #5 (Bedrock); afișată doar când un model are `Provider = Bedrock` |
| Images are sent to vision-capable models as they are: masking applies to text only. | Imaginile sunt trimise modelelor cu vedere așa cum sunt: mascarea se aplică doar textului. | cheie existentă |

Note:

- Coloana „Runs on” vine din `DataFlows.Models`. Pe `main`, un model Bedrock fără `Endpoint` ar apărea ca
  `api.openai.com`; PR #5 adaugă gazda `bedrock-runtime.<regiune>.amazonaws.com`. Rândul Bedrock are sens doar după #5.
- Exemplul cu țara folosește `ibanCountry`, nu `phoneCountry`: telefonul străin nu e mascat azi, deci nu are o valoare
  mascată lângă care să stea țara.

## 2. Ce se salvează

Ce face codul acum: `RunRedaction.Redact` copiază pașii rulării și îi maschează cu `PiiVault.ForStorage()` (marcaje
`[IBAN-1]`, fără cale înapoi). Argumentele unei aprobări în așteptare se păstrează criptat în `AgentStep.SealedInput`
(`PiiSealer`, ASP.NET Core Data Protection) și se șterg când aprobarea e decisă. Înregistrările (`Recordings/`,
`Recordings/targets/`) se maschează la fel, dacă `Privacy:RedactRecordings = true` (valoarea implicită).

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| What is stored | Ce se salvează | cheie existentă |
| Run history (audit trail) | Istoricul rulărilor (audit) | cheie existentă |
| Personal data replaced with one-way placeholders such as [IBAN-1] before saving. They can't be turned back into the original values. | Datele personale sunt înlocuite înainte de salvare cu marcaje ireversibile precum [IBAN-1]. Nu pot fi transformate înapoi în valorile originale. | cheie existentă |
| Actions awaiting approval | Acțiuni care așteaptă aprobare | cheie existentă |
| Their arguments are kept encrypted (ASP.NET Core Data Protection) until a person approves or rejects them, then deleted. | Argumentele lor sunt păstrate criptat (ASP.NET Core Data Protection) până când un om le aprobă sau le respinge, apoi sunt șterse. | cheie existentă |
| The encryption keys are stored in the same database: enough for a demo; in production they belong in a key vault. | Cheile de criptare stau în aceeași bază de date: suficient pentru demo; în producție, locul lor e într-un seif de chei (key vault). | cheie existentă |
| Recorded runs (demo fallback) | Rulări înregistrate (rezerva pentru demo) | cheie existentă |
| Masked like the run history. | Mascate la fel ca istoricul rulărilor. | cheie existentă |
| Not masked (Privacy:RedactRecordings = false): use synthetic data only. | Nemascate (Privacy:RedactRecordings = false): folosiți doar date sintetice. | cheie existentă |
| Exports (tests, reports) | Exporturi (teste, rapoarte) | cheie existentă |
| Built from the masked history and downloadable only after a person approves them. | Construite din istoricul mascat și descărcabile doar după ce un om le aprobă. | cheie existentă |
| Uploaded documents | Documente încărcate | cheie existentă |
| Kept in memory for the current conversation only. Passages an agent reads from them become part of the run history, masked like the rest. | Păstrate în memorie doar pe durata conversației. Pasajele citite de agent devin parte din istoricul rulării, mascate ca restul. | cheie existentă |
| Audit log (hash chain) | Jurnalul (lanț de hash-uri) | cheie nouă · după R3 (`AuditLog` cu lanț de hash-uri, PLAN 12) |
| Every change received, rule applied, case transition and human decision, with personal data masked like the run history. Each entry holds the SHA-256 hash of the previous one, so an edited entry is found on verification. | Fiecare modificare primită, fiecare regulă aplicată, fiecare tranziție de caz și fiecare decizie umană, cu datele personale mascate ca în istoricul rulărilor. Fiecare intrare conține hash-ul SHA-256 al celei anterioare, deci o intrare modificată e găsită la verificare. | cheie nouă · după R3 (`AuditLog` cu lanț de hash-uri, PLAN 12) |
| Clients and cases | Clienți și cazuri | cheie nouă · după R5 (`ClientSeed`) și R2 (`CaseService`) |
| Synthetic clients and their cases (changes, evidence, requests, deadlines), stored as entered, not masked, like any bank record. | Clienții sintetici și cazurile lor (modificări, dovezi, cereri, termene), păstrate așa cum au fost introduse, nemascate, ca orice evidență a băncii. | cheie nouă · după R5 (`ClientSeed`) și R2 (`CaseService`) |

Rândul „Clients and cases” spune „nemascate”: PLAN 13.1 spune că argumentele uneltelor ajung la cod demascate
(inclusiv citatele pentru `recordEvidence`). Pe `main`, tabelele (`Clients`, `Cases`, `Evidence`, `AuditEntries` etc.)
există de la Faza 0, dar sunt goale: `ClientSeed` nu întoarce nimic, `CaseService` nu salvează nimic, `AuditLog` nu
salvează nimic. R3 confirmă înainte să-l publice (vezi HANDOFF).

## 3. Cât timp

Ce face codul acum: `RetentionService` șterge din `AgentRuns` rulările mai vechi de `Privacy:RetentionDays`, la pornire
și apoi din oră în oră. Valoarea implicită e **30 de zile**, atât în `appsettings.json` (`"RetentionDays": 30`), cât și
în `PrivacyOptions`. `RetentionService` nu atinge alte tabele: clienții, cazurile și jurnalul nu se șterg automat.

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| How long | Cât timp | cheie existentă |
| Agent runs are deleted automatically after | Rulările se șterg automat după | cheie existentă |
| {0} days | {0} zile | cheie existentă |
| checked every hour. | verificat din oră în oră. | cheie existentă |
| Automatic deletion is off (Privacy:RetentionDays = 0): runs are kept until an admin resets the data. | Ștergerea automată e oprită (Privacy:RetentionDays = 0): rulările rămân până când un administrator resetează datele. | cheie existentă |
| Last check: {0}, {1} run(s) deleted. | Ultima verificare: {0}, {1} rulări șterse. | cheie existentă |
| No retention rule deletes clients, cases or the audit log: only agent runs expire. A bank must keep the records of its due diligence and reporting procedures, and the evidence it relied on, for 10 years from the reporting deadline of 15 May (Fiscal Procedure Code, Art. 62(11); FATCA records too). | Nicio regulă de retenție nu șterge clienții, cazurile sau jurnalul: doar rulările agentului expiră. O bancă trebuie să păstreze înregistrările procedurilor de diligență fiscală și de raportare, și probele pe care s-a bazat, 10 ani de la termenul de raportare de 15 mai (Codul de procedură fiscală, art. 62 alin. (11); și pentru FATCA). | cheie nouă · după R2 și R3 (cazurile și jurnalul chiar se salvează) |

Aplicația nu numără cei 10 ani și nu șterge nimic după ei. Textul spune doar ce cere legea băncii. Termenul de 15 mai
e cel din art. 62 alin. (7), pentru informațiile anului precedent. În Development, `Database:RecreateOnStartup = true`
recreează baza la fiecare pornire; în `appsettings.json` e `false`.

## 4. Temeiul legal

Secțiunea din PR #10, păstrată în aceeași ordine. Am schimbat doar trimiterile pe care sursa primară le arată altfel
(detalii în 7.4). Rândul „Informarea clientului” și propoziția despre expirarea automată sunt noi.

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| Legal basis | Temeiul legal | acum Lang.Pick (din #10) |
| Basis | Temei | acum Lang.Pick (din #10) |
| Legal obligation (Art. 6(1)(c) GDPR): the Romanian Fiscal Procedure Code (Law 207/2015) requires the bank to apply the due diligence procedures in Annexes 1 and 2 (Art. 291(4)) and to report the accounts to ANAF every year (Art. 62(1)); for FATCA, the procedures of the FATCA Agreement (Art. 62(3), Law 233/2015). | Obligație legală (art. 6 alin. (1) lit. (c) GDPR): Codul de procedură fiscală (Legea 207/2015) cere băncii să aplice procedurile de diligență fiscală din anexele nr. 1 și 2 (art. 291 alin. (4)) și să raporteze anual conturile la ANAF (art. 62 alin. (1)); pentru FATCA, procedurile din Acordul FATCA (art. 62 alin. (3), Legea 233/2015). | acum Lang.Pick (din #10) · text schimbat |
| Automated decisions | Decizii automate | acum Lang.Pick (din #10) |
| No decision is based solely on automated processing (Art. 22(1) GDPR): code applies the rules from the ANAF guide, the model decides nothing, and a person approves the request to the client and the closing of the case. | Nicio decizie nu se bazează exclusiv pe prelucrarea automată (art. 22 alin. (1) GDPR): codul aplică regulile din ghidul ANAF, modelul nu decide nimic, iar un om aprobă cererea către client și închiderea cazului. | acum Lang.Pick (din #10) · text schimbat |
| One step is automatic: when the deadline passes, the case becomes expired with the consequence the guide sets (p. 29, p. 35–36). | Un singur pas e automat: când trece termenul, cazul devine expirat, cu consecința stabilită de ghid (p. 29, p. 35–36). | cheie nouă · după R2 (`DeadlineWatcher`) și după decizia din HANDOFF (art. 22) |
| Masking | Mascarea | acum Lang.Pick (din #10) |
| Serves data minimisation (Art. 5(1)(c) GDPR) and security of processing through pseudonymisation and encryption (Art. 32(1)(a) GDPR). | Ține de reducerea la minimum a datelor (art. 5 alin. (1) lit. (c) GDPR) și de securitatea prelucrării, prin pseudonimizare și criptare (art. 32 alin. (1) lit. (a) GDPR). | acum Lang.Pick (din #10) · text schimbat |
| Informing the client | Informarea clientului | cheie nouă |
| The bank tells every reportable person that their data are processed under the Fiscal Procedure Code and the GDPR, at the latest when the account is opened (Art. 62(10)). This app does not send that notice. | Banca informează fiecare persoană raportabilă că datele ei sunt prelucrate conform Codului de procedură fiscală și GDPR, cel târziu la deschiderea contului (art. 62 alin. (10)). Aplicația nu trimite această informare. | cheie nouă |

Ce s-a schimbat față de #10 și de ce:

- **Temei:** #10 spunea că legea „cere băncii să identifice rezidența fiscală”, fără articol pe pagină (în PLAN și README:
  art. 62 alin. (7), art. 291 alin. (4) și Anexa nr. 1, după ghid p. 4). Art. 62 alin. (7) e termenul de 15 mai, nu
  obligația. Obligația de diligență e art. 291 alin. (4), care trimite la anexele nr. 1 **și 2**. Raportarea e art. 62
  alin. (1), iar FATCA e art. 62 alin. (3).
- **Decizii automate:** #10 spunea „Art. 22 GDPR nu se aplică”. Art. 22 alin. (1) dă un drept („de a nu face obiectul
  unei decizii bazate exclusiv pe prelucrarea automată”), deci formularea corectă e cea de fapt: nu avem astfel de
  decizii. Nu invocăm excepțiile din alin. (2).
- **Mascarea:** termenul oficial în română e „reducerea la minimum a datelor” (art. 5 alin. (1) lit. (c)), nu
  „minimizarea datelor”. La art. 32, litera potrivită e alin. (1) lit. (a), „pseudonimizarea și criptarea datelor”:
  marcajele din rulare sunt pseudonimizare, `SealedInput` e criptare.

## 5. Sisteme externe

Ce face codul acum: `TargetClient` trimite mesajele **nemascate** („a target is often the system being tested”), cu
timeout, iar dacă ținta nu răspunde întoarce răspunsul înregistrat pentru aceeași conversație. `TargetRecordings`
salvează doar cu `AI:Record = true` în Development și maschează cu `PiiVault`. Un TIN străin nu e un tip `PiiVault`,
deci rămâne în clar în `Recordings/targets/`.

TIN on Europa nu e încă pe `main` (Faza 0 are doar contractul `ITinValidator`; R3 adaugă apelul). PLAN 11 și
`docs/reguli-verificate.md` (apel real): răspunsul are doar `validStructure` și `validSyntax`; acoperă doar statele UE.

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| External systems this app talks to | Sisteme externe cu care vorbește aplicația | cheie existentă |
| Messages to these targets are sent as written, because a target is often the system being tested for leaks. Use synthetic data with them. | Mesajele către aceste ținte pleacă exact cum au fost scrise, pentru că ținta e adesea chiar sistemul testat pentru scurgeri de date. Folosiți date sintetice cu ele. | cheie existentă |
| not configured | neconfigurat | cheie existentă |
| TIN on Europa (European Commission), for EU states only: the app sends the country code and the TIN. The answer says whether the structure and the syntax are correct (validStructure, validSyntax), not whether the TIN was issued, so the app only says 'format valid'. If the service does not answer, a recorded answer to the same request is used, otherwise only the local format rules. Recordings keep the TIN as sent: in this demo every TIN is synthetic. | TIN on Europa (Comisia Europeană), doar pentru statele UE: aplicația trimite codul țării și TIN-ul. Răspunsul spune dacă structura și sintaxa sunt corecte (validStructure, validSyntax), nu dacă TIN-ul a fost emis, de aceea aplicația spune doar „format valid”. Dacă serviciul nu răspunde, se folosește un răspuns înregistrat la aceeași cerere; altfel, doar regulile locale de format. Înregistrările păstrează TIN-ul așa cum a fost trimis: în demo, toate TIN-urile sunt sintetice. | cheie nouă · după R3 (TIN on Europa prin `TargetClient`) |

Textul nu conține expresia interzisă din PLAN 24; spune doar „format valid”.

## 6. Semnale și date de test (secțiune nouă)

Secțiune nouă, propusă după „Temeiul legal” și înainte de „Sisteme externe”.

| Cheie EN (@T) | RO (Lang.Ro.cs) | Stare |
| --- | --- | --- |
| Signals and test data | Semnale și date de test | cheie nouă |
| Nationality | Naționalitatea | cheie nouă |
| Nationality is not a signal and opens no case: the CRS indicia in the ANAF guide (p. 29) do not include it. The only exception is US citizenship, a FATCA indicium (p. 31). | Naționalitatea nu e semnal și nu deschide niciun caz: indiciile CRS din ghidul ANAF (p. 29) nu o cuprind. Singura excepție e cetățenia SUA, indiciu FATCA (p. 31). | cheie nouă · după R1 (regula e în motor; azi `CrsRuleEngine` întoarce `NoCase`) |
| AI signals | Semnalele AI | cheie nouă |
| Context signals found by the model are shown to the operator as context only. They never change a decision and never open a case. | Semnalele de context găsite de model sunt arătate operatorului doar ca context. Nu schimbă nicio decizie și nu deschid niciun caz. | cheie nouă · după R4 (semnalele de context din agent) |
| Test data | Datele de test | cheie nouă |
| Synthetic data only: clients, documents and TINs are generated from a fixed seed. Generated identity documents show SPECIMEN. | Doar date sintetice: clienții, documentele și TIN-urile sunt generate dintr-un seed fix. Actele de identitate generate poartă „SPECIMEN”. | cheie nouă · se poate pune acum (#9, PDF-urile sintetice, e pe `main` din 4c8addb) |

## 7. Surse

Toate citatele de mai jos au fost copiate din sursă pe 28.10.2026.

### 7.1 GDPR: Regulamentul (UE) 2016/679

- EUR-Lex: https://eur-lex.europa.eu/legal-content/RO/TXT/HTML/?uri=CELEX:32016R0679 . De pe mașina noastră, EUR-Lex
  răspunde cu o provocare AWS WAF (HTTP 202, corp gol) la HTML, PDF și ELI.
- Textul l-am luat de la Oficiul pentru Publicații al UE, editorul Jurnalului Oficial:
  https://publications.europa.eu/resource/celex/32016R0679 (cu `Accept: application/xhtml+xml` și
  `Accept-Language: ron`). Răspunsul e documentul `JO L 119/1, 4.5.2016`, versiunea în română. Am comparat citatele și
  cu versiunea consolidată (https://publications.europa.eu/resource/celex/02016R0679-20160504, care include rectificarea
  JO L 127, 23.5.2018, p. 2), descărcată pe 28.10.2026 cu aceleași antete: textul celor șapte citate e identic. ELI:
  http://data.europa.eu/eli/reg/2016/679/oj .

| Articol | Citat exact |
| --- | --- |
| Art. 5 alin. (1) lit. (c) | „adecvate, relevante și limitate la ceea ce este necesar în raport cu scopurile în care sunt prelucrate („reducerea la minimum a datelor”);” |
| Art. 5 alin. (1) lit. (e) | „păstrate într-o formă care permite identificarea persoanelor vizate pe o perioadă care nu depășește perioada necesară îndeplinirii scopurilor în care sunt prelucrate datele; [...] („limitări legate de stocare”);” |
| Art. 6 alin. (1) lit. (c) | „prelucrarea este necesară în vederea îndeplinirii unei obligații legale care îi revine operatorului;” |
| Art. 6 alin. (3) | „Temeiul pentru prelucrarea menționată la alineatul (1) literele (c) și (e) trebuie să fie prevăzut în: (a) dreptul Uniunii; sau (b) dreptul intern care se aplică operatorului.” |
| Art. 22 alin. (1) | „Persoana vizată are dreptul de a nu face obiectul unei decizii bazate exclusiv pe prelucrarea automată, inclusiv crearea de profiluri, care produce efecte juridice care privesc persoana vizată sau o afectează în mod similar într-o măsură semnificativă.” |
| Art. 22 alin. (2) | „Alineatul (1) nu se aplică în cazul în care decizia: (a) este necesară pentru încheierea sau executarea unui contract [...]; (b) este autorizată prin dreptul Uniunii sau dreptul intern [...]; sau (c) are la bază consimțământul explicit al persoanei vizate.” Nu ne bazăm pe aceste excepții. |
| Art. 32 alin. (1) lit. (a) | „[...] operatorul și persoana împuternicită de acesta implementează măsuri tehnice și organizatorice adecvate în vederea asigurării unui nivel de securitate corespunzător acestui risc, incluzând printre altele, după caz: (a) pseudonimizarea și criptarea datelor cu caracter personal;” |

### 7.2 Codul de procedură fiscală (Legea 207/2015)

Forma consolidată la 08.08.2026: https://legislatie.just.ro/Public/DetaliiDocument/312603 . Art. 62 are forma dată
de OUG 102/2022 (art. I pct. 1; „la 30-06-2022, Articolul 62 [...] a fost modificat de Punctul 1, Articolul I din
ORDONANȚA DE URGENȚĂ nr. 102 din 29 iunie 2022”): https://legislatie.just.ro/Public/DetaliiDocument/256922 . Textul
alin. (11) apare la fel și în OUG 102/2022.

| Articol | Citat exact |
| --- | --- |
| Art. 62 alin. (1) | „În scopul realizării schimbului de informații în temeiul instrumentelor juridice de drept internațional la care România este parte și pentru îmbunătățirea conformării fiscale, instituțiile financiare raportoare au obligația să raporteze anual A.N.A.F. informații referitoare la conturile financiare.” |
| Art. 62 alin. (3) | „[...] în cazul schimbului de informații cu privire la contribuabilii nerezidenți, efectuat în temeiul Acordului dintre România și Statele Unite ale Americii pentru îmbunătățirea conformării fiscale internaționale și pentru implementarea FATCA, semnat la București la 28 mai 2015, ratificat prin Legea nr. 233/2015, denumit în continuare Acordul FATCA, instituțiile financiare raportoare raportează informațiile și aplică procedurile de conformare prevăzute de acest acord.” |
| Art. 62 alin. (7) | „Raportarea informațiilor prevăzute la alin. (1), inclusiv a celor prevăzute la art. 291 alin. (4), se face până la data de 15 mai inclusiv a anului calendaristic curent pentru informațiile aferente anului calendaristic precedent.” |
| Art. 62 alin. (10) | „Fără a aduce atingere dispozițiilor alin. (1) și (11), fiecare instituție financiară raportoare informează fiecare persoană care face obiectul raportării cu privire la faptul că informațiile care o vizează, prevăzute la alin. (1), sunt prelucrate în conformitate cu prevederile prezentei legi și ale Regulamentului (UE) 2016/679, cu modificările ulterioare, în timp util, dar nu mai târziu de momentul deschiderii contului.” |
| Art. 62 alin. (11) | „Instituțiile financiare raportoare au obligația de a păstra în format electronic și/sau letric toate înregistrările obținute prin aplicarea procedurilor de diligență fiscală și de raportare, a procedurilor speciale de diligență fiscală, a procedurilor suplimentare de raportare și de diligență fiscală pentru schimburile de informații referitoare la conturile financiare prevăzute în anexele nr. 1 și 2, precum și a oricăror probe pe care s-au bazat în vederea conformării cu prevederile stabilite în anexele nr. 1 și 2 pentru o perioadă de 10 ani de la împlinirea termenului prevăzut la alin. (7). Dispozițiile prezentului alineat se aplică în mod corespunzător și în cazul înregistrărilor obținute în temeiul Acordului FATCA.” |
| Art. 291 alin. (4) | „Instituțiile financiare raportoare sunt obligate să aplice procedurile de raportare și de diligență fiscală incluse în anexele nr. 1 și 2.” |

### 7.3 Ghidul ANAF DAC2/CRS/FATCA (29.12.2025)

| Pagina | Citat exact |
| --- | --- |
| p. 4 (`docs/ghid/pagini/p-004.md`) | „Legislația incidentă prevede obligația Instituțiilor Financiare Române de a identifica titularii de cont (atât persoane fizice cât și entități) care nu sunt rezidenți fiscali în România sau care sunt cetățeni americani și de a raporta către A.N.A.F. informații relevante cu privire la conturile financiare.” și „Prevederile referitoare la definițiile termenilor, condițiile și termenele de raportare a informațiilor cu privire la conturile financiare ale contribuabililor nerezidenți se regăsesc la art. 62, alin. (7), art. 291, alin. (4) și în Anexa nr. 1 din Legea nr. 207/2015 privind Codul de procedură fiscală, respectiv în cuprinsul Acordului dintre România și Statele Unite ale Americii [...] (Acordul FATCA).” |
| p. 29 (`p-029.md`) | Lista indiciilor CRS din căutarea electronică, lit. a)–f), începe cu „a) identificarea titularului de cont drept rezident al unei jurisdicții care face obiectul raportării;” și se termină cu adresa „post-restant” sau „în atenția”. Naționalitatea nu e în listă (confirmat și în `docs/reguli-verificate.md`, r. 63). |
| p. 31 (`p-031.md`) | Indiciile FATCA: „identificarea Titularului de Cont ca fiind cetățean sau rezident al Statelor Unite;” |

### 7.4 Ce am verificat din #10

| Afirmația din #10 | Rezultat |
| --- | --- |
| Art. 6 alin. (1) lit. (c) GDPR ca temei | VERIFICAT. Art. 6 alin. (3) cere ca obligația să fie în dreptul intern: e în Codul de procedură fiscală (art. 62 alin. (1), art. 291 alin. (4)). |
| Art. 22 GDPR „nu se aplică” | VERIFICAT pentru cerere și închidere (aprobate de om). Trecerea automată în `Expired` (PLAN 9.4, după R2) nu are aprobare umană: decizia e la om (vezi HANDOFF). |
| Art. 5 alin. (1) lit. (c) pentru mascare | VERIFICAT, trimitere potrivită. Termenul oficial: „reducerea la minimum a datelor”. |
| Art. 32 pentru mascare | VERIFICAT, trimitere potrivită; mai precis alin. (1) lit. (a), „pseudonimizarea și criptarea”. |
| „Legea 207/2015 (art. 62 alin. (7), art. 291 alin. (4), Anexa nr. 1) [...] așa cum le citează ghidul (p. 4)” | Citatul din ghid e exact: p. 4 numește aceste trei trimiteri. Dar ghidul le dă ca loc pentru „definițiile termenilor, condițiile și termenele de raportare”, nu ca temei al obligației. Art. 62 alin. (7) e termenul de 15 mai. Legea numește anexele nr. 1 **și 2** (art. 291 alin. (4)). Corectura propusă: art. 62 alin. (1) și (3), art. 291 alin. (4), anexele nr. 1 și 2. |
| Termenul de păstrare a dosarului (în PLAN și PLAYBOOK, NEVERIFICAT) | VERIFICAT: 10 ani de la termenul din art. 62 alin. (7), art. 62 alin. (11), aplicabil și la FATCA. |

## 8. Cum se aplică după Faza 0

Pași pentru R3 (`security-engineer`, proprietarul `/privacy`). Sunt propuneri; Tech Lead-ul decide.

**Înainte:** Faza 0 (#13) e pe `main` din 28.10.2026, dar are doar contracte și înlocuitori: `AuditLog` nu salvează
nimic, `CrsRuleEngine` întoarce `NoCase`, `CaseService` aruncă excepții, `ClientSeed` e gol, agentul nu are unelte.
Rândurile marcate „după R1”–„după R5” sau „după #5” intră doar când e pe `main` codul pe care îl descriu. #9 e pe `main`
(4c8addb), deci rândul „Test data” poate intra acum. Ce nu e gata rămâne afară, nu se scrie la viitor.

**În `Privacy.razor`, secțiune cu secțiune:**

1. **Antet:** nimic.
2. **Ce ajunge la un model:** în ramura `MaskPii`, cheia veche „CNPs, IBANs, card numbers, emails and phone numbers…”
   devine cheia nouă (cu „Romanian phone numbers”), urmată de `<span>` cu „Not masked: …”. Sub ea, propoziția despre
   scut (după R3) și cea despre țara derivată (după uneltele R4). Sub tabelul modelelor, propoziția Bedrock, afișată doar
   dacă `_models.Any(m => m.Provider.Equals("Bedrock", StringComparison.OrdinalIgnoreCase))` (după #5).
3. **Ce se salvează:** două rânduri noi la finalul tabelului: „Audit log (hash chain)” (după R3) și „Clients and cases”
   (după R5 și R2).
4. **Cât timp:** după `<span>`-urile existente, o propoziție nouă: „No retention rule deletes clients, cases or the audit
   log…” (după R2 și R3).
5. **Temeiul legal:** se înlocuiesc textele din rândurile „Temei”, „Decizii automate” și „Mascarea” cu cele din
   secțiunea 4 și se adaugă rândul „Informarea clientului”. Propoziția despre expirarea automată intră în „Decizii
   automate” după R2 (`DeadlineWatcher`) și după decizia omului din HANDOFF.
6. **Semnale și date de test:** un `MudPaper` nou, cu același tipar ca „Temeiul legal” (titlu + `MudSimpleTable` cu trei
   rânduri), pus după „Temeiul legal”. Azi poate intra doar „Test data”; „Nationality” după R1, „AI signals” după R4.
7. **Sisteme externe:** paragraful TIN on Europa, afișat doar dacă `_targets` conține targetul TIN on Europa (numele lui
   îl alege R3 în `Targets:`; nu e încă în `appsettings.json`).

**`Lang.Pick` sau `@T` la „Temeiul legal” (propunere, nu decizie):** recomand să rămână `Lang.Pick`. AGENTS.md (regula 8)
îl permite pentru texte lungi, iar trimiterile la lege se verifică mai ușor când RO și EN stau una lângă alta în pagină.
Dacă R3 preferă o pagină uniformă, textele din secțiunea 4 sunt gata ca chei `@T` (cu RO în dicționar), iar
`LangTests` le acoperă automat.

**În dicționar** (pe `main`, de la #13, `Lang.Ro.cs` e împărțit în `Lang.Ro.<Modul>.cs`; R3 poate edita doar
`Lang.Ro.Shield.cs`, `Lang.Ro.Tin.cs` și `Lang.Ro.Audit.cs`, după lista `edit:` din `docs/agents/security-engineer.md`):

- Cheile existente rămân în `Lang.Ro.cs` (textul template-ului). R3 nu editează `Lang.Ro.cs`: antetul lui (r. 3–5) spune
  că textul template-ului rămâne acolo și că fiecare modul CRS își scrie cheile în `Lang.Ro.<Modul>.cs`.
- Cheia veche de mascare rămâne în `Lang.Ro.cs`: o cheie nefolosită nu pică `LangTests`. O scoate Tech Lead-ul, după ce
  `grep` nu o mai găsește în `src/` (azi: `Privacy.razor:19` și definiția din `Lang.Ro.cs:84`).
- Cheile noi intră în `Lang.Ro.Audit.cs` (al lui R3, legat deja prin `AddRoAudit(d);`), sortate, în forma
  `ro["English"] = "Română";`; comentariul din antet devine „Features/Audit (hash-chained log) and /privacy”. Un
  `Lang.Ro.Privacy.cs` separat cere `AddRoPrivacy(d);` și declarația `static partial void` în `Lang.Ro.cs`: doar prin
  HANDOFF către Tech Lead.
- Ghilimelele românești „…” nu cer escape în C#. Cheile EN de aici folosesc apostroful ('format valid'), deci nici ele.

**Teste și verificări:**

- `dotnet build` cu 0 warnings.
- `dotnet test tests/HackathonStarter.Tests --filter LangTests`: trec `Every_literal_key_in_the_code_is_translated`,
  `Romanian_keys_are_not_defined_twice` și `Placeholders_match_between_languages` (pe `/privacy`, cheile cu `{0}` rămân
  „{0} days” și „Last check: {0}, {1} run(s) deleted.”; cheile noi nu au `{n}`).
- `dotnet test tests/HackathonStarter.E2E`: `AppTests` deschide `/privacy` și caută „Data and privacy”; titlul nu se schimbă.
- La 390px lățime, pe RO și pe EN: tabelele cu două coloane și texte lungi se rup pe rânduri, iar gazda Bedrock
  (`bedrock-runtime.us-west-2.amazonaws.com`, font `mono`) nu iese din pagină. Dacă iese, `overflow-wrap: anywhere` pe
  celula respectivă.
- O căutare pe pagina randată: niciun emoji, nicio apariție a expresiei interzise din PLAN 24 (doar „format valid”).
