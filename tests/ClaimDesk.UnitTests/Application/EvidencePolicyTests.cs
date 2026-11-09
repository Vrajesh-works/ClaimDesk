using ClaimDesk.Application.Uploads;

namespace ClaimDesk.UnitTests.Application;

public class EvidencePolicyTests
{
    private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0];

    [Theory]
    [InlineData("estimate.pdf", "application/pdf")]
    [InlineData("SCAN.PDF", "application/pdf")]
    public void Accepts_pdf(string name, string contentType)
    {
        var result = EvidencePolicy.Check(name, 100, Pdf);

        Assert.True(result.Ok);
        Assert.Equal(contentType, result.ContentType);
    }

    [Fact]
    public void Accepts_png_and_jpeg()
    {
        Assert.Equal("image/png", EvidencePolicy.Check("bumper.png", 100, Png).ContentType);
        Assert.Equal("image/jpeg", EvidencePolicy.Check("bumper.jpg", 100, Jpeg).ContentType);
        Assert.Equal("image/jpeg", EvidencePolicy.Check("bumper.jpeg", 100, Jpeg).ContentType);
    }

    [Fact]
    public void Rejects_disallowed_extension()
    {
        var result = EvidencePolicy.Check("payload.exe", 100, Pdf);

        Assert.False(result.Ok);
        Assert.Contains("Only PDF, PNG and JPEG", result.Error);
    }

    [Fact]
    public void Rejects_content_that_does_not_match_extension()
    {
        var result = EvidencePolicy.Check("photo.png", 100, Pdf);

        Assert.False(result.Ok);
        Assert.Contains("does not match", result.Error);
    }

    [Fact]
    public void Rejects_empty_and_oversized_files()
    {
        Assert.False(EvidencePolicy.Check("a.pdf", 0, Pdf).Ok);
        Assert.False(EvidencePolicy.Check("a.pdf", EvidencePolicy.MaxBytes + 1, Pdf).Ok);
        Assert.True(EvidencePolicy.Check("a.pdf", EvidencePolicy.MaxBytes, Pdf).Ok);
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("C:\\temp\\report.pdf", "report.pdf")]
    [InlineData("my<script>photo.png", "myscriptphoto.png")]
    [InlineData("<>", "evidence")]
    public void Sanitizes_file_names(string input, string expected)
    {
        Assert.Equal(expected, EvidencePolicy.SanitizeFileName(input));
    }
}
