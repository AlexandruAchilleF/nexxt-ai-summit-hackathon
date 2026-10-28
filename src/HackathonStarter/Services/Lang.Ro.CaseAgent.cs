namespace HackathonStarter.Services;

// Romanian UI text for the agent panel on the case page (Components/Crs/CaseAgentPanel). One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoCaseAgent(Dictionary<string, string> ro)
    {
        ro["Last run"] = "Ultima rulare";
        ro["Only admin and member can start the agent and approve its actions."] = "Doar admin și member pot porni agentul și aproba acțiunile lui.";
        ro["Open the run"] = "Deschide rularea";
        ro["The agent is working…"] = "Agentul lucrează…";
        ro["The agent reads the documents, records the quotes and the signals and drafts the letter. Code decides; nothing is sent or closed without your approval."] = "Agentul citește documentele, înregistrează citatele și semnalele și redactează scrisoarea. Codul decide; nimic nu se trimite și nu se închide fără aprobarea ta.";
        ro["The client answers"] = "Răspunde clientul";
        ro["The client's answer is in the file. Use \"{0}\"."] = "Răspunsul clientului e la dosar. Folosește „{0}”.";
        ro["the new self-certification arrives"] = "sosește autocertificarea nouă";
    }
}
