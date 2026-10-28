using HackathonStarter.Features.Finance;

namespace HackathonStarter.Tests;

/// <summary>Financial formulas must be right: judges from banks will check the numbers.</summary>
public class FinanceTests
{
    [Fact]
    public void LoanPayment_matches_standard_annuity_formula()
    {
        // 100,000 at 6%/year over 30 years → 599.55/month (well-known reference value).
        Assert.Equal(599.55m, FinanceCalculator.LoanPayment(100_000m, 6m, 360));
    }

    [Fact]
    public void LoanPayment_with_zero_interest_is_principal_divided_by_months()
    {
        Assert.Equal(1000m, FinanceCalculator.LoanPayment(12_000m, 0m, 12));
    }

    [Fact]
    public void Amortization_schedule_pays_off_the_loan_exactly()
    {
        var schedule = FinanceCalculator.AmortizationSchedule(50_000m, 7.5m, 60);

        Assert.Equal(60, schedule.Count);
        Assert.Equal(0m, schedule[^1].Balance);
        Assert.Equal(50_000m, schedule.Sum(r => r.Principal));
        Assert.True(schedule[0].Interest > schedule[^1].Interest);
    }

    [Fact]
    public void Savings_projection_grows_with_compound_interest()
    {
        var points = FinanceCalculator.SavingsProjection(0m, 100m, 0m, 10);
        Assert.Equal(12_000m, points[^1].Balance); // no return: just the contributions

        var withReturn = FinanceCalculator.SavingsProjection(0m, 100m, 5m, 10);
        Assert.True(withReturn[^1].Balance > 15_000m && withReturn[^1].Balance < 16_000m); // ≈ 15,528
        Assert.Equal(11, withReturn.Count); // year 0..10
    }

    [Fact]
    public void Inflation_lowers_the_real_value()
    {
        var points = FinanceCalculator.SavingsProjection(10_000m, 0m, 0m, 10, inflationPct: 3m);
        Assert.Equal(10_000m, points[^1].Balance);
        Assert.True(points[^1].RealValue < 7_500m); // 10,000 / 1.03^10 ≈ 7,441
    }

    [Fact]
    public void Monthly_saving_for_goal_reaches_the_target()
    {
        var monthly = FinanceCalculator.MonthlySavingForGoal(100_000m, 0m, 6m, 20);
        var end = FinanceCalculator.SavingsProjection(0m, monthly, 6m, 20)[^1].Balance;
        Assert.InRange(end, 99_900m, 100_100m);
    }

    [Fact]
    public void Retirement_plan_uses_withdrawal_rate()
    {
        var plan = FinanceCalculator.RetirementPlan(2_000m, 30, 0m, annualReturnPct: 6m, withdrawalRatePct: 4m);
        Assert.Equal(600_000m, plan.CapitalNeeded); // 2,000 × 12 / 4%
        Assert.True(plan.MonthlySaving > 0);
    }

    [Theory]
    [InlineData("RO49 AAAA 1B31 0075 9384 0000", true)]
    [InlineData("ro49aaaa1b31007593840000", true)]
    [InlineData("DE89 3704 0044 0532 0130 00", true)]
    [InlineData("RO49 AAAA 1B31 0075 9384 0001", false)] // wrong checksum
    [InlineData("RO49 AAAA 1B31", false)]                // too short
    [InlineData("not an iban", false)]
    public void Iban_validation(string iban, bool expected)
    {
        Assert.Equal(expected, Iban.Validate(iban).IsValid);
    }

    [Fact]
    public void Iban_format_groups_by_four()
    {
        Assert.Equal("RO49 AAAA 1B31 0075 9384 0000", Iban.Format("ro49aaaa1b31007593840000"));
    }
}
