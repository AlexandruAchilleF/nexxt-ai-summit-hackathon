using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Cases;

// EF configuration for the Cases module (picked up by ApplyConfigurationsFromAssembly in AppDbContext).

public class CaseConfiguration : IEntityTypeConfiguration<Case>
{
    public void Configure(EntityTypeBuilder<Case> e)
    {
        e.HasIndex(c => c.Code).IsUnique();
        e.HasIndex(c => new { c.Status, c.DueAt });
        e.HasIndex(c => c.ClientId);
        e.HasIndex(c => c.ClientCode);
        e.Property(c => c.Status).HasConversion<string>();
        e.Property(c => c.Regime).HasConversion<string>();
        e.Ignore(c => c.IsOpen);
        e.HasMany(c => c.Indicia).WithOne().HasForeignKey(i => i.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Evidence).WithOne().HasForeignKey(x => x.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Signals).WithOne().HasForeignKey(s => s.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Requests).WithOne().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Responses).WithOne().HasForeignKey(r => r.CaseId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(c => c.Proof).WithOne().HasForeignKey<ConfirmationProof>(p => p.CaseId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class IndiciumConfiguration : IEntityTypeConfiguration<Indicium>
{
    public void Configure(EntityTypeBuilder<Indicium> e) => e.Property(i => i.Regime).HasConversion<string>();
}

public class EvidenceConfiguration : IEntityTypeConfiguration<Evidence>
{
    public void Configure(EntityTypeBuilder<Evidence> e)
    {
        e.Property(x => x.Source).HasConversion<string>();
        e.HasIndex(x => x.ClientId);
    }
}

public class ContextSignalConfiguration : IEntityTypeConfiguration<ContextSignal>
{
    public void Configure(EntityTypeBuilder<ContextSignal> e)
    {
        e.Property(s => s.Strength).HasConversion<string>();
        e.HasIndex(s => s.ClientId);
    }
}

public class ClientRequestConfiguration : IEntityTypeConfiguration<ClientRequest>
{
    public void Configure(EntityTypeBuilder<ClientRequest> e)
    {
        e.Property(r => r.Kind).HasConversion<string>();
        e.Property(r => r.Status).HasConversion<string>();
    }
}

public class ClientResponseConfiguration : IEntityTypeConfiguration<ClientResponse>
{
    public void Configure(EntityTypeBuilder<ClientResponse> e)
    {
        e.Property(r => r.Kind).HasConversion<string>();
        e.Property(r => r.Category).HasConversion<string>();
    }
}

public class ConfirmationProofConfiguration : IEntityTypeConfiguration<ConfirmationProof>
{
    public void Configure(EntityTypeBuilder<ConfirmationProof> e)
    {
        e.Property(p => p.Channel).HasConversion<string>();
        e.Property(p => p.EvidenceKind).HasConversion<string>();
        e.Property(p => p.SignerCapacity).HasConversion<string>();
    }
}
