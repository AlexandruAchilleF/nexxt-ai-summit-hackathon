namespace HackathonStarter.Components.Crs;

/// <summary>
/// The Demo menu's view of the page being shown (one per circuit, cascaded by MainLayout). The demo shortcuts live in one
/// place, the top-bar menu: a page offers its own (jump to this case's reminder day, fill the sample answer, tamper mode)
/// by setting <see cref="Actions"/>, and clears them when it goes away. The operator's pages show only the bank's workflow.
/// </summary>
public sealed class DemoContext
{
    public sealed record DemoAction(string Label, string Icon, Func<Task> Run, string? Hint = null, bool Destructive = false);

    private object? _owner;

    /// <summary>A heading for the page's actions, e.g. "Case CRS-2026-0001".</summary>
    public string? Title { get; private set; }
    public IReadOnlyList<DemoAction> Actions { get; private set; } = [];

    /// <summary>Raised when a page sets or clears its actions, so the menu re-renders.</summary>
    public event Action? Changed;

    public void Offer(object owner, string title, IReadOnlyList<DemoAction> actions)
    {
        _owner = owner;
        Title = title;
        Actions = actions;
        Changed?.Invoke();
    }

    /// <summary>Clears the actions only if <paramref name="owner"/> set them (the next page may already have offered its own).</summary>
    public void Withdraw(object owner)
    {
        if (!ReferenceEquals(_owner, owner)) return;
        _owner = null;
        Title = null;
        Actions = [];
        Changed?.Invoke();
    }
}
