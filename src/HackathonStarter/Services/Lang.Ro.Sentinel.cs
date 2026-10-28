namespace HackathonStarter.Services;

// Romanian UI text for Features/Sentinel (CrsSentinelAgent, approvals). Owner: R4. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoSentinel(Dictionary<string, string> ro)
    {
        ro["Close case {0} (channel: {1}); the code re-evaluates the rules first"] = "Închide cazul {0} (canal: {1}); codul reevaluează mai întâi regulile";
        ro["Send the request for case {0} by {1}"] = "Trimite cererea pentru cazul {0} prin {1}";
        ro["Send the request for case {0} by {1}: {2}"] = "Trimite cererea pentru cazul {0} prin {1}: {2}";

        // Features/Checks/CrsChecks.cs: titles, reasons and findings, saved in English on the run and shown translated (AgentTrace, Lang.TFilled).
        ro["{0} of {1} quotation(s) are not among the {2} verified quote(s)."] = "{0} din {1} citate nu se află printre cele {2} citate verificate.";
        ro["{0} sentence(s) mention a signal; none presents it as an indicium or an alert."] = "Propoziții care menționează un semnal: {0}; niciuna nu îl prezintă ca indiciu sau alertă.";
        ro["{0} sentence(s) mention an indicium without a rule ID or a guide page."] = "Propoziții care menționează un indiciu fără ID de regulă sau pagină din ghid: {0}.";
        ro["{0} sentence(s) present a context signal as an indicium or an alert."] = "Propoziții care prezintă un semnal de context ca indiciu sau alertă: {0}.";
        ro["All {0} quotation(s) are verified evidence."] = "Toate citatele ({0}) sunt dovezi verificate.";
        ro["All {0} sentence(s) about an indicium cite a rule ID or a guide page."] = "Toate propozițiile despre un indiciu ({0}) citează un ID de regulă sau o pagină din ghid.";
        ro["Every indicium cites its rule"] = "Fiecare indiciu își citează regula";
        ro["indicium without rule ID or page"] = "indiciu fără ID de regulă sau pagină";
        ro["No \"{0}\" fact given."] = "Lipsește faptul „{0}”.";
        ro["No \"TIN valid\" claim"] = "Fără afirmația „TIN valid”";
        ro["Quotes are verified evidence"] = "Citatele sunt dovezi verificate";
        ro["signal presented as an alert"] = "semnal prezentat ca alertă";
        ro["signal presented as an indicium"] = "semnal prezentat ca indiciu";
        ro["Signals are never alerts"] = "Semnalele nu sunt niciodată alerte";
        ro["The \"{0}\" fact is not valid JSON, so no quotation can be verified."] = "Faptul „{0}” nu este JSON valid, deci niciun citat nu poate fi verificat.";
        ro["The answer contains no quotation."] = "Răspunsul nu conține niciun citat.";
        ro["The answer mentions no context signal."] = "Răspunsul nu menționează niciun semnal de context.";
        ro["The answer mentions no indicium."] = "Răspunsul nu menționează niciun indiciu.";
    }
}
