// Scaffolds a new feature by copying the Items example.
//
//   dotnet run --project tools/NewFeature -- Project            → "Projects"
//   dotnet run --project tools/NewFeature -- Person People      → custom plural
//
// Creates Features/<Plural>/ and Components/Pages/<Plural>/, registers the service, the DbSet,
// the seed and a nav entry. Then edit Features/<Plural>/<Singular>.cs and follow the compiler errors.
using System.Text.RegularExpressions;

if (args.Length < 1 || !Regex.IsMatch(args[0], "^[A-Z][A-Za-z0-9]*$"))
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/NewFeature -- <Singular> [Plural]   (PascalCase, e.g. Invoice)");
    return 1;
}

var singular = args[0];
var plural = args.Length > 1 ? args[1] : singular + "s";
var root = FindRoot();
var app = Path.Combine(root, "src", "HackathonStarter");

var featureDir = Path.Combine(app, "Features", plural);
var pagesDir = Path.Combine(app, "Components", "Pages", plural);
if (Directory.Exists(featureDir) || Directory.Exists(pagesDir))
{
    Console.Error.WriteLine($"❌ {plural} already exists.");
    return 1;
}

// Identifiers that contain "Item"/"Items" but must NOT be renamed (MudBlazor/Blazor APIs).
string[] protectedTokens =
[
    "MudSelectItem", "MudMenuItem", "MudListItem", "MudNavItem", "MudItem", "MudTreeViewItem",
    "context.Item", "args.Item", "Items=\"", "AddRange", "TemplateColumn",
];

string Rename(string text)
{
    for (var i = 0; i < protectedTokens.Length; i++) text = text.Replace(protectedTokens[i], $"\u0001{i}\u0001");
    text = text
        .Replace("Items", plural).Replace("items", plural.ToLowerInvariant())
        .Replace("Item", singular).Replace("item", singular.ToLowerInvariant());
    for (var i = 0; i < protectedTokens.Length; i++) text = text.Replace($"\u0001{i}\u0001", protectedTokens[i]);
    return text;
}

void CopyDir(string from, string to)
{
    Directory.CreateDirectory(to);
    foreach (var file in Directory.GetFiles(from))
        File.WriteAllText(Path.Combine(to, Rename(Path.GetFileName(file))), Rename(File.ReadAllText(file)));
    foreach (var dir in Directory.GetDirectories(from))
        CopyDir(dir, Path.Combine(to, Rename(Path.GetFileName(dir))));
}

void InsertBefore(string relativePath, string marker, string line)
{
    var path = Path.Combine(app, relativePath);
    var text = File.ReadAllText(path);
    var idx = text.IndexOf(marker, StringComparison.Ordinal);
    if (idx < 0)
    {
        Console.WriteLine($"⚠️  Marker \"{marker}\" not found in {relativePath}. Add manually:\n   {line}");
        return;
    }
    var lineStart = text.LastIndexOf('\n', idx) + 1;
    var indent = text[lineStart..idx];
    File.WriteAllText(path, text[..lineStart] + indent + line + "\n" + text[lineStart..]);
}

void AddUsing(string relativePath, string ns)
{
    var path = Path.Combine(app, relativePath);
    var text = File.ReadAllText(path);
    if (!text.Contains($"using {ns};")) File.WriteAllText(path, $"using {ns};\n" + text);
}

CopyDir(Path.Combine(app, "Features", "Items"), featureDir);
CopyDir(Path.Combine(app, "Components", "Pages", "Items"), pagesDir);

var ns = $"HackathonStarter.Features.{plural}";
AddUsing("Data/AppDbContext.cs", ns);
InsertBefore("Data/AppDbContext.cs", "// @gen:dbsets", $"public DbSet<{singular}> {plural} => Set<{singular}>();");
InsertBefore("Data/AppDbContext.cs", "// @gen:seed",
    $"if (!await db.{plural}.AnyAsync()) {{ db.{plural}.AddRange({singular}Seed.Create()); await db.SaveChangesAsync(); }}");
AddUsing("Data/DemoData.cs", ns);
InsertBefore("Data/DemoData.cs", "// @gen:reset", $"await db.{plural}.ExecuteDeleteAsync(); db.{plural}.AddRange({singular}Seed.Create());");
AddUsing("Program.cs", ns);
InsertBefore("Program.cs", "// @gen:services", $"builder.Services.AddScoped<{singular}Service>();");
InsertBefore("Config/SiteConfig.cs", "// @gen:nav", $"new(\"{plural}\", \"/{plural.ToLowerInvariant()}\", Icons.Material.Outlined.Folder),");

Console.WriteLine($"""

    ✅ Created feature "{plural}"
       Features/{plural}/              entity, service, seed
       Components/Pages/{plural}/      list, detail, dialog
       + service, DbSet, seed and nav entry registered

    Next:
       1. Edit Features/{plural}/{singular}.cs (fields), then fix what the compiler points at
       2. Edit Features/{plural}/{singular}Seed.cs (demo data)
       3. Restart: in Development the database is recreated automatically
    """);
return 0;

static string FindRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Run this from inside the repository.");
}
