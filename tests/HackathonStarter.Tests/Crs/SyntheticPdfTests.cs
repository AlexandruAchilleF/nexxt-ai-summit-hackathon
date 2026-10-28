using HackathonStarter.Features.Synthetic;
using HackathonStarter.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace HackathonStarter.Tests.Crs;

public class SyntheticPdfTests
{
    public static TheoryData<string> Files => [.. SyntheticPdfs.FileNames];

    public static TheoryData<string> CleanFiles => [.. SyntheticPdfs.All().Where(p => p.Hidden.Count == 0).Select(p => p.FileName)];

    public static TheoryData<string> HiddenFiles => [.. SyntheticPdfs.All().Where(p => p.Hidden.Count > 0).Select(p => p.FileName)];

    [Fact]
    public void Fonts_are_embedded_truetype()
    {
        using var builder = new PdfDocumentBuilder();
        foreach (var font in new[] { SyntheticPdfs.RegularFont, SyntheticPdfs.BoldFont })
            Assert.True(builder.CanUseTrueTypeFont(font, out var reasons), string.Join("; ", reasons));
    }

    [Theory]
    [MemberData(nameof(Files))]
    public async Task Every_quote_is_extracted_exactly(string fileName)
    {
        var pdf = SyntheticPdfs.Get(fileName);
        var file = await DocumentText.ExtractAsync(new MemoryStream(pdf.Bytes), pdf.FileName);

        Assert.Equal(fileName, pdf.FileName);
        Assert.NotEmpty(pdf.Quotes);
        foreach (var quote in pdf.Quotes) Assert.Contains(quote, file.Text);
    }

    [Fact]
    public async Task Diacritics_survive_extraction()
    {
        var text = "";
        foreach (var pdf in SyntheticPdfs.All())
            text += (await DocumentText.ExtractAsync(new MemoryStream(pdf.Bytes), pdf.FileName)).Text;

        foreach (var letter in "șțăîâȘȚĂÎÂüß") Assert.Contains(letter, text);
        Assert.DoesNotContain('ş', text);   // cedilla forms would break exact quotes
        Assert.DoesNotContain('ţ', text);
        Assert.DoesNotContain('�', text);
    }

    [Theory]
    [InlineData(SyntheticPdfs.PassportAlexandra)]
    [InlineData(SyntheticPdfs.PassportMarius)]
    public void Identity_documents_show_specimen_and_no_mrz(string fileName)
    {
        using var pdf = PdfDocument.Open(SyntheticPdfs.Get(fileName).Bytes);
        var page = pdf.GetPage(1);
        var specimen = page.GetWords().Where(w => w.Text == "SPECIMEN").ToList();

        Assert.True(specimen.Count >= 2);
        Assert.All(specimen.SelectMany(w => w.Letters), l => Assert.True(l.PointSize >= 20 && !IsWhitish(l)));
        Assert.DoesNotContain("<<", page.Text);
    }

    [Theory]
    [MemberData(nameof(CleanFiles))]
    public void Clean_documents_have_nothing_a_shield_would_flag(string fileName)
    {
        using var pdf = PdfDocument.Open(SyntheticPdfs.Get(fileName).Bytes);
        foreach (var page in pdf.GetPages())
            Assert.All(page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value)), l => Assert.Equal(Concealment.None, HowHidden(l, page)));
    }

    [Theory]
    [MemberData(nameof(HiddenFiles))]
    public async Task Hidden_text_is_hidden_exactly_as_declared(string fileName)
    {
        var synthetic = SyntheticPdfs.Get(fileName);
        var hidden = Assert.Single(synthetic.Hidden);
        using var pdf = PdfDocument.Open(synthetic.Bytes);

        foreach (var page in pdf.GetPages())
        {
            var flagged = page.Letters.Where(l => !string.IsNullOrWhiteSpace(l.Value) && HowHidden(l, page) != Concealment.None).ToList();
            if (page.Number != hidden.Page)
            {
                Assert.Empty(flagged);
                continue;
            }
            Assert.Equal(hidden.Text.Replace(" ", ""), string.Concat(flagged.Select(l => l.Value)));
            Assert.All(flagged, l => Assert.Equal(hidden.How, HowHidden(l, page)));
        }

        // The raw extraction still has it: this is what the shield has to remove before the model sees the text.
        var raw = await DocumentText.ExtractAsync(new MemoryStream(synthetic.Bytes), synthetic.FileName);
        Assert.Contains(hidden.Text, raw.Text);
    }

    [Fact]
    public void Victor_statement_hides_white_one_point_text_on_page_one()
    {
        var hidden = Assert.Single(SyntheticPdfs.Get(SyntheticPdfs.StatementVictor).Hidden);

        Assert.Equal(1, hidden.Page);
        Assert.Equal(Concealment.White | Concealment.Tiny, hidden.How);
        Assert.Equal(SyntheticPdfs.Injection, hidden.Text);
    }

    [Fact]
    public void Generation_is_deterministic() =>
        Assert.All(SyntheticPdfs.FileNames, name => Assert.Equal(SyntheticPdfs.Get(name).Bytes, SyntheticPdfs.Get(name).Bytes));

    [Fact]
    public void Unknown_file_name_throws() => Assert.Throws<KeyNotFoundException>(() => SyntheticPdfs.Get("nope.pdf"));

    // Same four detections as PLAN 10.1; the near-white threshold is stricter here than any shield would need.
    private static Concealment HowHidden(Letter letter, Page page)
    {
        var how = Concealment.None;
        if (IsWhitish(letter)) how |= Concealment.White;
        if (letter.PointSize < 2) how |= Concealment.Tiny;
        if (!Inside(letter.BoundingBox, page.MediaBox.Bounds)) how |= Concealment.OffPage;
        if (letter.RenderingMode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip) how |= Concealment.Invisible;
        return how;
    }

    private static bool IsWhitish(Letter letter)
    {
        var (r, g, b) = letter.FillColor.ToRGBValues();
        return r > 0.75 && g > 0.75 && b > 0.75;
    }

    private static bool Inside(PdfRectangle glyph, PdfRectangle box) =>
        glyph.Left >= box.Left && glyph.Right <= box.Right && glyph.Bottom >= box.Bottom && glyph.Top <= box.Top;
}
