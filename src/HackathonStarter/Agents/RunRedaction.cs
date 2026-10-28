using System.Security.Cryptography;
using System.Text.Json;
using HackathonStarter.Services;
using Microsoft.AspNetCore.DataProtection;

namespace HackathonStarter.Agents;

/// <summary>
/// Privacy at rest: what is SAVED (run history, recordings, exports) never contains personal data.
/// The live run in memory keeps the real values (the user who typed them sees them, tools need them);
/// every copy that is written to the database or to disk goes through <see cref="Redact"/> first.
///
/// One exception, needed for human approval: the arguments of an action still awaiting approval are kept
/// encrypted (<see cref="PiiSealer"/>) so the action can run with the real values once approved. They are
/// dropped as soon as the action is decided.
/// </summary>
public static class RunRedaction
{
    /// <summary>
    /// Copies of the steps with personal data replaced by one-way placeholders ([IBAN-1]). One vault for the whole
    /// run, so the same value gets the same placeholder in the request and in every step.
    /// </summary>
    /// <param name="vault">Null = copy without masking (Privacy:RedactRecordings = false).</param>
    public static List<AgentStep> Redact(IEnumerable<AgentStep> steps, PiiVault? vault, PiiSealer? sealer = null) =>
        steps.Select(step =>
        {
            var copy = JsonSerializer.Deserialize<AgentStep>(JsonSerializer.Serialize(step, RunStore.Json), RunStore.Json)!;
            var pending = copy is { Kind: StepKind.Approval, State: ApprovalState.Pending };
            if (!pending || sealer is null) copy.SealedInput = null;
            else if (copy.SealedInput is null && PiiVault.Detect(copy.Input).Count > 0) copy.SealedInput = sealer.Seal(copy.Input!);

            if (vault is null) return copy;
            copy.Text = vault.Mask(copy.Text);
            copy.Input = Mask(copy.Input, vault);
            copy.Output = Mask(copy.Output, vault);
            copy.Error = Mask(copy.Error, vault);
            copy.Summary = Mask(copy.Summary, vault);
            return copy;
        }).ToList();

    public static string? Mask(string? text, PiiVault vault) => text is null ? null : vault.Mask(text);
}

/// <summary>
/// Encrypts the few values that must survive a save with their real content (arguments of a pending approval),
/// with ASP.NET Core Data Protection. If the keys change (e.g. a container restart without persisted keys),
/// the value can't be decrypted any more and the approval must be requested again: safe by default.
/// </summary>
public class PiiSealer(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("HackathonStarter.PendingApprovals.v1");

    public string Seal(string value) => _protector.Protect(value);

    public string? TryUnseal(string? sealedValue)
    {
        if (string.IsNullOrEmpty(sealedValue)) return null;
        try { return _protector.Unprotect(sealedValue); }
        catch (CryptographicException) { return null; }
    }
}
