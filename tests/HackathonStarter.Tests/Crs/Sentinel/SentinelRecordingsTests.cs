using System.Globalization;
using System.Text.Json;
using HackathonStarter.Agents;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Targets;
using HackathonStarter.Features.Tin;
using HackathonStarter.Services;
using HackathonStarter.Tests.Support;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// Recordings/crs-sentinel.json (recorded on Bedrock): replay does not run tools, so only questions that read are recorded,
/// personal data is masked, and a question about another case or the case page's two buttons never replays one.
/// </summary>
public class SentinelRecordingsTests
{
    private static readonly string[] ReadTools = ["getCase", "getClientSnapshot", "evaluateRules", "listDocuments", "readDocument", "checkTin"];

    [Fact]
    public void The_committed_recordings_only_read_and_hold_no_personal_data_in_clear()
    {
        var text = File.ReadAllText(RecordingFile());
        var recordings = JsonSerializer.Deserialize<List<Recordings.Recording>>(text, RunStore.Json)!;

        Assert.NotEmpty(recordings);
        Assert.Contains(recordings, r => r.Input.Contains("Rezumă cazul"));
        Assert.Contains(recordings, r => r.Input.Contains("Summarize the case"));
        foreach (var r in recordings)
        {
            Assert.Equal(SentinelActions.Action.None, SentinelActions.Recognize(r.Input));
            Assert.DoesNotContain(r.Steps, s => s.Kind == StepKind.Approval);
            Assert.All(r.Steps.Where(s => s.Kind == StepKind.ToolCall), s => Assert.Contains(s.Tool, ReadTools));
            Assert.Contains(r.Steps, s => s.Kind == StepKind.Text && s.Text.Length > 0);
        }
        Assert.Equal(text, PiiVault.ForStorage().Mask(text)); // CNP, IBAN, phone, email: already placeholders
        Assert.Contains("[CNP-1]", text);
    }

    [Fact]
    public async Task A_recorded_question_replays_but_another_case_and_the_two_buttons_do_not()
    {
        await using var host = await TestHost.CreateAsync(client: null);
        Directory.CreateDirectory(host.RecordingsDir);
        File.Copy(RecordingFile(), Path.Combine(host.RecordingsDir, "crs-sentinel.json"));
        var recordings = host.Get<Recordings>();

        Assert.True(recordings.Find("crs-sentinel", "Caz: CRS-2026-0006\n\nRezumă cazul.")?.Exact);
        Assert.False(recordings.Find("crs-sentinel", "Client: CL-0003\n\nWhy is there no case?")?.Exact);
        Assert.Null(recordings.Find("crs-sentinel", "Caz: CRS-2026-0008\n\nRezumă cazul."));
        Assert.Null(recordings.Find("crs-sentinel", "Client: CL-0013\n\nDe ce nu e caz?"));

        var before = CultureInfo.CurrentUICulture;
        try
        {
            foreach (var culture in new[] { "ro-RO", "en-GB" })
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
                foreach (var code in new[] { "CRS-2026-0005", "CRS-2026-0006", "CRS-2026-0008" })
                {
                    Assert.Null(recordings.Find("crs-sentinel", SentinelActions.Analyze(code)));
                    Assert.Null(recordings.Find("crs-sentinel", SentinelActions.Verify(code)));
                }
            }
        }
        finally { CultureInfo.CurrentUICulture = before; }
    }

    [Theory]
    [InlineData("ro-RO")]
    [InlineData("en-US")]
    public async Task Maria_tin_recording_replays_offline_and_only_claims_format(string culture)
    {
        using var language = new UiLanguage(culture);
        await using var host = await TestHost.CreateAsync(client: null, targets: t =>
            t[TinOnEuropaClient.Target] = new TargetOptions { Kind = "Http", Endpoint = "http://127.0.0.1:9", TimeoutSeconds = 1 });
        var targetDir = Path.Combine(host.RecordingsDir, "targets");
        Directory.CreateDirectory(targetDir);
        File.Copy(Path.Combine(Path.GetDirectoryName(RecordingFile())!, "targets", "tin-on-europa.json"),
            Path.Combine(targetDir, "tin-on-europa.json"));
        var europa = new TinOnEuropaClient(host.Get<TargetClient>());
        var answer = await europa.CheckAsync("DE", "86095174223");
        Assert.Equal(TargetSource.Replay, answer.Source);
        Assert.Equal(new TinOnEuropaAnswer(true, true), answer.Answer);
        var check = await new TinValidator(new TinFormat(), europa).CheckAsync("DE", "86095174223");
        Assert.Equal(TinCheckSource.TinOnEuropaReplay, check.Source);
        Assert.Contains("format valid", check.Label);
        Assert.DoesNotContain("tin valid", check.Label, StringComparison.OrdinalIgnoreCase);
    }

    private static string RecordingFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "HackathonStarter.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found"),
            "src", "HackathonStarter", "Recordings", "crs-sentinel.json");
    }
}
