using HackathonStarter.Data;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Synthetic;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;

namespace HackathonStarter.Features.Sentinel.Interim;

/// <summary>
/// Maria Popescu (CL-0001) in her golden "Before" state (docs/GOLDEN-CASES.md, case 4), with her payslip. Synthetic data
/// only. Interim (docs/adr/0001): it stops mattering once R5's ClientSeed inserts CL-0001, because it never overwrites.
/// </summary>
public static class MariaSeed
{
    public const string ClientCode = "CL-0001";

    /// <param name="now">The demo date (ISimClock): the upload time of the payslip.</param>
    public static Client Create(DateTime now)
    {
        var payslip = SyntheticPdfs.Get(SyntheticPdfs.PayslipMaria).Bytes;
        var openedOn = new DateOnly(2019, 3, 14);
        var signedAt = new DateTime(2019, 3, 14, 9, 0, 0, DateTimeKind.Utc);
        return new Client
        {
            Code = ClientCode,
            FirstName = "Maria",
            LastName = "Popescu",
            BirthDate = new DateOnly(1991, 6, 8),
            BirthCountry = "RO",
            Cnp = "2910608400319",
            Citizenships = ["RO"],
            Addresses =
            [
                new Address { Kind = AddressKind.Residence, Line = "Bd. Unirii 15, bl. 7, ap. 22", City = "030823 București", Country = "RO", ValidFrom = openedOn },
            ],
            Phones = [new Phone { Number = "+40721384516", Country = "RO" }],
            Accounts =
            [
                new Account
                {
                    Iban = "RO19RZBR0000060021458391", Type = AccountType.Depository, Product = "Cont curent", OpenedOn = openedOn,
                    Balance = 18_450.00m, Currency = "RON",
                },
            ],
            SelfCertifications =
            [
                new SelfCertification
                {
                    SignedAt = signedAt, ReceivedAt = signedAt, Channel = "ghișeu", Status = SelfCertStatus.Valid,
                    Residences = [new TaxResidence { Country = "RO", Tin = "2910608400319" }],
                },
            ],
            Documents =
            [
                new ClientDocument
                {
                    Kind = DocumentKind.Payslip, FileName = SyntheticPdfs.PayslipMaria, Content = payslip, Pages = PageCount(payslip),
                    UploadedAt = DateTime.SpecifyKind(now, DateTimeKind.Utc),
                },
            ],
        };
    }

    /// <summary>Inserts Maria when there is no CL-0001 yet. True when it inserted her.</summary>
    public static async Task<bool> EnsureAsync(IDbContextFactory<AppDbContext> dbFactory, ISimClock clock, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        if (await db.Clients.AnyAsync(c => c.Code == ClientCode, ct)) return false;
        db.Clients.Add(Create(clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return true;
    }

    internal static int PageCount(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return document.NumberOfPages;
    }
}
