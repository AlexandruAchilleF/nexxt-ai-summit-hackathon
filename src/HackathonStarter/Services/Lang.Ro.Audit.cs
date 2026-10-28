namespace HackathonStarter.Services;

// Romanian UI text for Features/Audit (hash-chained log). Owner: R3. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoAudit(Dictionary<string, string> ro)
    {
        ro["Actor"] = "Actor";
        ro["after a break"] = "după o rupere";
        ro["broken here"] = "rupt aici";
        ro["Case"] = "Caz";
        ro["Chain broken at entry #{0}"] = "Lanț rupt la intrarea #{0}";
        ro["Chain intact: {0} entries checked"] = "Lanț intact: {0} intrări verificate";
        ro["Demo only: the payload is changed directly in the database, without recomputing its hash. Verify then finds this entry."] = "Doar pentru demo: conținutul se modifică direct în baza de date, fără să i se recalculeze hash-ul. Verificarea găsește apoi exact această intrare.";
        ro["Entry #{0} was edited outside the application"] = "Intrarea #{0} a fost modificată în afara aplicației";
        ro["Every change, rule, case step, evidence check and approval is written here."] = "Aici se scrie fiecare schimbare, regulă, pas al cazului, verificare de dovadă și aprobare.";
        ro["Hash"] = "Hash";
        ro["Kind"] = "Tip";
        ro["No entries yet"] = "Încă nu există intrări";
        ro["Payload"] = "Conținut";
        ro["Tamper"] = "Falsifică";
        ro["Tamper with entry #{0}?"] = "Falsifici intrarea #{0}?";
        ro["The bank keeps a record that can credibly prove each confirmation, for audit purposes, and its electronic system must ensure that the information received is the information sent (ANAF guide, p. 34-35). Each entry is chained to the previous one with SHA-256: a change breaks the chain exactly there."] = "Banca păstrează o evidență care să poată demonstra, în mod credibil, fiecare confirmare, în scopuri de audit, iar sistemul electronic trebuie să asigure faptul că informațiile primite sunt informațiile trimise (ghidul ANAF, p. 34-35). Fiecare intrare e legată de cea anterioară prin SHA-256: o modificare rupe lanțul exact acolo.";
        ro["Verify"] = "Verifică";
        ro["When"] = "Când";
    }
}
