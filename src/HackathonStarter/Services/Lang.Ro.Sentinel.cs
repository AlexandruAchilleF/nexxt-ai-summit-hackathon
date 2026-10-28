namespace HackathonStarter.Services;

// Romanian UI text for Features/Sentinel (CrsSentinelAgent, approvals). Owner: R4. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoSentinel(Dictionary<string, string> ro)
    {
        ro["Files with HTML or XML markup are not accepted in the chat: markup can hide text from a reader. Attach a PDF or plain text."] = "Fișierele cu marcaje HTML sau XML nu sunt acceptate în chat: marcajele pot ascunde text de cititor. Atașează un PDF sau text simplu.";
        ro["No visible text found (scanned PDF, or only hidden text). Try a text-based PDF."] = "Nu am găsit text vizibil (PDF scanat sau doar text ascuns). Încearcă un PDF cu text.";
        ro["No visible text found in the file."] = "Nu am găsit text vizibil în fișier.";
        ro["{0}: hidden text removed before the model reads it ({1} fragments)."] = "{0}: textul ascuns a fost eliminat înainte să-l citească modelul ({1} fragmente).";
    }
}
