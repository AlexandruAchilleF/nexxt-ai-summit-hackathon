namespace HackathonStarter.Services;

// Romanian UI text for Features/Audit (hash-chained log) and /privacy. Owner: R3. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoAudit(Dictionary<string, string> ro)
    {
        ro["Actor"] = "Actor";
        ro["after a break"] = "după o rupere";
        ro["Audit log (hash chain)"] = "Jurnalul (lanț de hash-uri)";
        ro["broken here"] = "rupt aici";
        ro["Case"] = "Caz";
        ro["Case evidence"] = "Dovezile cazului";
        ro["Chain broken at entry #{0}"] = "Lanț rupt la intrarea #{0}";
        ro["Chain intact: {0} entries checked"] = "Lanț intact: {0} intrări verificate";
        ro["CNPs, IBANs, card numbers, emails and phone numbers (Romanian ones, and foreign ones written with the country code after + or 00) are replaced with placeholders such as [CNP_1] before any text leaves the app, and restored only in the answer shown to you. Tools receive the real values; the model never does."] = "CNP-urile, IBAN-urile, numerele de card, emailurile și numerele de telefon (cele românești și cele străine scrise cu indicativul țării după + sau 00) sunt înlocuite cu marcaje precum [CNP_1] înainte ca textul să iasă din aplicație și refăcute doar în răspunsul afișat ție. Uneltele primesc valorile reale; modelul, niciodată.";
        ro["Demo only: the payload is changed directly in the database, without recomputing its hash. Verify then finds this entry."] = "Doar pentru demo: conținutul se modifică direct în baza de date, fără să i se recalculeze hash-ul. Verificarea găsește apoi exact această intrare.";
        ro["Entry #{0} was edited outside the application"] = "Intrarea #{0} a fost modificată în afara aplicației";
        ro["Every change received, rule applied, case transition and human decision, with personal data masked like the run history. Each entry holds the SHA-256 hash of the previous one, so an edited entry is found on verification."] = "Fiecare modificare primită, fiecare regulă aplicată, fiecare tranziție de caz și fiecare decizie umană, cu datele personale mascate ca în istoricul rulărilor. Fiecare intrare conține hash-ul SHA-256 al celei anterioare, deci o intrare modificată e găsită la verificare.";
        ro["Every change, rule, case step, evidence check and approval is written here."] = "Aici se scrie fiecare schimbare, regulă, pas al cazului, verificare de dovadă și aprobare.";
        ro["Hash"] = "Hash";
        ro["In the demo the model runs on Amazon Bedrock in us-west-2 (United States), because the workshop account denies eu-central-1, and it sees synthetic data only. In production it would run in an EU region or on the bank's own servers."] = "În demo, modelul rulează pe Amazon Bedrock în us-west-2 (SUA), pentru că contul de workshop refuză eu-central-1, și vede doar date sintetice. În producție ar rula într-o regiune UE sau pe serverele băncii.";
        ro["Kind"] = "Tip";
        ro["No entries yet"] = "Încă nu există intrări";
        ro["No retention rule deletes clients, cases or the audit log: only agent runs expire. A bank must keep the records of its due diligence and reporting procedures, and the evidence it relied on, for 10 years from the reporting deadline of 15 May (Fiscal Procedure Code, Art. 62(11); FATCA records too)."] = "Nicio regulă de retenție nu șterge clienții, cazurile sau jurnalul: doar rulările agentului expiră. O bancă trebuie să păstreze înregistrările procedurilor de diligență fiscală și de raportare, și probele pe care s-a bazat, 10 ani de la termenul de raportare de 15 mai (Codul de procedură fiscală, art. 62 alin. (11); și pentru FATCA).";
        ro["Not masked: names, addresses, dates of birth, foreign TINs and foreign phone numbers written without the country code. This is why the demo uses synthetic data only."] = "Nu sunt mascate: numele, adresele, datele de naștere, TIN-urile străine și numerele de telefon străine scrise fără indicativul țării. De aceea demo-ul folosește doar date sintetice.";
        ro["Payload"] = "Conținut";
        ro["Quotes from the client's documents are stored with the case as written, not masked, and marked verified or not: they are part of the bank's case file. Agent runs, prompts and tool results are stored masked."] = "Citatele din documentele clientului se salvează la caz așa cum sunt scrise, nemascate, marcate ca verificate sau nu: fac parte din dosarul băncii. Rulările agentului, prompturile și rezultatele uneltelor se salvează mascate.";
        ro["Signals and test data"] = "Semnale și date de test";
        ro["Synthetic data only: clients, documents and TINs are generated from a fixed seed. Generated identity documents show SPECIMEN."] = "Doar date sintetice: clienții, documentele și TIN-urile sunt generate dintr-un seed fix. Actele de identitate generate poartă „SPECIMEN”.";
        ro["Tamper"] = "Falsifică";
        ro["Tamper with entry #{0}?"] = "Falsifici intrarea #{0}?";
        ro["Test data"] = "Datele de test";
        ro["The bank keeps a record that can credibly prove each confirmation, for audit purposes, and its electronic system must ensure that the information received is the information sent (ANAF guide, p. 34-35). Each entry is chained to the previous one with SHA-256: a change breaks the chain exactly there."] = "Banca păstrează o evidență care să poată demonstra, în mod credibil, fiecare confirmare, în scopuri de audit, iar sistemul electronic trebuie să asigure faptul că informațiile primite sunt informațiile trimise (ghidul ANAF, p. 34-35). Fiecare intrare e legată de cea anterioară prin SHA-256: o modificare rupe lanțul exact acolo.";
        ro["TIN on Europa (European Commission), for EU states only: the app sends the country code and the TIN. The answer says whether the structure and the syntax are correct (validStructure, validSyntax), not whether the TIN was issued, so the app only says 'format valid'. If the service does not answer, a recorded answer to the same request is used, otherwise only the local format rules. Recordings keep the TIN as sent: in this demo every TIN is synthetic."] = "TIN on Europa (Comisia Europeană), doar pentru statele UE: aplicația trimite codul țării și TIN-ul. Răspunsul spune dacă structura și sintaxa sunt corecte (validStructure, validSyntax), nu dacă TIN-ul a fost emis, de aceea aplicația spune doar „format valid”. Dacă serviciul nu răspunde, se folosește un răspuns înregistrat la aceeași cerere; altfel, doar regulile locale de format. Înregistrările păstrează TIN-ul așa cum a fost trimis: în demo, toate TIN-urile sunt sintetice.";
        ro["Verify"] = "Verifică";
        ro["When"] = "Când";
    }
}
