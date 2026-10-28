using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HackathonStarter.Tests.Crs;

public record TestText(string Text, double Size = 11, byte R = 0, byte G = 0, byte B = 0, double X = 50, double Y = 750,
    TextRenderingMode Mode = TextRenderingMode.Fill);

public record TestPage(params TestText[] Texts);

/// <summary>Builds small PDFs for tests. Helvetica only: no ș, ț, ă.</summary>
public static class TestPdf
{
    public static byte[] Build(params TestPage[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var p in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            foreach (var t in p.Texts)
            {
                page.SetTextAndFillColor(t.R, t.G, t.B);
                page.SetTextRenderingMode(t.Mode);
                page.AddText(t.Text, t.Size, new PdfPoint(t.X, t.Y), font);
                page.SetTextRenderingMode(TextRenderingMode.Fill);
                page.ResetColor();
            }
        }
        return builder.Build();
    }
}
