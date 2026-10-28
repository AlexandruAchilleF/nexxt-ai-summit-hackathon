namespace HackathonStarter.Services;

// Romanian UI text for Features/Sentinel (CrsSentinelAgent, approvals). Owner: R4. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoSentinel(Dictionary<string, string> ro)
    {
        ro["Close case {0} (channel: {1}); the code re-evaluates the rules first"] = "Închide cazul {0} (canal: {1}); codul reevaluează mai întâi regulile";
        ro["Send the request for case {0} by {1}"] = "Trimite cererea pentru cazul {0} prin {1}";
        ro["Send the request for case {0} by {1}: {2}"] = "Trimite cererea pentru cazul {0} prin {1}: {2}";
    }
}
