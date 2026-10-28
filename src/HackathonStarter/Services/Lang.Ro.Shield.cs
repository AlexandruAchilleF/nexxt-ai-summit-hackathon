namespace HackathonStarter.Services;

// Romanian UI text for Features/Shield (hidden text, evidence). Owner: R3. One line per key, sorted: ro["English"] = "Română";
public static partial class Lang
{
    static partial void AddRoShield(Dictionary<string, string> ro)
    {
        ro["Covered text is not detected"] = "Textul acoperit nu este detectat";
        ro["font under 2 pt"] = "font sub 2 pt";
        ro["invisible text"] = "text invizibil";
        ro["No hidden text"] = "Fără text ascuns";
        ro["Not scanned yet"] = "Nescanat încă";
        ro["Only in hidden text"] = "Doar în textul ascuns";
        ro["outside the page"] = "în afara paginii";
        ro["Page {0}"] = "Pagina {0}";
        ro["Quote not found"] = "Citat negăsit";
        ro["Quote verified"] = "Citat verificat";
        ro["Record the upload and scan"] = "Înregistrează încărcarea și scanează";
        ro["Red flag from the shield: {0} hidden fragment(s) removed"] = "Semnal roșu de la scut: {0} fragment(e) ascuns(e) eliminat(e)";
        ro["Scanning…"] = "Se scanează…";
        ro["white text"] = "text alb";
    }
}
