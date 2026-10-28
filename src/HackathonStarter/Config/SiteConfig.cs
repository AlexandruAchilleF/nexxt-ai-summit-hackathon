using HackathonStarter.Services;
using MudBlazor;

namespace HackathonStarter.Config;

/// <summary>
/// ⭐ THE FILE TO EDIT FIRST ⭐
/// App name, colors, landing copy, navigation and demo users all come from here.
/// Adding a page = create Components/Pages/YourPage.razor with @page "/your-page" + one line in <see cref="Nav"/>.
/// </summary>
public static class SiteConfig
{
    // Working name: change it here (and in the PWA icons) once the team picks the final one.
    public const string Name = "CRS Sentinel";
    public static string Description => Lang.Pick(
        ro: "Fiecare modificare de date a clientului devine o decizie CRS/FATCA, înainte să expire termenul legal.",
        en: "Every change to a customer's data becomes a CRS/FATCA decision before the legal deadline.");
    public const string Logo = "/icons/icon.svg"; // a path in wwwroot like "/logo.svg", or an emoji

    /// <summary>Brand color (hex): filled buttons and selection. Keep white text on it at 4.5:1 or more. The MudBlazor theme is derived from it.</summary>
    public const string PrimaryColor = "#0071E3";
    public const string SecondaryColor = "#C93400";
    public const bool DarkModeByDefault = true;

    /// <summary>UI language for first-time visitors, "ro" or "en". Visitors switch with the RO/EN button (Services/Lang.cs).</summary>
    public const string DefaultLanguage = "ro";

    /// <summary>Formatting for money, numbers and dates (see Services/Format.cs).</summary>
    public const string Culture = "ro-RO";
    public const string Currency = "EUR";

    /// <summary>
    /// The company behind the app, used by the footer, Terms, Privacy policy and Contact pages.
    /// Replace every [bracketed] value before going public, and have the legal texts reviewed.
    /// </summary>
    public static readonly LegalInfo Legal = new(
        CompanyName: "[Company name SRL]",
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
        Subheadline: "Când un client își schimbă adresa, telefonul sau un ordin permanent, agentul deschide cazul, pornește termenul din Ghidul ANAF, adună dovezile și scrie cererea către client. Omul decide.",
        CtaLabel: "Deschide aplicația",
        CtaHref: "/dashboard",
        Announcement: new("Nou", "Regulile din Ghidul ANAF, cu pagina lângă fiecare", "/#how"),
        Preview: new(
            ItemId: "CRS-0412",
            ItemTitle: "Schimbare de adresă: Maria Ionescu, München",
            ItemDescription: "Adresă nouă în Germania, telefon german și niciun telefon românesc. Cont nou: termen de 90 de zile.",
            Activity:
            [
                "Modificare de adresă primită din core banking",
                "Caz deschis · termen 26 ianuarie 2027",
                "Agentul a găsit 2 indicii și 1 semnal, toate cu citat verificat",
            ],
            Prompt: "Pregătește cererea de clarificare către client",
            Reply: "Indicii CRS (ghidul, p. 29): adresă în Germania, telefon german fără telefon în România. Salariul de la o firmă germană e doar un semnal, fără alertă. Cererea e gata: autocertificare nouă și TIN-ul german.",
            ResultTitle: "Necesită aprobare",
            ResultDetail: "Cerere către client · 2 documente de trimis"),
        Statement: new("Regulile sunt cod, nu păreri.",
            "Fiecare indiciu are ID și pagina din Ghidul ANAF, fiecare citat e verificat în document, iar fiecare termen e urmărit pe caz. Modelul citește și scrie; omul aprobă ce pleacă și ce se închide."),
        Sections:
        [
            new("Modificarea, transformată în caz",
                "Doar modificările care ating un indiciu din lege pornesc un termen. Restul se opresc la filtru, cu explicație.",
                [
                    new(Icons.Material.Rounded.Rule, "Filtru în cod", "O mutare din Cluj în Iași nu deschide niciun caz, iar regula spune de ce."),
                    new(Icons.Material.Rounded.Timer, "Termen pe tipul de cont", "90 de zile la conturile noi; la cele preexistente, sfârșitul anului sau 90 de zile."),
                    new(Icons.Material.Rounded.FormatQuote, "Dovezi cu citat", "Fiecare dovadă are citatul exact, verificat de cod în document."),
                    new(Icons.Material.Rounded.Lightbulb, "Indicii și semnale", "Un salariu din Germania e semnal, nu indiciu: se arată, dar nu pornește nimic."),
                ]),
            new("Bucla închisă, cu omul la decizie",
                "Cererea, răspunsul, reevaluarea și închiderea, cu dovada confirmării. Ce nu se potrivește ajunge la om.",
                [
                    new(Icons.Material.Rounded.HowToReg, "Aprobare", "Cererea către client și închiderea cazului pleacă doar cu acordul operatorului."),
                    new(Icons.Material.Rounded.Verified, "TIN verificat", "Formatul, la serviciul Comisiei Europene și după fișele OCDE: „format valid”, nu „TIN valid”."),
                    new(Icons.Material.Rounded.Shield, "Scut pentru text ascuns", "Textul alb pe alb sau microscopic e scos înainte să ajungă la model și devine semnal roșu."),
                    new(Icons.Material.Rounded.NotificationsActive, "Memento și escaladare", "Înainte de expirare: memento și escaladare; după: consecința exactă din ghid."),
                ]),
        ],
        Steps:
        [
            new("Modificarea intră", "Adresă, telefon, ordin permanent sau împuternicire, din core banking."),
            new("Codul filtrează", "Contează fiscal? Dacă da, cazul se deschide și termenul pornește."),
            new("Agentul pregătește", "Dovezi cu citat, indiciile din lege, cererea către client."),
            new("Omul decide", "Aprobă cererea, apoi închiderea cu dovada confirmării."),
        ],
        Closing: new("Agentul urmărește termenul.", "Omul decide."));

