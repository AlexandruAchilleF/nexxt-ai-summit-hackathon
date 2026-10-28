using HackathonStarter.Features.Tin;

namespace HackathonStarter.Features.Rules;

public static class RulesSetup
{
    /// <summary>
    /// The rules engine is stateless: one instance, also behind <see cref="ICrsRuleEngine"/>. It takes R3's
    /// <see cref="ITinFormat"/> from AddTin(); without it, TIN formats are reported as "not checked".
    /// </summary>
    public static IServiceCollection AddRules(this IServiceCollection services)
    {
        services.AddSingleton(sp => new CrsRuleEngine(sp.GetService<ITinFormat>()));
        services.AddSingleton<ICrsRuleEngine>(sp => sp.GetRequiredService<CrsRuleEngine>());
        return services;
    }
}
