namespace HackathonStarter.Features.Items;

/// <summary>Demo data, inserted when the table is empty. Realistic demo data sells the demo: replace it.</summary>
public static class ItemSeed
{
    public static IEnumerable<Item> Create()
    {
        var rows = new (string Title, string Description, ItemStatus Status, ItemPriority Priority, decimal Value, string Owner)[]
        {
            ("Launch landing page", "Hero, features and CTA from SiteConfig.", ItemStatus.Active, ItemPriority.High, 1200, "admin"),
            ("Connect database", "Switch to PostgreSQL with one setting.", ItemStatus.Active, ItemPriority.High, 800, "admin"),
            ("Design onboarding flow", "Three screens, skippable.", ItemStatus.Draft, ItemPriority.Medium, 650, "member"),
            ("Integrate payments", "Mock checkout for the demo.", ItemStatus.Draft, ItemPriority.Low, 300, "member"),
            ("Write demo script", "Two-minute happy path, rehearsed twice.", ItemStatus.Active, ItemPriority.High, 0, "admin"),
            ("Add analytics events", "Track the three key actions.", ItemStatus.Draft, ItemPriority.Low, 150, "member"),
            ("Prepare seed data", "Realistic names and numbers.", ItemStatus.Active, ItemPriority.Medium, 400, "member"),
            ("Mobile layout pass", "Check every page on a phone.", ItemStatus.Active, ItemPriority.Medium, 500, "admin"),
            ("Old prototype", "Superseded by the new flow.", ItemStatus.Draft, ItemPriority.Low, 200, "viewer"),
            ("AI summary feature", "Structured output with a mock fallback.", ItemStatus.Active, ItemPriority.High, 1500, "admin"),
            ("Notification emails", "Out of scope for the hackathon.", ItemStatus.Archived, ItemPriority.Low, 100, "member"),
            ("Deploy the app", "Docker image on Azure App Service.", ItemStatus.Draft, ItemPriority.Medium, 250, "admin"),
        };

        return rows.Select((r, i) =>
        {
            var created = DateTime.UtcNow.AddDays(-(i * 5 % 14));
            return new Item
            {
                Title = r.Title, Description = r.Description, Status = r.Status, Priority = r.Priority,
                Value = r.Value, OwnerId = r.Owner, CreatedAt = created, UpdatedAt = created,
            };
        });
    }
}