    private static readonly Landing LandingEn = new(
        Enabled: true,
        Headline: "Every data change, a CRS/FATCA decision before the deadline",
        Subheadline: "When a customer changes their address, phone or a standing order, the agent opens a case, starts the deadline from the ANAF guide, gathers the evidence and drafts the request to the customer. A person decides.",
        CtaLabel: "Open the app",
        CtaHref: "/dashboard",
        Announcement: new("New", "The rules of the ANAF guide, each with its page", "/#how"),
        Preview: new(
            ItemId: "CRS-0412",
            ItemTitle: "Address change: Maria Ionescu, Munich",
            ItemDescription: "New address in Germany, a German phone and no Romanian phone. New account: 90-day deadline.",
            Activity:
            [
                "Address change received from core banking",
                "Case opened · deadline 26 January 2027",
                "The agent found 2 indicia and 1 signal, every quote verified",
            ],
            Prompt: "Prepare the clarification request to the customer",
            Reply: "CRS indicia (guide, p. 29): an address in Germany, a German phone and no phone in Romania. The salary from a German company is only a signal, no alert. The request is ready: a new self-certification and the German TIN.",
            ResultTitle: "Approval needed",
            ResultDetail: "Request to the customer · 2 documents to send"),
        Statement: new("The rules are code, not opinions.",
            "Every indicium has an ID and its page in the ANAF guide, every quote is checked against the document and every deadline is tracked per case. The model reads and writes; a person approves what goes out and what gets closed."),
        Sections:
        [
            new("A data change, turned into a case",
                "Only changes that touch an indicium from the law start a deadline. The rest stop at the filter, with an explanation.",
                [
                    new(Icons.Material.Rounded.Rule, "Filter in code", "A move from Cluj to Iași opens no case, and the rule says why."),
                    new(Icons.Material.Rounded.Timer, "Deadline by account type", "90 days for new accounts; for pre-existing ones, the end of the year or 90 days."),
                    new(Icons.Material.Rounded.FormatQuote, "Evidence with quotes", "Every piece of evidence has its exact quote, checked by code against the document."),
                    new(Icons.Material.Rounded.Lightbulb, "Indicia and signals", "A salary from Germany is a signal, not an indicium: it is shown, but triggers nothing."),
                ]),
            new("The loop closed, with a person deciding",
                "The request, the reply, the re-assessment and the closing, with proof of confirmation. Anything that doesn't fit goes to a person.",
                [
                    new(Icons.Material.Rounded.HowToReg, "Approval", "The request to the customer and the closing of the case go out only with the operator's consent."),
                    new(Icons.Material.Rounded.Verified, "TIN checked", "The format, against the European Commission's service and the OECD sheets: \"valid format\", not \"valid TIN\"."),
                    new(Icons.Material.Rounded.Shield, "Hidden-text shield", "White-on-white or microscopic text is removed before it reaches the model and becomes a red flag."),
                    new(Icons.Material.Rounded.NotificationsActive, "Reminder and escalation", "Before the deadline: a reminder and an escalation; after it: the exact consequence from the guide."),
                ]),
        ],
        Steps:
        [
            new("The change comes in", "Address, phone, standing order or power of attorney, from core banking."),
            new("Code filters", "Does it matter for tax? If so, the case opens and the deadline starts."),
            new("The agent prepares", "Evidence with quotes, the legal indicia, the request to the customer."),
            new("A person decides", "Approves the request, then the closing with proof of confirmation."),
        ],
        Closing: new("The agent tracks the deadline.", "A person decides."));

