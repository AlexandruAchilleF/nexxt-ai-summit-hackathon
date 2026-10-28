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
    public const string Name = "Acme";
    public static string Description => Lang.Pick(
        ro: "O propoziție scurtă despre ce face aplicația.",
        en: "A short sentence about what your app does.");
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
        Headline: "Ideea voastră, într-o singură propoziție",
        Subheadline: "Spuneți pentru cine e și ce se schimbă pentru ei. Cel mult două rânduri.",
        CtaLabel: "Deschide aplicația",
        CtaHref: "/dashboard",
        Announcement: new("Nou", "Scenarii cu agenți", "/scenarios"),
        Preview: new(
            ItemId: "REQ-1042",
            ItemTitle: "Verifică o cerere",
            ItemDescription: "Agentul citește documentele atașate, verifică cifrele în cod și cere aprobare înainte să schimbe ceva.",
            Activity:
            [
                "Ana a atașat 2 documente",
                "Agentul a extras 6 câmpuri · 4 sec",
                "Verificarea pii-leak a trecut",
            ],
            Prompt: "Verifică cererea și spune-mi ce trebuie văzut de un om",
            Reply: "Cinci câmpuri se potrivesc cu evidențele. Unul trebuie văzut de un om: documentul a expirat.",
            ResultTitle: "Necesită aprobare",
            ResultDetail: "Actualizare înregistrare · 1 câmp"),
        Statement: new("O frază scurtă despre ce ați construit.",
            "Continuați cu de ce contează, cine plătește și de ce poate fi crezut. Două-trei rânduri, nu mai mult."),
        Sections:
        [
            new("Agenți care întreabă întâi",
                "Agenții folosesc unelte reale, arată fiecare pas și așteaptă un om înainte ca ceva să iasă din aplicație.",
                [
                    new(Icons.Material.Rounded.Build, "Unelte", "Unelte tipizate cu rezultate mici; modelul nu face niciodată calcule."),
                    new(Icons.Material.Rounded.HowToReg, "Aprobări", "Orice are efect în afara aplicației așteaptă un editor."),
                    new(Icons.Material.Rounded.History, "Istoricul rulărilor", "Fiecare pas, apel de unealtă și decizie e salvat, mascat."),
                    new(Icons.Material.Rounded.Forum, "Scenarii", "Conversații pe mai multe replici cu un sistem extern."),
                ]),
            new("Confidențialitate din proiectare",
                "Datele personale sunt mascate înainte să ajungă la un model și înainte de orice salvare. Ștergerea e automată.",
                [
                    new(Icons.Material.Rounded.Shield, "Mascare", "IBAN, CNP, carduri și emailuri devin înlocuitori."),
                    new(Icons.Material.Rounded.Rule, "Verificări deterministe", "Verdicte repetabile, cu amprentă."),
                    new(Icons.Material.Rounded.FileDownload, "Exporturi", "Rulările devin teste și rapoarte după aprobare."),
                    new(Icons.Material.Rounded.Timer, "Retenție", "Rulările vechi se șterg după un program."),
                ]),
        ],
        Steps:
        [
            new("Conectezi", "Legi aplicația de un model și, opțional, de un sistem extern."),
            new("Rulezi", "Agenții lucrează cu unelte; acțiunile riscante așteaptă aprobare."),
            new("Verifici", "Reguli în cod decid trecut sau picat, la fel de fiecare dată."),
            new("Exporți", "Rulările aprobate devin teste și rapoarte."),
        ],
        Closing: new("Construit pentru demo.", "Gata de producție."));

    private static readonly Landing LandingEn = new(
        Enabled: true,
        Headline: "Your big idea, one sentence long",
        Subheadline: "Explain who it is for and what changes for them. Keep it under two lines.",
        CtaLabel: "Open the app",
        CtaHref: "/dashboard",
        Announcement: new("New", "Agent scenarios", "/scenarios"),
        Preview: new(
            ItemId: "REQ-1042",
            ItemTitle: "Review a request",
            ItemDescription: "The agent reads the attached documents, checks the numbers in code and asks before changing anything.",
            Activity:
            [
                "Ana attached 2 documents",
                "Agent extracted 6 fields · 4 sec",
                "Check pii-leak passed",
            ],
            Prompt: "Check the request and tell me what needs a human",
            Reply: "Five fields match the records. One needs a human: the document has expired.",
            ResultTitle: "Approval needed",
            ResultDetail: "Update record · 1 field"),
        Statement: new("A short statement of what you built.",
            "Continue with why it matters, who pays for it and what makes it trustworthy. Two or three lines at most."),
        Sections:
        [
            new("Agents that ask first",
                "Agents call real tools, show every step and wait for a human before anything leaves the app.",
                [
                    new(Icons.Material.Rounded.Build, "Tools", "Typed tools with small results; the model never does arithmetic."),
                    new(Icons.Material.Rounded.HowToReg, "Approvals", "Anything with an effect outside the app waits for an editor."),
                    new(Icons.Material.Rounded.History, "Run history", "Every step, tool call and decision is saved, masked."),
                    new(Icons.Material.Rounded.Forum, "Scenarios", "Multi-turn conversations against an external system."),
                ]),
            new("Private by design",
                "Personal data is masked before it reaches a model and before anything is saved. Retention is automatic.",
                [
                    new(Icons.Material.Rounded.Shield, "Masking", "IBAN, CNP, cards and emails become placeholders."),
                    new(Icons.Material.Rounded.Rule, "Deterministic checks", "Repeatable verdicts with a fingerprint."),
                    new(Icons.Material.Rounded.FileDownload, "Exports", "Runs become tests and reports after approval."),
                    new(Icons.Material.Rounded.Timer, "Retention", "Old runs are deleted on a schedule."),
                ]),
        ],
        Steps:
        [
            new("Connect", "Point the app at a model and, optionally, an external system."),
            new("Run", "Agents work with tools; risky actions wait for approval."),
            new("Check", "Rules in code decide pass or fail, the same way every time."),
            new("Export", "Approved runs become tests and reports."),
        ],
        Closing: new("Built for the demo.", "Ready for production."));

    public static readonly NavItem[] Nav =
    [
        new("Dashboard", "/dashboard", Icons.Material.Rounded.SpaceDashboard),
        new("Items", "/items", Icons.Material.Rounded.Checklist),
        // @gen:nav (the feature generator inserts new pages above this line)
        new("Calculators", "/calculators", Icons.Material.Rounded.Calculate, "Finance"),
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
    public static readonly string[] MobileNav = ["/dashboard", "/items", "/assistant", "/calculators"];

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
