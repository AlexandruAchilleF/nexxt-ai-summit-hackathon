using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Data;
using HackathonStarter.Features.Arena;
using HackathonStarter.Features.Assistant;
using HackathonStarter.Features.Checks;
using HackathonStarter.Features.Exports;
using HackathonStarter.Features.Items;
using HackathonStarter.Features.Scenarios;
using HackathonStarter.Features.Targets;
using HackathonStarter.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace HackathonStarter.Tests.Support;

/// <summary>The real agent runtime + services wired like Program.cs, with a temp SQLite DB and a fake model.</summary>
public sealed class TestHost : IAsyncDisposable
{
    private readonly string _dir;
    private readonly ServiceProvider _root;
    private readonly AsyncServiceScope _scope;

    private TestHost(string dir, ServiceProvider root, DemoUser user)
    {
        _dir = dir;
        _root = root;
        _scope = root.CreateAsyncScope();
        _scope.ServiceProvider.GetRequiredService<CurrentUser>().Use(user);
    }

    public IServiceProvider Services => _scope.ServiceProvider;
    public IServiceProvider Root => _root;
    public AiOptions Ai { get; private init; } = new();
    public string RecordingsDir => Path.Combine(_dir, "Recordings");
    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public static async Task<TestHost> CreateAsync(
        IChatClient? client,
        Action<AiOptions>? ai = null,
        Action<SecurityOptions>? security = null,
        DemoUser? user = null,
        IReadOnlyDictionary<string, IChatClient>? profiles = null,
        Action<PrivacyOptions>? privacy = null,
        Action<Dictionary<string, TargetOptions>>? targets = null,
        params AgentDefinition[] extraAgents)
    {
        var dir = Path.Combine(Path.GetTempPath(), "hs-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        var aiOptions = new AiOptions { Provider = "Fake", Model = "fake-model", TimeoutSeconds = 30 };
        ai?.Invoke(aiOptions);
        var securityOptions = new SecurityOptions();
        security?.Invoke(securityOptions);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Options.Create(aiOptions));
        services.AddSingleton(Options.Create(securityOptions));
        services.Configure<PrivacyOptions>(o => privacy?.Invoke(o));
        services.AddSingleton(ChatClientProvider.Fixed(aiOptions, client, profiles));
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<PiiSealer>();
        services.AddChecks();
        services.AddSingleton<IRunExporter, JsonTestCaseExporter>();
        services.AddSingleton<IRunExporter, XunitTestExporter>();
        services.AddSingleton<IRunExporter, MarkdownReportExporter>();
        services.AddScoped<RunExportService>();
        services.Configure<Dictionary<string, TargetOptions>>(t =>
        {
            t["assistant"] = new TargetOptions { Kind = "Agent", Agent = "assistant", Label = "Built-in assistant" };
            targets?.Invoke(t);
        });
        services.AddHttpClient("targets");
        services.AddSingleton<TargetRecordings>();
        services.AddSingleton<TargetClient>();
        services.AddScoped<ConnectionCheck>();
        services.AddSingleton<RetentionService>();
        services.AddSingleton<AgentDefinition, CustomerProbeScenario>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AgentRateLimiter>();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment(dir));
        services.AddSingleton<Recordings>();
        services.AddDbContextFactory<AppDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(dir, "test.db")}"));
        services.AddScoped<RunStore>();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ItemService>();
        services.AddScoped<DemoData>();
        services.AddScoped<AgentRunner>();
        services.AddSingleton<AgentRunService>();
        services.AddSingleton<AgentDefinition, AssistantAgent>();
        services.AddSingleton<AgentDefinition, DebatePanel>();
        foreach (var agent in extraAgents) services.AddSingleton(agent);

        var root = services.BuildServiceProvider();
        await using (var db = await root.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync())
        {
            await db.Database.EnsureCreatedAsync();
            db.Items.AddRange(ItemSeed.Create());
            await db.SaveChangesAsync();
        }
        return new TestHost(dir, root, user ?? SiteConfig.Users[0]) { Ai = aiOptions };
    }

    public AgentDefinition Agent(string name) => Get<IEnumerable<AgentDefinition>>().First(a => a.Name == name);

    public Task<AgentRun> RunAsync(string agent, string message, IReadOnlyList<AgentFile>? files = null, CancellationToken ct = default) =>
        Get<AgentRunner>().RunAsync(Agent(agent), [new ChatTurn("user", message)], files ?? [], _ => Task.CompletedTask, ct);

    /// <summary>Writes a recording file, as AI:Record would.</summary>
    public void AddRecording(string agent, string input, params AgentStep[] steps)
    {
        Directory.CreateDirectory(RecordingsDir);
        var list = new[] { new Recordings.Recording(input, [.. steps], DateTime.UtcNow) };
        File.WriteAllText(Path.Combine(RecordingsDir, $"{agent}.json"), JsonSerializer.Serialize(list, RunStore.Json));
    }

    public async ValueTask DisposeAsync()
    {
        await _scope.DisposeAsync();
        await _root.DisposeAsync();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private sealed class FakeEnvironment(string dir) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "HackathonStarter";
        public string ContentRootPath { get; set; } = dir;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