    public static readonly NavItem[] Nav =
    [
        new("Dashboard", "/dashboard", Icons.Material.Rounded.SpaceDashboard),
        // Items and Calculators stay reachable by URL (tests use them) until the CRS pages replace them.
        // @gen:nav (the feature generator inserts new pages above this line)
        new("Assistant", "/assistant", Icons.Material.Rounded.AutoAwesome, "Agents"),
        new("Agent Arena", "/arena", Icons.Material.Rounded.Forum, "Agents"),
        new("Scenarios", "/scenarios", Icons.Material.Rounded.QuestionAnswer, "Agents"),
        new("Agent runs", "/runs", Icons.Material.Rounded.History, "Agents"),
        new("Exports", "/exports", Icons.Material.Rounded.FileDownload, "Agents"),
        new("Architecture", "/architecture", Icons.Material.Rounded.AccountTree, "Agents"),
        new("Settings", "/settings", Icons.Material.Rounded.Settings, "Account"),
    ];

    /// <summary>
    /// Bottom navigation on phones (max 4–5 hrefs from <see cref="Nav"/>; the menu button still shows everything).
    /// Empty = no bottom bar.
    /// </summary>
    public static readonly string[] MobileNav = ["/dashboard", "/assistant", "/runs", "/settings"];

    /// <summary>
    /// Installable app (PWA): "Install" in Chrome/Edge (desktop + Android), "Add to Home Screen" on iPhone.
    /// Opens in its own window with this name and icon. Icons: wwwroot/icons (replace them with your logo).
    /// </summary>
    public static readonly Pwa Pwa = new(Enabled: true, ShortName: Name, StartUrl: "/dashboard", BackgroundColor: "#000000");

    /// <summary>Demo users replace login: pick one from the app bar. The first one is the default.</summary>
    public static readonly DemoUser[] Users =
    [
        new("admin", "Ana Admin", "ana@example.com", Roles.Admin, "#16a34a"),
        new("member", "Mihai Member", "mihai@example.com", Roles.Member, "#ea580c"),
        new("viewer", "Vera Viewer", "vera@example.com", Roles.Viewer, "#9333ea"),
    ];
}

public static class Roles
{
    public const string Admin = "admin";
    public const string Member = "member";
    public const string Viewer = "viewer";

    /// <summary>Roles that can create/edit data and approve agent actions.</summary>
    public const string Editors = Admin + "," + Member;
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

public record LandingLink(string Badge, string Label, string Href);

/// <summary>The static app preview under the hero: an item, its activity and the agent panel.</summary>
public record LandingPreview(string ItemId, string ItemTitle, string ItemDescription, string[] Activity,
                             string Prompt, string Reply, string ResultTitle, string ResultDetail);

/// <summary>Two-tone text: <see cref="Lead"/> in full color, <see cref="Rest"/> dimmed.</summary>
public record LandingStatement(string Lead, string Rest);

public record LandingSection(string Title, string Description, LandingFeature[] Features);

public record LandingStep(string Title, string Description);

public record Landing(bool Enabled, string Headline, string Subheadline, string CtaLabel, string CtaHref,
                      LandingLink? Announcement, LandingPreview Preview, LandingStatement Statement,
                      LandingSection[] Sections, LandingStep[] Steps, LandingStatement Closing);
