using HackathonStarter.Features.Clients;

namespace HackathonStarter.Features.Shield;

public enum ShieldVerdict { NotScanned, Clean, HiddenTextRemoved }

/// <summary>The text a person sees on one page. Only this ever reaches a model.</summary>
public record ShieldedPage(int Number, string VisibleText);

/// <summary>Text a person would not see (white, microscopic, off-page, invisible). Never sent to a model; shown masked in the UI.</summary>
public record HiddenFragment(int Page, string Text, string Reason);

/// <param name="Pages">Visible text only: the only part a model may receive.</param>
/// <param name="Hidden">Never sent to a model (readDocument returns <see cref="Pages"/> only).</param>
public record ShieldResult(ShieldVerdict Verdict, IReadOnlyList<ShieldedPage> Pages, IReadOnlyList<HiddenFragment> Hidden);

/// <summary>Runs on every PDF before any model reads it (docs/PLAN.md 10.1). Pure code over PdfPig letters. Owner: R3.</summary>
public interface IHiddenTextShield
{
    ShieldResult Scan(byte[] pdf);
}

/// <param name="Page">The page where the quote was found in the visible text.</param>
/// <param name="FoundOnlyInHiddenText">The quote exists only in hidden text: not verified and a red signal (PLAN 10.2).</param>
public record EvidenceCheck(bool Verified, int? Page, string Note, bool FoundOnlyInHiddenText = false);

/// <summary>Does the quote exist in the visible text (normalized)? A quote found only in hidden text is not verified (PLAN 10.2). Owner: R3.</summary>
public interface IEvidenceVerifier
{
    EvidenceCheck Verify(string quote, ShieldResult document, int? page = null);
}

public record ShieldedDocument(int DocumentId, int ClientId, string FileName, DocumentKind Kind, ShieldResult Result)
{
    /// <summary>What a model may read. Use this, never <see cref="ShieldResult.Hidden"/>, in agent tools.</summary>
    public IReadOnlyList<ShieldedPage> VisiblePages => Result.Pages;
}

/// <summary>Loads a client document and returns it shielded: the agent's readDocument tool and evidence checks use this.</summary>
public interface IShieldedDocuments
{
    Task<ShieldedDocument?> GetAsync(int documentId);
}
