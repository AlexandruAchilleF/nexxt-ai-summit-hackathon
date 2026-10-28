using System.Diagnostics;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Targets;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Services;

/// <summary>
/// "Does everything answer?" before going on stage: sends a tiny request to the main model, every model profile
/// and every target, and reports which ones work and how fast. Admins/members only (it spends a few tokens).
/// </summary>
public class ConnectionCheck(ChatClientProvider ai, TargetClient targets, CurrentUser currentUser)
{
    public record Result(string Name, string Kind, bool Ok, long Milliseconds, string Detail);

    public async Task<List<Result>> RunAsync(CancellationToken ct = default)
    {
        await currentUser.Require(Roles.Admin, Roles.Member);
        var results = new List<Result>();

        // One after the other: easier to read in order, and a slow target doesn't distort the others' timings.
        if (ai.IsConfigured) results.Add(await ModelAsync("main model", null, ct));
        foreach (var profile in ai.Options.Profiles.Keys)
        {
            results.Add(ai.ClientFor(profile) == ai.Client
                ? new Result($"profile “{profile}”", "model", false, 0, "not usable: missing or invalid settings, the main model is used instead")
                : await ModelAsync($"profile “{profile}”", profile, ct));
        }
        foreach (var target in targets.List()) results.Add(await TargetAsync(target, ct));

        return results;
    }

    private async Task<Result> ModelAsync(string name, string? profile, CancellationToken ct)
    {
        var options = ai.OptionsFor(profile);
        var watch = Stopwatch.StartNew();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var response = await ai.ClientFor(profile)!.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "Reply with the single word OK.")], cancellationToken: timeout.Token);
            return new Result(name, "model", true, watch.ElapsedMilliseconds, $"{options.Provider} · {options.Model}: “{Short(response.Text)}”");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return new Result(name, "model", false, watch.ElapsedMilliseconds, $"{options.Provider} · {options.Model}: {Short(ex.Message)}");
        }
    }

    private async Task<Result> TargetAsync(TargetInfo target, CancellationToken ct)
    {
        var reply = await targets.SendAsync(target.Name, [new ChatTurn("user", "Hello, are you there?")], ct);
        var detail = reply.Source switch
        {
            TargetSource.Live => $"{target.Kind} · {target.Host}: “{Short(reply.Text)}”",
            TargetSource.Replay => $"down ({Short(reply.Error)}), but a recorded answer exists",
            _ => $"down: {Short(reply.Error)}",
        };
        return new Result($"target “{target.Label}”", "target", reply.Source == TargetSource.Live, reply.ElapsedMs, detail);
    }

    private static string Short(string? text) =>
        string.IsNullOrWhiteSpace(text) ? "(empty)" : text.Length <= 120 ? text.Trim() : text[..120].Trim() + "…";
}
