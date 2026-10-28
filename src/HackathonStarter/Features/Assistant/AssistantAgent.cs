using System.ComponentModel;
using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Features.Finance;
using HackathonStarter.Features.Items;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Assistant;

/// <summary>
/// Example TOOL AGENT. Copy this class for your own agent:
/// 1. write tools that call YOUR services (keep results small and JSON-friendly),
/// 2. describe in SystemPrompt what the agent is for and its rules,
/// 3. write MockAsync so the demo works without a model,
/// 4. register it in Program.cs (AddSingleton&lt;AgentDefinition, YourAgent&gt;()).
/// </summary>
public class AssistantAgent : ToolAgent
{
    public override string Name => "assistant";
    public override string Label => "Assistant";
    public override string Description => "Answers questions about items, does financial calculations, reads documents and proposes changes.";

    public override string[] Suggestions =>
    [
        "Give me an overview of our items",
        "Monthly payment for a 50,000 EUR loan at 7.5% over 5 years?",
        "Archive the 'Old prototype' item",
        "Câte elemente active avem?",
    ];

    /// <summary>Verdicts computed by code after every answer (see Features/Checks). Shown at the end of the timeline.</summary>
    public override string[] Checks => ["pii-leak", "no-guaranteed-returns", "no-personal-investment-advice"];

    public override string SystemPrompt(ToolContext ctx) => """
        You are the assistant of a business app. Use the tools to look up real data and to do ANY calculation;
        never invent items or numbers and never do financial math yourself.
        Be concise: short paragraphs or bullet lists. Mention item titles, not ids. Amounts are in EUR unless stated.
        Answer in the user's language (e.g. Romanian if they write in Romanian).
        To change data, call updateItem: it will be shown to the user for approval.
        """;

