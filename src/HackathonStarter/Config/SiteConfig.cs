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
    public const string Name = "DosarViu";
    public static string Description => Lang.Pick(
        ro: "Revizuirea KYC a firmelor: certificatul ONRC comparat cu dosarul, beneficiarii reali calculați în cod.",
        en: "Business KYC reviews: the ONRC certificate compared with the file, beneficial owners computed in code.");
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
        Headline: "Dosarul KYC al firmei, la zi cu fiecare certificat ONRC",
        Subheadline: "Agentul compară certificatul nou cu dosarul, calculează beneficiarii reali pe fiecare lanț de proprietate și pregătește actualizarea. Analistul aprobă.",
        CtaLabel: "Deschide aplicația",
        CtaHref: "/dashboard",
        Announcement: new("Nou", "Pragul AMLR din iulie 2027, deja configurabil", "/#how"),
        Preview: new(
            ItemId: "KYC-2041",
            ItemTitle: "Revizuire periodică: Alfa Logistic SRL",
            ItemDescription: "Certificatul ONRC nou diferă de dosar: un asociat nou, prin două firme intermediare.",
            Activity:
            [
                "Certificat ONRC încărcat (12 pagini)",
                "Agentul a extras 4 asociați și 2 administratori · 6 sec",
                "Diferențe față de dosar: 1 asociat nou, 1 cotă schimbată",
            ],
            Prompt: "Compară certificatul cu dosarul și calculează beneficiarii reali",
            Reply: "Maria Ionescu: 20% direct + 24% prin Beta Holding (60% × 40%) = 44%, beneficiar real. Elena Radu are exact 25% prin Gamma Invest: la limită, decide un om.",
            ResultTitle: "Necesită aprobare",
            ResultDetail: "Actualizare dosar · 1 beneficiar real, 1 caz la limită"),
        Statement: new("Beneficiarii reali se calculează în cod, nu se ghicesc.",
            "Fiecare procent e produsul cotelor de pe lanț, fiecare caz la limita de 25% ajunge la un om și fiecare decizie rămâne în audit. Modelul poate rula pe serverele băncii: datele nu pleacă din ea."),
        Sections:
        [
            new("Certificatul, citit și comparat",
                "Din PDF-ul ONRC rezultă asociați, cote și administratori, comparați câmp cu câmp cu ce știe deja banca.",
                [
                    new(Icons.Material.Rounded.DocumentScanner, "Extragere", "Certificatul ONRC devine câmpuri structurate, fiecare cu pagina din care provine."),
                    new(Icons.Material.Rounded.CompareArrows, "Diferențe în cod", "Asociați noi sau ieșiți, cote schimbate, administratori: comparația o face codul, nu modelul."),
                    new(Icons.Material.Rounded.HowToReg, "Aprobare", "Dosarul se actualizează doar după ce analistul confirmă."),
                    new(Icons.Material.Rounded.History, "Audit", "Cine a aprobat, ce s-a schimbat și pe ce bază rămân în istoric."),
                ]),
            new("Beneficiarul real, calculat",
                "Procentul indirect e produsul cotelor pe fiecare lanț, adunat pe toate lanțurile aceleiași persoane.",
                [
                    new(Icons.Material.Rounded.AccountTree, "Lanțuri de proprietate", "Oricâte firme intermediare, cu cicluri detectate și semnalate."),
                    new(Icons.Material.Rounded.Rule, "Pragul legal", "Peste 25% azi (Legea 129/2019), 25% sau mai mult din iulie 2027 (AMLR): o setare."),
                    new(Icons.Material.Rounded.Gavel, "Control fără cote", "Acorduri între asociați sau drept de veto: semnalate analistului, nu ignorate."),
                    new(Icons.Material.Rounded.SupervisorAccount, "Când nu există", "Fără beneficiar real identificat, regula legală: conducerea de rang superior."),
                ]),
        ],
        Steps:
        [
            new("Încarci certificatul", "PDF-ul ONRC al firmei, la onboarding sau la revizuirea periodică."),
            new("Agentul compară", "Extrage datele și le pune față în față cu dosarul existent."),
            new("Codul calculează", "Beneficiarii reali pe fiecare lanț; cazurile la limită sunt marcate."),
            new("Analistul aprobă", "Actualizarea dosarului pleacă doar cu acordul lui."),
        ],
        Closing: new("Agentul face copy-paste-ul.", "Analistul decide."));

    private static readonly Landing LandingEn = new(
        Enabled: true,
        Headline: "The company's KYC file, current with every ONRC certificate",
        Subheadline: "The agent compares the new certificate with the file, computes beneficial owners along every ownership chain and prepares the update. The analyst approves.",
        CtaLabel: "Open the app",
        CtaHref: "/dashboard",
        Announcement: new("New", "The July 2027 AMLR threshold, already a setting", "/#how"),
        Preview: new(
            ItemId: "KYC-2041",
            ItemTitle: "Periodic review: Alfa Logistic SRL",
            ItemDescription: "The new ONRC certificate differs from the file: a new shareholder, through two intermediate companies.",
            Activity:
            [
                "ONRC certificate uploaded (12 pages)",
                "Agent extracted 4 shareholders and 2 directors · 6 sec",
                "Differences from the file: 1 new shareholder, 1 changed stake",
            ],
            Prompt: "Compare the certificate with the file and compute the beneficial owners",
            Reply: "Maria Ionescu: 20% direct + 24% through Beta Holding (60% × 40%) = 44%, a beneficial owner. Elena Radu holds exactly 25% through Gamma Invest: borderline, a person decides.",
            ResultTitle: "Approval needed",
            ResultDetail: "Update file · 1 beneficial owner, 1 borderline case"),
        Statement: new("Beneficial owners are computed in code, not guessed.",
            "Every percentage is the product of the stakes along the chain, every case at the 25% line goes to a person and every decision stays in the audit trail. The model can run on the bank's own servers: the data never leaves."),
        Sections:
        [
            new("The certificate, read and compared",
                "Shareholders, stakes and directors come out of the ONRC PDF and are compared field by field with what the bank already knows.",
                [
                    new(Icons.Material.Rounded.DocumentScanner, "Extraction", "The ONRC certificate becomes structured fields, each with the page it came from."),
                    new(Icons.Material.Rounded.CompareArrows, "Differences in code", "New or departed shareholders, changed stakes, directors: code compares, not the model."),
                    new(Icons.Material.Rounded.HowToReg, "Approval", "The file is updated only after the analyst confirms."),
                    new(Icons.Material.Rounded.History, "Audit", "Who approved, what changed and on what basis stay in the history."),
                ]),
            new("The beneficial owner, computed",
                "The indirect percentage is the product of the stakes along each chain, summed over every chain of the same person.",
                [
                    new(Icons.Material.Rounded.AccountTree, "Ownership chains", "Any number of intermediate companies, with cycles detected and flagged."),
                    new(Icons.Material.Rounded.Rule, "The legal threshold", "Over 25% today (Romanian Law 129/2019), 25% or more from July 2027 (AMLR): one setting."),
                    new(Icons.Material.Rounded.Gavel, "Control without shares", "Shareholder agreements or veto rights: flagged to the analyst, not ignored."),
                    new(Icons.Material.Rounded.SupervisorAccount, "When there is none", "No beneficial owner found: the legal fallback is senior management."),
                ]),
        ],
        Steps:
        [
            new("Upload the certificate", "The company's ONRC PDF, at onboarding or at the periodic review."),
            new("The agent compares", "It extracts the data and puts it side by side with the existing file."),
            new("Code computes", "Beneficial owners along every chain; borderline cases are marked."),
            new("The analyst approves", "The file update goes out only with their consent."),
        ],
        Closing: new("The agent does the copy-paste.", "The analyst decides."));

    public static readonly NavItem[] Nav =
    [
        new("Dashboard", "/dashboard", Icons.Material.Rounded.SpaceDashboard),
        // Items and Calculators stay reachable by URL (tests use them) until the KYC pages replace them.
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
