using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HackathonStarter.Features.Rules;

public static class RulesSetup
{
    /// <summary>
    /// The rules engine is stateless: one instance. R3 registers its <see cref="ITinFormatCheck"/> before or after this
    /// call (TryAdd keeps theirs); without it, TIN formats are reported as "not checked".
    /// </summary>
    public static IServiceCollection AddRules(this IServiceCollection services)
    {
        services.TryAddSingleton<ITinFormatCheck, TinFormatNotChecked>();
        services.AddSingleton(sp => new CrsRuleEngine(sp.GetRequiredService<ITinFormatCheck>()));
#if CRS_PHASE0
        services.AddSingleton<ICrsRuleEngine>(sp => sp.GetRequiredService<CrsRuleEngine>());
#endif
        return services;
    }
}
