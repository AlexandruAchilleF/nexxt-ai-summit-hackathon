namespace HackathonStarter.Services;

// Romanian UI text for the shared CRS pages and nav (Components/Pages/Cases, Changes, Clients, Audit, Metrics). Owner: shared UI.
// One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoCrsUi(Dictionary<string, string> ro)
    {
        ro["Addresses, phones, accounts, standing orders, powers of attorney and self-certifications."] = "Adrese, telefoane, conturi, instrucțiuni permanente de transfer, împuterniciri și autocertificări.";
        ro["All cases"] = "Toate cazurile";
        ro["Audit log"] = "Jurnal";
        ro["Case {0}"] = "Cazul {0}";
        ro["Cases"] = "Cazuri";
        ro["Caught, missed and false alarms on the golden cases and the synthetic clients."] = "Prinse, ratate și alarme false pe cazurile-etalon și pe clienții sintetici.";
        ro["Client {0}"] = "Clientul {0}";
        ro["Coming in the next phase"] = "Vine în faza următoare";
        ro["CRS"] = "CRS";
        ro["Every step, rule and human decision in a hash chain."] = "Fiecare pas, fiecare regulă și fiecare decizie umană, într-un lanț de hash-uri.";
        ro["Indicia with the guide page, evidence, the client request and the deadline."] = "Indiciile cu pagina din ghid, dovezile, cererea către client și termenul.";
        ro["Metrics"] = "Metrici";
        ro["New change"] = "Modificare nouă";
        ro["Record a change to a client's data and see the decision immediately."] = "Înregistrează o modificare a datelor unui client și vezi imediat decizia.";
        ro["The backend for this page is being built."] = "Partea de backend a acestei pagini este în lucru.";
        ro["The queue of CRS/FATCA cases, ordered by deadline."] = "Coada cazurilor CRS/FATCA, ordonată după termen.";
    }
}
