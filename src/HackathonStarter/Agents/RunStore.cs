using System.Text.Json;
using System.Text.Json.Serialization;
using HackathonStarter.Data;
using HackathonStarter.Services;
using Microsoft.EntityFrameworkCore;

namespace HackathonStarter.Agents;

/// <summary>
/// Stores every run with all its steps: history page + audit trail + approval flow.
/// Personal data is masked before saving (<see cref="RunRedaction"/>): the database never holds a CNP, IBAN,
/// card number, email or phone number in clear, only one-way placeholders like [CNP-1].
/// </summary>
public class RunStore(IDbContextFactory<AppDbContext> dbFactory, PiiSealer sealer)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Shown (translated) on runs that were still executing when the server stopped.</summary>
    public const string InterruptedError = "The server restarted before this run finished.";

    /// <summary>Runs saved as Running whose process is gone (called at startup): marked as failed, not left spinning.</summary>
    public static Task<int> MarkInterruptedAsync(AppDbContext db) =>
        db.AgentRuns.Where(r => r.Status == nameof(RunStatus.Running)).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, nameof(RunStatus.Error))
            .SetProperty(r => r.Error, InterruptedError)
            .SetProperty(r => r.FinishedAt, DateTime.UtcNow));

    public async Task SaveAsync(AgentRun run)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.AgentRuns.FindAsync(run.Id);
        if (record is null)
        {
            record = new AgentRunRecord { Id = run.Id };
            db.AgentRuns.Add(record);
        }
        var vault = PiiVault.ForStorage(); // the request first, so its values get the lowest numbers
        record.ConversationId = run.ConversationId;
        record.Agent = run.Agent;
        record.UserId = run.UserId;
        record.Input = vault.Mask(run.Input);
        record.Status = run.Status.ToString();
        record.Source = run.Source.ToString();
        record.StepsJson = JsonSerializer.Serialize(RunRedaction.Redact(run.Snapshot(), vault, sealer), Json);
        record.Error = RunRedaction.Mask(run.Error, vault);
        record.ReplayOf = RunRedaction.Mask(run.ReplayOf, vault);
        record.FallbackReason = RunRedaction.Mask(run.FallbackReason, vault);
        record.Model = run.Model;
        record.InputTokens = run.InputTokens;
        record.OutputTokens = run.OutputTokens;
        record.EstimatedCost = run.EstimatedCost;
        record.MaskedCount = run.MaskedCount;
        record.CreatedAt = run.CreatedAt;
        record.FinishedAt = run.FinishedAt;
        await db.SaveChangesAsync();
    }

    public async Task<AgentRun?> GetAsync(string id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var record = await db.AgentRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        return record is null ? null : ToRun(record);
    }

    /// <summary>All runs of one chat, oldest first: used to restore a conversation after a reload.</summary>
    public async Task<List<AgentRun>> ListByConversationAsync(string conversationId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var records = await db.AgentRuns.AsNoTracking().Where(r => r.ConversationId == conversationId).ToListAsync();
        return records.OrderBy(r => r.CreatedAt).Select(ToRun).ToList();
    }

    public async Task<List<AgentRun>> ListAsync(int limit = 100)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var records = await db.AgentRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync();
        return records.Select(ToRun).ToList();
    }

    private static AgentRun ToRun(AgentRunRecord r) => new()
    {
        Id = r.Id,
        ConversationId = r.ConversationId,
        Agent = r.Agent,
        UserId = r.UserId,
        Input = r.Input,
        Status = Enum.Parse<RunStatus>(r.Status),
        Source = Enum.Parse<RunSource>(r.Source),
        Steps = JsonSerializer.Deserialize<List<AgentStep>>(r.StepsJson, Json) ?? [],
        Error = r.Error,
        ReplayOf = r.ReplayOf,
        FallbackReason = r.FallbackReason,
        Model = r.Model,
        InputTokens = r.InputTokens,
        OutputTokens = r.OutputTokens,
        EstimatedCost = r.EstimatedCost,
        MaskedCount = r.MaskedCount,
        CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
        FinishedAt = r.FinishedAt,
    };
}
