using System.Text.RegularExpressions;
using HackathonStarter.Features.Finance;
using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;

namespace HackathonStarter.Tests.Crs;

/// <summary>Foreign TINs, phones, IBANs and ABA numbers from <see cref="SyntheticData"/>, plus every value in GOLDEN-CASES.md.</summary>
public class SyntheticDataTests
{
    private const int Seeds = 1000;

    public static TheoryData<string> TinCountries => [.. SyntheticData.TinCountries];
    public static TheoryData<string> PhoneCountries => [.. SyntheticData.PhoneCountries];
    public static TheoryData<string> IbanCountries => [.. SyntheticData.IbanCountries];

    [Theory]
    [MemberData(nameof(TinCountries))]
    public void Tins_have_a_valid_format_on_1000_seeds(string country)
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var tin = new SyntheticData(seed).Tin(country);
            Assert.True(ForeignFormats.IsValidTin(country, tin), $"{country} seed {seed}: {tin}");
        }
    }

    [Fact]
    public void Spanish_dni_and_us_reserved_range()
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var data = new SyntheticData(seed);
            var dni = data.EsTin(foreigner: false);
            Assert.Matches(@"^\d{8}[A-Z]$", dni);
            Assert.True(ForeignFormats.IsValidTin("ES", dni), dni);
            Assert.Matches("^98765432[0-9]$", data.UsTin());
        }
    }

    [Theory]
    [MemberData(nameof(TinCountries))]
    public void Wrong_tins_fail_on_1000_seeds(string country)
    {
        for (var seed = 0; seed < Seeds; seed++)
            foreach (var flaw in Enum.GetValues<TinFlaw>())
            {
                var tin = new SyntheticData(seed).WrongTin(country, flaw);
                Assert.False(ForeignFormats.IsValidTin(country, tin), $"{country} {flaw} seed {seed}: {tin}");
            }
    }

    [Theory]
    [MemberData(nameof(PhoneCountries))]
    public void Phones_are_e164_on_1000_seeds(string country)
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var phone = new SyntheticData(seed).PhoneE164(country);
            Assert.True(ForeignFormats.IsValidPhone(country, phone), $"{country} seed {seed}: {phone}");
            if (country == "US") Assert.Matches(@"^\+1\d{3}55501\d{2}$", phone);
        }
    }

    [Theory]
    [MemberData(nameof(IbanCountries))]
    public void Ibans_pass_mod97_and_the_national_check_on_1000_seeds(string country)
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var iban = new SyntheticData(seed).ForeignIban(country);
            var check = Iban.Validate(iban);
            Assert.True(check.IsValid, $"{country} seed {seed}: {iban} {check.Error}");
            Assert.Equal(country, check.Country);
            Assert.True(ForeignFormats.HasValidNationalCheck(iban), $"{country} seed {seed}: {iban}");
        }
    }

    [Fact]
    public void Aba_routing_numbers_on_1000_seeds()
    {
        for (var seed = 0; seed < Seeds; seed++)
        {
            var aba = new SyntheticData(seed).AbaRoutingNumber();
            Assert.True(ForeignFormats.IsValidAba(aba), $"seed {seed}: {aba}");
        }
    }

    [Fact]
    public void Same_seed_same_values_other_seed_other_values()
    {
        static string All(SyntheticData d) => string.Join('|',
            SyntheticData.TinCountries.Select(d.Tin).Concat(SyntheticData.PhoneCountries.Select(d.PhoneE164))
                .Concat(SyntheticData.IbanCountries.Select(d.ForeignIban)).Append(d.AbaRoutingNumber())
                .Append(d.WrongTin("IT")).Append(d.WrongTin("DE", TinFlaw.WrongCheck)));

        Assert.Equal(All(new SyntheticData(5)), All(new SyntheticData(5)));
        Assert.NotEqual(All(new SyntheticData(5)), All(new SyntheticData(6)));
    }

    [Fact]
    public void Check_functions_match_values_accepted_by_tin_on_europa()
    {
        // Each full value got syntaxValid = true from TIN on Europa (2026-10-28); one changed digit got false.
        Assert.Equal(1, SyntheticData.Mod11_10CheckDigit("1234567891"));         // DE 12345678911
        Assert.Equal(3, SyntheticData.Mod11_10CheckDigit("8609517422"));         // DE 86095174223 (case 4)
        Assert.Equal('T', SyntheticData.ItalianControlCharacter("DBRCST90A41Z129")); // IT, case 2
        Assert.Equal('Z', SyntheticData.SpanishControlLetter("12345678"));       // ES 12345678Z
        Assert.Equal('L', SyntheticData.SpanishControlLetter("X1234567"));       // ES X1234567L
        Assert.Equal('X', SyntheticData.SpanishControlLetter("Y1234567"));       // ES Y1234567X
        Assert.Equal('R', SyntheticData.SpanishControlLetter("Z1234567"));       // ES Z1234567R
        Assert.Equal(53, SyntheticData.FrenchTinKey("3023217600"));              // FR 3023217600053
        Assert.Equal(2, SyntheticData.LuhnCheckDigit("12345678"));               // AT 123456782
        Assert.Equal(5, SyntheticData.LuhnCheckDigit("52601815"));               // AT 526018155
    }

    [Fact]
    public void National_iban_checks_match_published_examples()
    {
        Assert.Equal("IT60X0542811101000000123456", SyntheticData.IbanWithCheckDigits("IT", SyntheticData.ItalianBban("05428", "11101", "000000123456")));
        Assert.Equal("ES9121000418450200051332", SyntheticData.IbanWithCheckDigits("ES", SyntheticData.SpanishBban("2100", "0418", "0200051332")));
        Assert.Equal("FR7630006000011234567890189", SyntheticData.IbanWithCheckDigits("FR", SyntheticData.FrenchBban("30006", "00001", "12345678901")));
    }

    [Fact]
    public void Codice_fiscale_of_case_2_and_its_wrong_variant()
    {
        var valid = SyntheticData.CodiceFiscale("Dobre", "Cristina", new DateOnly(1990, 1, 1), male: false);

        Assert.Equal("DBRCST90A41Z129T", valid);
        Assert.True(ForeignFormats.IsValidTin("IT", valid));
        Assert.False(ForeignFormats.IsValidTin("IT", "DBRCST90A41Z129"));
        Assert.Equal("MRSNIO88D51Z129", SyntheticData.CodiceFiscale("Mureșan", "Ioana", new DateOnly(1988, 4, 11), male: false)[..15]);
    }

    [Theory]
    [InlineData("2910608400319", "1991-06-08")] // 4 Maria Popescu
    [InlineData("1820214400458", "1982-02-14")] // 5 Radu Ionescu
    [InlineData("1950923350125", "1995-09-23")] // 8, 13 Andrei Pop
    [InlineData("2880411120231", "1988-04-11")] // 1, 15 Ioana Mureșan
    [InlineData("2900101405128", "1990-01-01")] // 2 Cristina Dobre
    [InlineData("1870522083340", "1987-05-22")] // 3 Bogdan Lazăr
    [InlineData("2930719400974", "1993-07-19")] // 6 Alexandra Vlad
    [InlineData("1790305082613", "1979-03-05")] // 7 Marius Stoica
    [InlineData("1840930081451", "1984-09-30")] // 9 Sorin Matei
    [InlineData("1760812403886", "1976-08-12")] // 10 Victor Neagu
    [InlineData("2580117222041", "1958-01-17")] // 11 Doina Cazacu
    [InlineData("1610403121731", "1961-04-03")] // 12 Petru Sârbu
    [InlineData("1910226354122", "1991-02-26")] // 14 Lucian Toma
    [InlineData("2860614402290", "1986-06-14")] // 16, 17a Irina Constantin
    [InlineData("1670921403155", "1967-09-21")] // 17b Gheorghe Marin
    [InlineData("6040502401181", "2004-05-02")] // 18 Teodora Iliescu
    public void Golden_cnps_are_valid_and_match_the_birth_date(string cnp, string birth)
    {
        Assert.True(PiiVault.IsValidCnp(cnp), cnp);
        Assert.True(ForeignFormats.IsValidTin("RO", cnp), cnp);
        var century = cnp[0] is '5' or '6' ? 2000 : 1900;
        var date = new DateOnly(century + int.Parse(cnp[1..3]), int.Parse(cnp[3..5]), int.Parse(cnp[5..7]));
        Assert.Equal(DateOnly.Parse(birth), date);
    }

    [Theory]
    [InlineData("RO19RZBR0000060021458391")] // 4
    [InlineData("RO46RZBR0000060019823746")] // 5
    [InlineData("RO88RZBR0000069004471852")] // 5b
    [InlineData("DE02700100800918273645")]   // 5a, 5b beneficiary
    [InlineData("RO20RZBR0000060033317205")] // 13
    [InlineData("RO43RZBR0000060007719364")] // 15
    [InlineData("RO59RZBR0000060041172630")] // 2
    [InlineData("RO57RZBR0000060052208841")] // 3
    [InlineData("RO34RZBR0000060063391027")] // 6
    [InlineData("RO76RZBR0000060074415582")] // 7
    [InlineData("RO87RZBR0000060085527316")] // 9
    [InlineData("RO84RZBR0000060096630475")] // 10
    [InlineData("RO56RZBR0000060107741068")] // 11
    [InlineData("RO20RZBR0000060118852290")] // 12
    [InlineData("RO27RZBR0000060129963514")] // 14
    [InlineData("RO06RZBR0000060131074639")] // 16
    [InlineData("RO03RZBR0000060142185752")] // 17b
    [InlineData("RO38RZBR0000060153296860")] // 18
    public void Golden_ibans_are_valid(string iban) => Assert.True(Iban.Validate(iban).IsValid, iban);

    [Theory]
    [InlineData("DE", "86095174223")]      // 4 Maria, after the answer
    [InlineData("DE", "47302619546")]      // 9 Sorin
    [InlineData("IT", "DBRCST90A41Z129T")] // 2, the valid variant
    public void Golden_foreign_tins_have_a_valid_format(string country, string tin) =>
        Assert.True(ForeignFormats.IsValidTin(country, tin), $"{country} {tin}");

    [Fact]
    public void Golden_aba_routing_number_is_valid() => Assert.True(ForeignFormats.IsValidAba("073909523")); // 5c

    [Theory]
    [InlineData("RO", "+40721384516")]  // 4 Maria, before
    [InlineData("DE", "+498945217730")] // 4 Maria, after (München landline)
    [InlineData("RO", "+40745210963")]  // 13 Andrei
    [InlineData("DE", "+4915144201187")] // 13 Andrei, German SIM
    [InlineData("RO", "+40744518207")]  // 1, 15 Ioana
    [InlineData("RO", "+40733902614")]  // 1 Ioana, new number
    [InlineData("RO", "+40726447031")]  // 9 Sorin
    [InlineData("RO", "+40752318640")]  // 14 Lucian
    [InlineData("US", "+13125550147")]  // 14 Lucian, 555-01xx
    [InlineData("RO", "+40723655190")]  // 16 Irina
    [InlineData("RO", "+40722180944")]  // 17b Gheorghe
    [InlineData("RO", "+40731904226")]  // 18 Teodora
    public void Golden_phones_are_e164(string country, string phone) =>
        Assert.True(ForeignFormats.IsValidPhone(country, phone), $"{country} {phone}");
}

