using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Changes;

public interface IChangeService
{
    /// <summary>Saves the change, applies it, evaluates the rules (code), saves the evaluation, opens a case if needed, writes the audit log.</summary>
    Task<ChangeResult> RecordAsync(ChangeRequest request);
    Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50);
}

/// <summary>Phase 0 placeholder. Owner: R2.</summary>
public class ChangeService : IChangeService
{
    public Task<ChangeResult> RecordAsync(ChangeRequest request) =>
        throw new NotImplementedException("ChangeService.RecordAsync arrives with R2 (phase 1).");

    public Task<List<ChangeEvent>> ListAsync(string? clientCode = null, int take = 50) => Task.FromResult(new List<ChangeEvent>());
}

public static class ChangesSetup
{
    public static IServiceCollection AddChanges(this IServiceCollection services) =>
        services.AddScoped<IChangeService, ChangeService>();
}

public class ChangeEventConfiguration : IEntityTypeConfiguration<ChangeEvent>
{
    public void Configure(EntityTypeBuilder<ChangeEvent> e)
    {
        e.Property(c => c.Kind).HasConversion<string>();
        e.HasIndex(c => c.ClientId);
        e.HasIndex(c => c.ReceivedAt);
    }
}

public class RuleEvaluationConfiguration : IEntityTypeConfiguration<RuleEvaluation>
{
    public void Configure(EntityTypeBuilder<RuleEvaluation> e)
    {
        e.Property(r => r.Decision).HasConversion<string>();
        e.HasIndex(r => r.ChangeEventId);
    }
}
