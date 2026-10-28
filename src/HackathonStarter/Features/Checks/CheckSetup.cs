namespace HackathonStarter.Features.Checks;

public static class CheckSetup
{
    /// <summary>
    /// Registers the check engine and the built-in checks, plus phrase rules from configuration:
    /// <code>
    /// "Checks": { "PhraseRules": [ { "Id": "no-crypto", "Title": "No crypto talk", "Phrases": ["bitcoin", "crypto"] } ],
    ///             "Canaries": ["CANARY-7F3A9C"] }
    /// </code>
    /// A rule with the id of a built-in one replaces it.
    /// </summary>
    public static IServiceCollection AddChecks(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddSingleton<ICheck, PiiLeakCheck>();
        services.AddSingleton<ICheck, ExpectedNumberCheck>();
        services.AddSingleton<ICheck>(new PromptLeakCheck(configuration?.GetSection("Checks:Canaries").Get<List<string>>() ?? []));
        foreach (var rule in PhraseRuleCheck.Defaults) services.AddSingleton<ICheck>(new PhraseRuleCheck(rule));
        // CRS agent verdicts (docs/PLAN.md 13.4).
        services.AddSingleton<ICheck, NoTinValidClaimCheck>();
        services.AddSingleton<ICheck, SignalsNeverAlertCheck>();
        services.AddSingleton<ICheck, RuleCitedCheck>();
        services.AddSingleton<ICheck, EvidenceQuotedCheck>();

        var configured = configuration?.GetSection("Checks:PhraseRules").Get<List<PhraseRule>>() ?? [];
        foreach (var rule in configured.Where(r => !string.IsNullOrWhiteSpace(r.Id) && r.Phrases.Count > 0))
        {
            if (string.IsNullOrWhiteSpace(rule.Title)) rule.Title = rule.Id;
            services.AddSingleton<ICheck>(new PhraseRuleCheck(rule));
        }

        services.AddSingleton<CheckEngine>();
        return services;
    }
}