/// <summary>
/// Format checks written apart from the generators (the reference R3's TinFormat should agree with). "Format valid"
/// only: none of this says a TIN, IBAN or phone number exists.
/// </summary>
internal static partial class ForeignFormats
{
    public static bool IsValidTin(string country, string tin) => country switch
    {
        "RO" => PiiVault.IsValidCnp(tin),
        "DE" => IsValidDe(tin),
        "IT" => IsValidIt(tin),
        "ES" => EsRegex().IsMatch(tin) && "TRWAGMYFPDXBNJZSQVHLCKE"[int.Parse(tin[..^1].Replace('X', '0').Replace('Y', '1').Replace('Z', '2')) % 23] == tin[^1],
        "FR" => Regex.IsMatch(tin, "^[0-3][0-9]{12}$") && long.Parse(tin[..10]) % 511 == int.Parse(tin[10..]),
        "AT" => Regex.IsMatch(tin, "^[0-9]{9}$") && LuhnSum(tin) % 10 == 0,
        "US" => Regex.IsMatch(tin, "^9[0-9]{8}$"),
        _ => false,
    };

    private static bool IsValidDe(string tin)
    {
        if (!Regex.IsMatch(tin, "^[1-9][0-9]{10}$")) return false;
        var counts = tin[..10].GroupBy(c => c).Select(g => g.Count()).ToList();
        if (counts.Count(n => n > 1) != 1 || counts.Max() > 3) return false;
        if (Regex.IsMatch(tin[..10], @"(\d)\1\1")) return false;
        var product = 10;
        foreach (var c in tin[..10])
        {
            var sum = (c - '0' + product) % 10;
            product = (sum == 0 ? 10 : sum) * 2 % 11;
        }
        return (11 - product) % 10 == tin[10] - '0';
    }

