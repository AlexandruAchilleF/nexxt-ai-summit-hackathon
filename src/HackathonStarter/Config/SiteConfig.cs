using HackathonStarter.Services;
using MudBlazor;

namespace HackathonStarter.Config;

/// <summary>
/// THE FILE TO EDIT FIRST.
/// App name, colors, landing copy, navigation and demo users all come from here.
/// Adding a page = create Components/Pages/YourPage.razor with @page "/your-page" + one line in <see cref="Nav"/>.
/// </summary>
public static class SiteConfig
{
    public const string Name = "TRACE";
    public const string FullName = "Tax Residency Anomaly Correlation Engine";
    public static string Description => Lang.Pick(
        ro: "Fiecare modificare de date a clientului devine o decizie CRS/FATCA, înainte să expire termenul legal.",
        en: "Every change to a customer's data becomes a CRS/FATCA decision before the legal deadline.");
    public const string Logo = "/icons/icon.svg";

    /// <summary>Brand color (hex): filled buttons and selection. Keep white text on it at 4.5:1 or more. The MudBlazor theme is derived from it.</summary>
    public const string PrimaryColor = "#2563EB";
    public const string SecondaryColor = "#17468F";
    public const bool DarkModeByDefault = false;

    /// <summary>UI language for first-time visitors, "ro" or "en". Visitors switch with the RO/EN button (Services/Lang.cs).</summary>
    public const string DefaultLanguage = "ro";

    /// <summary>Formatting for money, numbers and dates (see Services/Format.cs).</summary>
    public const string Culture = "ro-RO";
    public const string Currency = "EUR";

    /// <summary>
    /// The company behind the app, used by the footer, Terms, Privacy policy and Contact pages.
    /// Hackathon prototype, no company: the footers show the product name and link only to /privacy. Terms, Privacy
    /// policy and Contact stay reachable by URL with these placeholders; fill them in before linking those pages again.
    /// </summary>
    public static readonly LegalInfo Legal = new(
        CompanyName: Name,
        RegistrationNumber: "[J40/0000/2026]",
        FiscalCode: "[RO00000000]",
        Address: "[Street, number, Bucharest, Romania]",
        Email: "contact@example.com",
        PrivacyEmail: "privacy@example.com",
        SecurityEmail: "security@example.com",
        LastUpdated: new DateOnly(2026, 10, 27));

    /// <summary>
    /// Landing page copy (Components/Pages/Home.razor), in both languages. Only claims we can show in the demo:
    /// no customer logos, no invented quotes or numbers.
    /// </summary>
    public static Landing Landing => Lang.Pick(LandingRo, LandingEn);

