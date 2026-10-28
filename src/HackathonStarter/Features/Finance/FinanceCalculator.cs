namespace HackathonStarter.Features.Finance;

/// <summary>
/// Pure, tested financial formulas (see tests/HackathonStarter.Tests). Used by the Calculators page
/// and exposed to agents as tools, so the AI never does the math itself: it calls these.
/// All money is <see cref="decimal"/>; rates are annual percentages (e.g. 7.5 = 7.5%).
/// </summary>
public static class FinanceCalculator
{
    /// <summary>Monthly payment of a fixed-rate annuity loan (equal installments).</summary>
    public static decimal LoanPayment(decimal principal, decimal annualRatePct, int months)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(months);
        if (principal <= 0) return 0;
        if (annualRatePct == 0) return Round(principal / months);

        var r = (double)annualRatePct / 100 / 12;
        var payment = (double)principal * r / (1 - Math.Pow(1 + r, -months));
        return Round((decimal)payment);
    }

    /// <summary>Month-by-month amortization: how each payment splits into interest and principal.</summary>
    public static List<AmortizationRow> AmortizationSchedule(decimal principal, decimal annualRatePct, int months)
    {
        var payment = LoanPayment(principal, annualRatePct, months);
        var monthlyRate = annualRatePct / 100 / 12;
        var balance = principal;
        var rows = new List<AmortizationRow>(months);

        for (var m = 1; m <= months; m++)
        {
            var interest = Round(balance * monthlyRate);
            // Last payment absorbs rounding differences so the balance ends at exactly 0.
            var principalPart = m == months ? balance : Math.Min(balance, payment - interest);
            balance = Round(balance - principalPart);
            rows.Add(new AmortizationRow(m, principalPart + interest, principalPart, interest, balance));
        }
        return rows;
    }

    public static LoanSummary LoanSummary(decimal principal, decimal annualRatePct, int months)
    {
        var schedule = AmortizationSchedule(principal, annualRatePct, months);
        var totalPaid = schedule.Sum(r => r.Payment);
        return new LoanSummary(LoanPayment(principal, annualRatePct, months), totalPaid, totalPaid - principal, months);
    }

    /// <summary>
    /// Savings growth with monthly contributions and monthly compounding. Also returns the value in
    /// today's money (adjusted for inflation), which is what people actually care about.
    /// </summary>
    public static List<SavingsPoint> SavingsProjection(
        decimal initial, decimal monthlyContribution, decimal annualReturnPct, int years, decimal inflationPct = 0)
    {
        var monthlyRate = (double)annualReturnPct / 100 / 12;
        var balance = (double)initial;
        var contributed = (double)initial;
        var points = new List<SavingsPoint> { new(0, initial, initial, initial) };

        for (var month = 1; month <= years * 12; month++)
        {
            balance = balance * (1 + monthlyRate) + (double)monthlyContribution;
            contributed += (double)monthlyContribution;
            if (month % 12 == 0)
            {
                var year = month / 12;
                var real = balance / Math.Pow(1 + (double)inflationPct / 100, year);
                points.Add(new SavingsPoint(year, Round((decimal)balance), Round((decimal)contributed), Round((decimal)real)));
            }
        }
        return points;
    }

    /// <summary>
    /// How much to save each month to reach a target capital in <paramref name="years"/> (future value of an annuity).
    /// </summary>
    public static decimal MonthlySavingForGoal(decimal target, decimal initial, decimal annualReturnPct, int years)
    {
        var n = years * 12;
        var r = (double)annualReturnPct / 100 / 12;
        var futureOfInitial = (double)initial * Math.Pow(1 + r, n);
        var remaining = (double)target - futureOfInitial;
        if (remaining <= 0) return 0;
        var factor = r == 0 ? n : (Math.Pow(1 + r, n) - 1) / r;
        return Round((decimal)(remaining / factor));
    }

    /// <summary>
    /// Retirement plan: capital needed for a monthly income (using a safe withdrawal rate, e.g. 4%/year),
    /// and the monthly saving required to get there.
    /// </summary>
    public static RetirementPlan RetirementPlan(
        decimal desiredMonthlyIncome, int yearsUntilRetirement, decimal currentSavings,
        decimal annualReturnPct = 6, decimal withdrawalRatePct = 4)
    {
        var capitalNeeded = Round(desiredMonthlyIncome * 12 / (withdrawalRatePct / 100));
        var monthly = MonthlySavingForGoal(capitalNeeded, currentSavings, annualReturnPct, yearsUntilRetirement);
        return new RetirementPlan(capitalNeeded, monthly, yearsUntilRetirement, annualReturnPct, withdrawalRatePct);
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

public record AmortizationRow(int Month, decimal Payment, decimal Principal, decimal Interest, decimal Balance);

public record LoanSummary(decimal MonthlyPayment, decimal TotalPaid, decimal TotalInterest, int Months);

public record SavingsPoint(int Year, decimal Balance, decimal Contributed, decimal RealValue);

public record RetirementPlan(
    decimal CapitalNeeded, decimal MonthlySaving, int Years, decimal AnnualReturnPct, decimal WithdrawalRatePct);
