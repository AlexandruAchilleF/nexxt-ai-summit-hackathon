namespace HackathonStarter.Services;

public static partial class Lang
{
    static partial void AddRoConnections(Dictionary<string, string> ro)
    {
        ro["(empty)"] = "(gol)";
        ro["down ({0}), but a recorded answer exists"] = "indisponibil ({0}), dar există un răspuns înregistrat";
        ro["down: {0}"] = "indisponibil: {0}";
        ro["live SOAP check failed"] = "verificarea SOAP live a eșuat";
        ro["main model"] = "model principal";
        ro["not usable: missing or invalid settings, the main model is used instead"] = "inutilizabil: setări lipsă sau incorecte; se folosește modelul principal";
        ro["profile “{0}”"] = "profil „{0}”";
        ro["SOAP checkTin response received (connectivity only)"] = "Răspuns SOAP checkTin primit (doar conectivitate)";
        ro["target “{0}”"] = "țintă „{0}”";
        ro["this app"] = "această aplicație";
        ro["unexpected SOAP response"] = "răspuns SOAP neașteptat";
    }
}
