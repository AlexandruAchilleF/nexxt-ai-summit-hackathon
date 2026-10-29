namespace HackathonStarter.Services;

public static partial class Lang
{
    static partial void AddRoArchitecture(Dictionary<string, string> ro)
    {
        ro["A case opens with its deadline"] = "Se deschide un caz, cu termenul lui";
        ro["A change comes in"] = "Sosește o schimbare";
        ro["A four-eyes warning when the approver also drafted it"] = "Un avertisment de patru ochi când cel care aprobă a scris și ciorna";
        ro["A person approves"] = "Un om aprobă";
        ro["A person approves the closing"] = "Un om aprobă închiderea";
        ro["A person approves the letter"] = "Un om aprobă scrisoarea";
        ro["AI"] = "AI";
        ro["AI explains"] = "AI-ul explică";
        ro["broken at entry #{0}"] = "rupt la intrarea #{0}";
        ro["Closed with the proof"] = "Închis cu dovada";
        ro["Closing the case"] = "Închiderea cazului";
        ro["Code checks the answer again"] = "Codul verifică din nou răspunsul";
        ro["Code decides"] = "Codul decide";
        ro["Code decides. AI explains. A person approves."] = "Codul decide. AI-ul explică. Un om aprobă.";
        ro["{0}: {1} tools, {2} of them wait for a person"] = "{0}: {1} unelte, dintre care {2} așteaptă un om";
        ro["Deadlines by rule: {0} for a new account, {1} for a pre-existing one"] = "Termene după regulă: {0} la un cont nou, {1} la unul preexistent";
        ro["Every quote is checked against the document's visible text"] = "Fiecare citat e verificat pe textul vizibil al documentului";
        ro["Every step is written to the hash-chained audit log."] = "Fiecare pas e scris în jurnalul cu lanț de hash-uri.";
        ro["How {0} decides"] = "Cum decide {0}";
        ro["In a bank"] = "Într-o bancă";
        ro["In the demo, the form simulates each change. In a bank, core banking would send every change to the same service, and the same rules would run."] =
            "În demo, formularul simulează fiecare schimbare. Într-o bancă, core banking-ul ar trimite fiecare schimbare la același serviciu, iar aceleași reguli ar rula.";
        ro["intact · {0} entries"] = "intact · {0} intrări";
        ro["It never asks a model whether a rule applies."] = "Nu întreabă niciodată un model dacă se aplică o regulă.";
        ro["It never decides and never sends."] = "Nu decide și nu trimite niciodată.";
        ro["Maria's case, step by step"] = "Cazul Mariei, pas cu pas";
        ro["Model: {0} ({1})"] = "Model: {0} ({1})";
        ro["Model: {0} on Amazon Bedrock, {1}"] = "Model: {0} pe Amazon Bedrock, {1}";
        ro["Moving the demo date and Tamper are admin only"] = "Mutarea datei demo și „Falsifică” sunt doar pentru admin";
        ro["National ID, IBAN, card, email and phone are masked before the model"] = "CNP-ul, IBAN-ul, cardul, emailul și telefonul sunt mascate înainte de model";
        ro["No model set: the simulation runs the same real tools"] = "Niciun model setat: simularea rulează aceleași unelte reale";
        ro["Nothing irreversible happens in one click."] = "Nimic ireversibil nu se întâmplă dintr-un click.";
        ro["Person"] = "Om";
        ro["Reads the documents, quotes the evidence and drafts the letter in the client's language"] = "Citește documentele, citează dovezile și scrie ciorna scrisorii în limba clientului";
        ro["Sending the letter to the client"] = "Trimiterea scrisorii către client";
        ro["The agent reads and quotes the file"] = "Agentul citește dosarul și citează";
        ro["The agent's tools"] = "Uneltele agentului";
        ro["waits for a person's approval"] = "așteaptă aprobarea unui om";
        ro["The rules are applied by code"] = "Codul aplică regulile";
        ro["The shield removes hidden text before any model reads a document"] = "Scutul scoate textul ascuns înainte ca vreun model să citească documentul";
        ro["{0} checks on every answer: no personal data, every quote verified"] = "{0} verificări pe fiecare răspuns: fără date personale, fiecare citat verificat";
        ro["{0} rules, each with an ID and a source: {1} from the text of the law, {2} our reading, {3} bank policy"] =
            "{0} reguli, fiecare cu ID și sursă: {1} din textul legii, {2} interpretarea noastră, {3} politica băncii";
    }
}