    private static bool IsValidIt(string tin)
    {
        if (!ItRegex().IsMatch(tin)) return false;
        var day = int.Parse(tin[9..11]);
        if (day is < 1 or > 71 or > 31 and < 41) return false;
        const string odd = "BAKPLCQDREVOSFTGUHMINJWZYX"; // letters ordered by their odd-position value 0..25
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            var value = char.IsDigit(tin[i]) ? tin[i] - '0' : tin[i] - 'A';
            sum += i % 2 == 0 ? odd.IndexOf((char)('A' + value)) : value;
        }
        return tin[15] == 'A' + sum % 26;
    }

    public static bool IsValidPhone(string country, string phone) =>
        Regex.IsMatch(phone, @"^\+[1-9][0-9]{6,14}$") && country switch
        {
            "RO" => Regex.IsMatch(phone, @"^\+40[237][0-9]{8}$"),
            "DE" => Regex.IsMatch(phone, @"^\+49[1-9][0-9]{6,12}$"),
            "AT" => Regex.IsMatch(phone, @"^\+43[1-9][0-9]{3,12}$"),
            "IT" => Regex.IsMatch(phone, @"^\+39[0-9]{6,11}$"),
            "ES" => Regex.IsMatch(phone, @"^\+34[6-9][0-9]{8}$"),
            "FR" => Regex.IsMatch(phone, @"^\+33[1-9][0-9]{8}$"),
            "US" => Regex.IsMatch(phone, @"^\+1[2-9][0-9]{2}[2-9][0-9]{6}$"),
            _ => false,
        };

    public static bool HasValidNationalCheck(string iban) => iban[..2] switch
    {
        "IT" => SyntheticData.ItalianControlCharacter(iban[5..]) == iban[4],
        "ES" => $"{SyntheticData.SpanishControlDigit("00" + iban[4..12])}{SyntheticData.SpanishControlDigit(iban[14..])}" == iban[12..14],
        "FR" => SyntheticData.RibKey(iban[4..9], iban[9..14], iban[14..25]) == int.Parse(iban[25..]),
        _ => true,
    };

    public static bool IsValidAba(string aba) =>
        Regex.IsMatch(aba, "^(0[1-9]|1[0-2])[0-9]{7}$")
        && (3 * (aba[0] + aba[3] + aba[6] - 3 * '0') + 7 * (aba[1] + aba[4] + aba[7] - 3 * '0') + (aba[2] + aba[5] + aba[8] - 3 * '0')) % 10 == 0;

    // Luhn over the whole number (check digit included): doubles every second digit from the right.
    private static int LuhnSum(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var d = digits[^(i + 1)] - '0';
            sum += i % 2 == 1 ? (d * 2 > 9 ? d * 2 - 9 : d * 2) : d;
        }
        return sum;
    }

    [GeneratedRegex("^([0-9]{8}|[XYZ][0-9]{7})[A-Z]$")]
    private static partial Regex EsRegex();

    [GeneratedRegex("^[A-Z]{6}[0-9]{2}[ABCDEHLMPRST][0-9]{2}[A-Z][0-9]{3}[A-Z]$")]
    private static partial Regex ItRegex();
}
