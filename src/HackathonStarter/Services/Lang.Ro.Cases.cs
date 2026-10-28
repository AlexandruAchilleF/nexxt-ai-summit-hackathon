namespace HackathonStarter.Services;

// Romanian UI text for Features/Cases and Features/Deadlines. Owner: R2. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoCases(Dictionary<string, string> ro)
    {
        // Required documents (Features/Cases/RequiredDocuments.cs): stored in English on the request, shown translated.
        ro["A new FATCA self-certification"] = "O autocertificare FATCA nouă";
        ro["A new self-certification"] = "O autocertificare nouă";
        ro["A new self-certification, or a reasonable explanation with supporting documents (if applicable)"] = "O autocertificare nouă sau o explicație rezonabilă și documentele aferente (dacă este cazul)";
        ro["New documentary evidence of residence"] = "Un document justificativ nou privind rezidența";
        ro["Proof of the new address"] = "Dovada noii adrese";
    }
}
