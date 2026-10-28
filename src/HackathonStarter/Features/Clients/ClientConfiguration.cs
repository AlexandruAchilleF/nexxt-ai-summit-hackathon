using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HackathonStarter.Features.Clients;

// EF configuration for the Clients module (picked up by ApplyConfigurationsFromAssembly in AppDbContext).

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> e)
    {
        e.HasIndex(c => c.Code).IsUnique();
        e.Ignore(c => c.FullName);
        e.HasMany(c => c.Addresses).WithOne().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Phones).WithOne().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Accounts).WithOne().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.PowersOfAttorney).WithOne().HasForeignKey(p => p.ClientId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.SelfCertifications).WithOne().HasForeignKey(s => s.ClientId).OnDelete(DeleteBehavior.Cascade);
        e.HasMany(c => c.Documents).WithOne().HasForeignKey(d => d.ClientId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> e) => e.Property(a => a.Kind).HasConversion<string>();
}

public class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> e)
    {
        e.Property(a => a.Type).HasConversion<string>();
        e.Property(a => a.Balance).HasPrecision(18, 2);
        e.HasMany(a => a.StandingOrders).WithOne().HasForeignKey(o => o.AccountId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StandingOrderConfiguration : IEntityTypeConfiguration<StandingOrder>
{
    public void Configure(EntityTypeBuilder<StandingOrder> e) => e.Property(o => o.Amount).HasPrecision(18, 2);
}

public class SelfCertificationConfiguration : IEntityTypeConfiguration<SelfCertification>
{
    public void Configure(EntityTypeBuilder<SelfCertification> e)
    {
        e.Property(s => s.Status).HasConversion<string>();
        e.OwnsMany(s => s.Residences, r =>
        {
            r.ToJson();
            r.Property(x => x.NoTinReason).HasConversion<string>();
        });
    }
}

public class ClientDocumentConfiguration : IEntityTypeConfiguration<ClientDocument>
{
    public void Configure(EntityTypeBuilder<ClientDocument> e)
    {
        e.Property(d => d.Kind).HasConversion<string>();
        e.Property(d => d.ShieldVerdict).HasConversion<string>();
    }
}
