namespace HackathonStarter.Services;

// Romanian UI text for Features/Cases and Features/Deadlines. Owner: R2. One line per key, sorted: ro["English"] = "Română";
// The case vocabulary of the CRS screens (statuses, deadlines, requests, proof) lives here: Components/Crs/CaseText.cs,
// DeadlineClock.razor, DaysLeft.razor.
public static partial class Lang
{
    static partial void AddRoCases(Dictionary<string, string> ro)
    {
        ro["(+{0} days)"] = "(+{0} zile)";
        ro["{0} overdue"] = "Depășit cu {0}";
        ro["Account holder"] = "Titularul contului";
        ro["Approved, not sent yet"] = "Aprobată, netrimisă încă";
        ro["Awaiting client"] = "Așteaptă clientul";
        ro["Case closed"] = "Caz închis";
        ro["Change received, case opened"] = "Schimbare primită, caz deschis";
        ro["Clarification needed"] = "Necesită clarificare";
        ro["Client answered"] = "Clientul a răspuns";
        ro["Closed"] = "Închis";
        ro["closed on {0}"] = "închis pe {0}";
        ro["Compliant"] = "Conform";
        ro["Confirmed in the app"] = "Confirmat în aplicație";
        ro["Covered by an accepted explanation"] = "Acoperit de o explicație acceptată";
        ro["Covered by the self-certification"] = "Acoperit de autocertificare";
        ro["CRS and FATCA"] = "CRS și FATCA";
        ro["Day {0}"] = "Ziua {0}";
        ro["Day {0} of {1}"] = "Ziua {0} din {1}";
        ro["Deadline passed, case expired"] = "Termen depășit, caz expirat";
        ro["Deadline rule"] = "Regula termenului";
        ro["Diplomat"] = "Diplomat";
        ro["Due date"] = "Termen";
        ro["Electronic acceptance"] = "Acceptare electronică";
        ro["Escalated to tax"] = "Escaladat la fiscal";
        ro["Escalated to the tax department"] = "Escaladat la departamentul fiscal";
        ro["EU official"] = "Funcționar UE";
        ro["Expired"] = "Expirat";
        ro["expired on {0}"] = "expirat pe {0}";
        ro["FATCA alert"] = "Alertă FATCA";
        ro["First request"] = "Prima cerere";
        ro["Follow-up request"] = "Cerere suplimentară";
        ro["guide p. {0}"] = "ghid p. {0}";
        ro["Hidden text found"] = "Text ascuns găsit";
        ro["In person"] = "Față în față";
        ro["Legal deadline"] = "Termenul legal";
        ro["Moderate signal"] = "Semnal moderat";
        ro["New self-certification"] = "Autocertificare nouă";
        ro["No case"] = "Fără caz";
        ro["No deadline"] = "Fără termen";
        ro["no legal deadline in the guide"] = "fără termen legal în ghid";
        ro["No page in the guide was found for this rule."] = "Pentru această regulă nu am găsit pagina în ghid.";
        ro["none in the guide"] = "niciunul în ghid";
        ro["Not covered"] = "Neacoperit";
        ro["Not on the guide's list: a person decides"] = "Nu e în lista din ghid: decide un om";
        ro["on day {0}, {1}"] = "în ziua {0}, {1}";
        ro["Online"] = "Online";
        ro["Open"] = "Deschis";
        ro["Other answer"] = "Alt răspuns";
        ro["p. {0}"] = "p. {0}";
        ro["past the deadline"] = "peste termen";
        ro["Reasonable explanation"] = "Explicație rezonabilă";
        ro["Rejected by the operator"] = "Respinsă de operator";
        ro["Remote"] = "La distanță";
        ro["Reminder"] = "Memento";
        ro["Reminder date reached"] = "A venit data memento-ului";
        ro["Reminder due"] = "Memento programat";
        ro["Reminder sent"] = "Memento trimis";
        ro["Reminder sent to the client"] = "Memento trimis clientului";
        ro["Representative"] = "Reprezentant";
        ro["Request approved"] = "Cerere aprobată";
        ro["Request approved by {0}"] = "Cerere aprobată de {0}";
        ro["Response received"] = "Răspuns primit";
        ro["sent"] = "trimis";
        ro["Sent"] = "Trimisă";
        ro["Signed document"] = "Document semnat";
        ro["Student"] = "Student";
        ro["Teacher or trainee"] = "Profesor sau stagiar";
        ro["The deadline is today"] = "Termenul e azi";
        ro["The guide gives no deadline for this case; we do not borrow the CRS 90 days."] = "Ghidul nu dă un termen pentru acest caz; nu împrumutăm cele 90 de zile de la CRS.";
        ro["to the deadline"] = "până la termen";
        ro["Today, on the demo date"] = "Azi, în data demo";
        ro["Voice recording"] = "Înregistrare vocală";
        ro["Weak signal"] = "Semnal slab";
    }
}