    private static readonly Landing LandingRo = new(
        Enabled: true,
        Headline: "Fiecare modificare de date, o decizie CRS/FATCA înainte de termen",
        CtaLabel: "Deschide aplicația",
        CtaHref: "/dashboard",
        Preview: new(
            ItemTitle: "Schimbare de adresă: Maria Popescu, München",
            ItemDescription: "Adresă în Germania, telefon german, niciun telefon românesc.",
            Prompt: "Pregătește cererea de clarificare către client",
            Reply: "Indicii CRS (p. 29): adresă în Germania; telefon german, niciun telefon în România. Salariu de la o firmă germană: semnal, fără alertă. Cerere: autocertificare nouă cu TIN-ul german sau explicație rezonabilă (p. 35), plus dovada adresei.",
            ResultTitle: "Necesită aprobare",
            ResultDetail: "Cerere către client · 2 documente cerute"),
        Sections:
        [
            new("Modificarea, transformată în caz",
                [
                    new(Icons.Material.Rounded.Rule, "Filtru în cod", "O mutare din Cluj în Iași nu deschide niciun caz, iar regula spune de ce."),
                    new(Icons.Material.Rounded.Timer, "Termenul, din ziua în care banca află", "90 de zile calendaristice, cu pagina din ghid; conturile preexistente și cele noi au reguli diferite."),
                    new(Icons.Material.Rounded.FormatQuote, "Dovezi cu citat", "Fiecare citat dintr-un document e verificat de cod în textul vizibil."),
                    new(Icons.Material.Rounded.Lightbulb, "Indicii și semnale", "Un salariu din Germania e semnal, nu indiciu: se arată, dar nu pornește nimic."),
                ]),
            new("Bucla închisă, cu omul la decizie",
                [
                    new(Icons.Material.Rounded.HowToReg, "Aprobare", "Cererea către client și închiderea cazului pleacă doar cu acordul operatorului."),
                    new(Icons.Material.Rounded.Verified, "Format TIN verificat", "Formatul, după fișele OCDE (SUA: IRS/SSA) și, pentru statele UE, la serviciul Comisiei Europene: „format valid”, nu „TIN valid”."),
                    new(Icons.Material.Rounded.Shield, "Scut pentru text ascuns", "Textul alb pe alb sau microscopic e scos înainte să ajungă la model și ridică o alertă."),
                    new(Icons.Material.Rounded.NotificationsActive, "Memento și escaladare", "Înainte de expirare: memento și escaladare; după: consecința exactă din ghid."),
                ]),
        ],
        Steps:
        [
            new("Modificarea intră", "Adresă, telefon, ordin permanent sau împuternicire, din core banking (simulat în demo)."),
            new("Codul filtrează", "Contează fiscal? Dacă da, cazul se deschide și, la CRS, termenul pornește."),
            new("Agentul pregătește", "Dovezi cu citat, explicația indiciilor, cererea către client."),
            new("Omul decide", "Aprobă cererea, apoi închiderea cu dovada confirmării."),
        ]);

    private static readonly Landing LandingEn = new(
        Enabled: true,
        Headline: "Every data change, a CRS/FATCA decision before the deadline",
        CtaLabel: "Open the application",
        CtaHref: "/dashboard",
        Preview: new(
            ItemTitle: "Address change: Maria Popescu, Munich",
            ItemDescription: "Address in Germany, a German phone, no Romanian phone.",
            Prompt: "Prepare the clarification request to the customer",
            Reply: "CRS indicia (p. 29): an address in Germany; a German phone, no phone in Romania. Salary from a German company: signal, no alert. Request: a new self-certification with the German TIN or a reasonable explanation (p. 35), plus proof of address.",
            ResultTitle: "Approval needed",
            ResultDetail: "Request to the customer · 2 documents requested"),
        Sections:
        [
            new("A data change, turned into a case",
                [
                    new(Icons.Material.Rounded.Rule, "Filter in code", "A move from Cluj to Iași opens no case, and the rule says why."),
                    new(Icons.Material.Rounded.Timer, "The deadline, from the day the bank knows", "90 calendar days, with the guide page; pre-existing and new accounts follow different rules."),
                    new(Icons.Material.Rounded.FormatQuote, "Evidence with quotes", "Every quote from a document is checked by code against its visible text."),
                    new(Icons.Material.Rounded.Lightbulb, "Indicia and signals", "A salary from Germany is a signal, not an indicium: it is shown, but triggers nothing."),
                ]),
            new("The loop closed, with a person deciding",
                [
                    new(Icons.Material.Rounded.HowToReg, "Approval", "The request to the customer and the closing of the case go out only with the operator's consent."),
                    new(Icons.Material.Rounded.Verified, "TIN format checked", "The format, from the OECD sheets (US: IRS/SSA) and, for EU states, via the European Commission's service: \"format valid\", not \"TIN valid\"."),
                    new(Icons.Material.Rounded.Shield, "Hidden-text shield", "White-on-white or microscopic text is removed before it reaches the model and raises an alert."),
                    new(Icons.Material.Rounded.NotificationsActive, "Reminder and escalation", "Before the deadline: a reminder and an escalation; after it: the exact consequence from the guide."),
                ]),
        ],
        Steps:
        [
            new("The change comes in", "Address, phone, standing order or power of attorney, from core banking (simulated in the demo)."),
            new("Code filters", "Does it matter for tax? If so, the case opens and, for CRS, the deadline starts."),
            new("The agent prepares", "Evidence with quotes, an explanation of the indicia, the request to the customer."),
            new("A person decides", "Approves the request, then the closing with proof of confirmation."),
        ]);