    public override IEnumerable<AgentTool> Tools(ToolContext ctx)
    {
        var items = ctx.Services.GetRequiredService<ItemService>();

        yield return AgentTool.Create("searchItems",
            "Search items by text and/or status. Use it to find items before answering or acting on them.",
            async ([Description("Free text matched against title and description")] string? query = null,
                   [Description("Draft, Active or Archived")] ItemStatus? status = null) =>
                (await items.ListAsync(new ItemFilter(query, status)))
                    .Take(10)
                    .Select(i => new { i.Id, i.Title, Status = i.Status.ToString(), Priority = i.Priority.ToString(), i.Value }));

        yield return AgentTool.Create("getStats",
            "Totals: number of items per status and priority, and total value.",
            async () =>
            {
                var s = await items.StatsAsync();
                return new
                {
                    s.Total, s.TotalValue,
                    ByStatus = s.ByStatus.ToDictionary(k => k.Key.ToString(), v => v.Value),
                    ByPriority = s.ByPriority.ToDictionary(k => k.Key.ToString(), v => v.Value),
                };
            });

        yield return AgentTool.Create("updateItem",
            "Change the status and/or priority of an item. Requires human approval: the user confirms in the UI.",
            async (int id,
                   [Description("Item title, shown on the approval card")] string title,
                   ItemStatus? status = null, ItemPriority? priority = null) =>
            {
                var item = await items.GetAsync(id) ?? throw new KeyNotFoundException($"Item {id} not found");
                if (status is { } s) item.Status = s;
                if (priority is { } p) item.Priority = p;
                var saved = await items.UpdateAsync(item);
                return new { saved.Id, Status = saved.Status.ToString(), Priority = saved.Priority.ToString() };
            },
            needsApproval: true,
            summarize: args => string.Join(", ", new[]
            {
                $"Update \"{Str(args, "title")}\"",
                Str(args, "status") is { Length: > 0 } st ? $"status → {st}" : null,
                Str(args, "priority") is { Length: > 0 } pr ? $"priority → {pr}" : null,
            }.Where(x => x is not null)));

        yield return AgentTool.Create("calculateLoan",
            "Monthly payment, total interest and total cost of a fixed-rate loan.",
            (decimal principal, [Description("Annual interest rate in %, e.g. 7.5")] decimal annualRatePct,
             [Description("Duration in months")] int months) =>
                FinanceCalculator.LoanSummary(principal, annualRatePct, months));

        yield return AgentTool.Create("projectSavings",
            "Projects savings growth with monthly contributions; also returns the value in today's money (after inflation).",
            (decimal initial, decimal monthlyContribution,
             [Description("Expected annual return in %")] decimal annualReturnPct, int years,
             [Description("Annual inflation in %, e.g. 3")] decimal inflationPct = 0) =>
                FinanceCalculator.SavingsProjection(initial, monthlyContribution, annualReturnPct, years, inflationPct)[^1]);

        yield return AgentTool.Create("retirementPlan",
            "Capital needed for a desired monthly retirement income, and the monthly saving required to reach it.",
            (decimal desiredMonthlyIncome, int yearsUntilRetirement, decimal currentSavings) =>
                FinanceCalculator.RetirementPlan(desiredMonthlyIncome, yearsUntilRetirement, currentSavings));

        yield return AgentTool.Create("validateIban",
            "Checks whether an IBAN is valid (format, length, checksum).",
            (string iban) => Iban.Validate(iban));

        yield return AgentTool.Create("readDocument",
            "Read the beginning of an attached document by file name. For specific facts in long documents use searchDocuments.",
            (string name) =>
            {
                var file = ctx.Files.FirstOrDefault(f => f.Name == name)
                           ?? ctx.Files.FirstOrDefault(f => f.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
                if (file is null) return (object)new { error = $"No attached document named \"{name}\"" };
                const int max = 6_000;
                return new
                {
                    file.Name, file.Pages, TotalCharacters = file.Text.Length,
                    Text = file.Text.Length > max ? file.Text[..max] : file.Text,
                    Note = file.Text.Length > max ? "Truncated. Use searchDocuments to find specific information." : null,
                };
            });

        yield return AgentTool.Create("searchDocuments",
            "Find the passages of the attached documents most relevant to a query (keywords). Use it for specific facts: amounts, dates, names, clauses.",
            ([Description("Keywords to look for, e.g. 'monthly income salary'")] string query) =>
                DocumentSearch.Search(ctx.Files, query).Select(p => new { p.Document, Passage = p.Index, p.Text, p.Score }));
    }

    /// <summary>Works with no model: simple intent rules, but REAL tool calls on real data.</summary>
    public override async Task MockAsync(MockContext m)
    {
        var text = m.Input;

        if (m.Context.Files.FirstOrDefault(f => f.IsImage) is { } image)
        {
            await m.SayAsync($"I received the image **{image.Name}** ({image.Image!.Length / 1024} KB). " +
                             "_Connect a vision-capable model (Claude, GPT-4o…) and I can read it: amounts, dates, names._");
            return;
        }
        if (m.Context.Files.Count > 0)
        {
            var doc = await m.CallAsync("readDocument", new { name = m.Context.Files[0].Name });
            var content = doc.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
            var words = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            await m.SayAsync($"I read **{m.Context.Files[0].Name}** ({words} words). It starts with:\n\n> {Truncate(content, 280)}\n\n_Connect a model (see README) for a real summary._");
            return;
        }

        if (TextMatch.ContainsAny(text, "loan", "credit", "mortgage", "rata", "imprumut", "dobanda", "ipotec"))
        {
            var numbers = Numbers(m.Input);
            var principal = numbers.FirstOrDefault(n => n >= 1000, 50_000m);
            var rate = numbers.FirstOrDefault(n => n is > 0 and < 30, 7.5m);
            var years = numbers.FirstOrDefault(n => n is >= 1 and <= 40 && n != rate, 5m);
            var loan = await m.CallAsync("calculateLoan", new { principal, annualRatePct = rate, months = (int)years * 12 });
            await m.SayAsync($"For **{Format.Money(principal)}** at **{rate}%** over **{years} years**: " +
                             $"monthly payment **{Format.Money(loan.GetProperty("monthlyPayment").GetDecimal())}**, " +
                             $"total interest {Format.Money(loan.GetProperty("totalInterest").GetDecimal())}.");
            return;
        }

        if (TextMatch.ContainsAny(text, "overview", "stat", "how many", "total", "summary", "sumar", "rezumat", "cate", "situatie"))
        {
            var s = await m.CallAsync("getStats", new { });
            var byStatus = s.GetProperty("byStatus");
            await m.SayAsync($"You have **{s.GetProperty("total").GetInt32()} items** worth {Format.Money(s.GetProperty("totalValue").GetDecimal())}: " +
                             $"{byStatus.GetProperty("Active").GetInt32()} active, {byStatus.GetProperty("Draft").GetInt32()} drafts and {byStatus.GetProperty("Archived").GetInt32()} archived.");
            return;
        }

        var quoted = System.Text.RegularExpressions.Regex.Match(m.Input, "['\"“„]([^'\"”“]+)['\"”“]").Groups[1].Value;
        if (TextMatch.ContainsAny(text, "archive", "update", "mark", "change", "arhiv", "actualiz", "marcheaz", "schimb", "modific"))
        {
            var found = await m.CallAsync("searchItems", new { query = quoted });
            if (found.ValueKind != JsonValueKind.Array || found.GetArrayLength() == 0)
            {
                await m.SayAsync("I couldn't find a matching item.");
                return;
            }
            var target = found[0];
            await m.CallAsync("updateItem", new { id = target.GetProperty("id").GetInt32(), title = target.GetProperty("title").GetString(), status = "Archived" });
            await m.SayAsync($"I propose archiving **{target.GetProperty("title").GetString()}**. Approve it below to apply the change.");
            return;
        }

        var results = await m.CallAsync("searchItems", new { query = string.IsNullOrEmpty(quoted) ? null : quoted });
        if (results.ValueKind != JsonValueKind.Array)
        {
            await m.SayAsync("Sorry, I couldn't search the items right now.");
            return;
        }
        var lines = results.EnumerateArray().Take(6)
            .Select(i => $"- **{i.GetProperty("title").GetString()}** · {i.GetProperty("priority").GetString()} priority · {i.GetProperty("status").GetString()}");
        await m.SayAsync(results.GetArrayLength() > 0 ? "Here is what I found:\n\n" + string.Join("\n", lines) : "No matching items.");
    }

    private static string? Str(IReadOnlyDictionary<string, JsonElement> args, string key) =>
        args.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string Truncate(string s, int max) => s.Length <= max ? s.Trim() : s[..max].Trim() + "…";

    private static List<decimal> Numbers(string s) =>
        System.Text.RegularExpressions.Regex.Matches(s.Replace(",", ""), @"\d+(\.\d+)?")
            .Select(x => decimal.Parse(x.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();
}
