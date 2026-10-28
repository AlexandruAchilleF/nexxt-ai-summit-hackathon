using System.Text.RegularExpressions;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Sentinel;

/// <summary>
/// The two fixed messages the case page sends to the agent (one run each, with Context = the case code), so the buttons, the
/// mock and the tests use the same words. Recognition does not need the exact text: the action's keywords (Romanian or
/// English, without diacritics) and a case code are enough.
/// </summary>
public static partial class SentinelActions
{
    public enum Action { None, Analyze, Verify }

    /// <summary>"Analizează dosarul" (run 1): read the documents, record evidence and signals, draft the request, propose sending it.</summary>
    public static string Analyze(string caseCode) => Lang.Pick(
        $"Analizează dosarul cazului {caseCode}: citește documentele clientului, înregistrează dovezile și semnalele, redactează cererea către client și propune trimiterea.",
        $"Analyze the file of case {caseCode}: read the client's documents, record the evidence and the signals, draft the request to the client and propose sending it.");

    /// <summary>"Verifică răspunsul" (run 2): read the new self-certification, check the TIN format, register, re-evaluate, propose closing.</summary>
    public static string Verify(string caseCode) => Lang.Pick(
        $"Verifică răspunsul clientului pentru cazul {caseCode}: citește autocertificarea nouă, verifică formatul TIN, înregistrează răspunsul, reevaluează și propune închiderea.",
        $"Check the client's answer for case {caseCode}: read the new self-certification, check the TIN format, register the answer, re-evaluate and propose closing.");

    public static string AnalyzeLabel => Lang.Pick("Analizează dosarul", "Analyze the file");
    public static string VerifyLabel => Lang.Pick("Verifică răspunsul", "Check the answer");

    /// <summary>Which of the two actions a message asks for; None when it names no case or neither action.</summary>
    public static Action Recognize(string message)
    {
        if (string.IsNullOrWhiteSpace(message) || !CaseCode().IsMatch(message)) return Action.None;
        if (TextMatch.ContainsAny(message, "verifica raspuns", "raspunsul clientului", "check the client's answer", "check the answer", "client's answer"))
            return Action.Verify;
        if (TextMatch.ContainsAny(message, "analizeaza dosar", "dosarul cazului", "analyze the file", "analyse the file", "file of case"))
            return Action.Analyze;
        return Action.None;
    }

    [GeneratedRegex(@"\bCRS-\d{4}-\d{4}\b", RegexOptions.IgnoreCase)]
    private static partial Regex CaseCode();
}