    public static readonly NavItem[] Nav =
    [
        new("Dashboard", "/dashboard", Icons.Material.Rounded.SpaceDashboard),
        new("Cases", "/cases", Icons.Material.Rounded.AssignmentLate, "CRS"),
        new("New change", "/changes/new", Icons.Material.Rounded.EditNote, "CRS"),
        new("Clients", "/clients", Icons.Material.Rounded.People, "CRS"),
        new("Audit log", "/audit", Icons.Material.Rounded.VerifiedUser, "CRS"),
        new("Metrics", "/metrics", Icons.Material.Rounded.Insights, "CRS"),
        // Assistant, Arena, Scenarios, Items and Calculators stay reachable by URL (E2E tests and the Docker CI check use them).
        // @gen:nav (the feature generator inserts new pages above this line)
        new("Agent runs", "/runs", Icons.Material.Rounded.History, "How it works"),
        new("Architecture", "/architecture", Icons.Material.Rounded.AccountTree, "How it works"),
        new("Settings", "/settings", Icons.Material.Rounded.Settings, "Account"),
    ];

    /// <summary>
    /// Bottom navigation on phones (max 4–5 hrefs from <see cref="Nav"/>; the menu button still shows everything).
    /// Empty = no bottom bar.
    /// </summary>
    public static readonly string[] MobileNav = ["/dashboard", "/cases", "/changes/new", "/clients"];

    /// <summary>
    /// Installable app (PWA): "Install" in Chrome/Edge (desktop + Android), "Add to Home Screen" on iPhone.
    /// Opens in its own window with this name and icon. Icons: wwwroot/icons (replace them with your logo).
    /// </summary>
    public static readonly Pwa Pwa = new(Enabled: true, ShortName: Name, StartUrl: "/dashboard", BackgroundColor: "#F3F6FA");

    /// <summary>Demo users replace login: pick one from the app bar. The first one is the default.</summary>
    public static readonly DemoUser[] Users =
    [
        new("admin", "Ana Popa", "ana@example.com", Roles.Admin, "#2563EB"),
        new("member", "Mihai Stan", "mihai@example.com", Roles.Member, "#17468F"),
        new("viewer", "Vera Ionescu", "vera@example.com", Roles.Viewer, "#63758A"),
    ];
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";

    /// <summary>Roles that can create/edit data and approve agent actions.</summary>
    public const string Editors = Admin + "," + Member;

    // CRS Sentinel names for the same roles: the operator approves requests and closings (and moves the demo clock),
    // the tax department receives escalations.
    public const string Operator = Admin;
    public const string TaxDepartment = Member;
}

public record DemoUser(string Id, string Name, string Email, string Role, string Color)
{
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(p => p[0])).ToUpperInvariant();
}

public record Pwa(bool Enabled, string ShortName, string StartUrl, string BackgroundColor);

public record NavItem(string Label, string Href, string Icon, string? Group = null);

public record LegalInfo(string CompanyName, string RegistrationNumber, string FiscalCode, string Address,
                        string Email, string PrivacyEmail, string SecurityEmail, DateOnly LastUpdated);

public record LandingFeature(string Icon, string Title, string Description);

/// <summary>The static app preview under the hero: the case and the agent panel.</summary>
public record LandingPreview(string ItemTitle, string ItemDescription, string Prompt, string Reply, string ResultTitle, string ResultDetail);

public record LandingSection(string Title, LandingFeature[] Features);

public record LandingStep(string Title, string Description);

public record Landing(bool Enabled, string Headline, string CtaLabel, string CtaHref,
                      LandingPreview Preview, LandingSection[] Sections, LandingStep[] Steps);
