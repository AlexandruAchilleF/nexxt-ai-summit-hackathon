namespace HackathonStarter.Services;

// Romanian UI text for Features/Sentinel (CrsSentinelAgent, approvals). Owner: R4. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoSentinel(Dictionary<string, string> ro)
    {
        ro["{0}: hidden text removed before the model reads it ({1} fragments)."] = "{0}: textul ascuns a fost eliminat înainte să-l citească modelul ({1} fragmente).";
    }
}
