using AgroPlan.Domain.Symptoms;
using Xunit;

namespace AgroPlan.UnitTests.Symptoms;

public sealed class SymptomReportTests
{
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/2ioAAAAASUVORK5CYII=");

    [Fact]
    public void Photo_inspector_accepts_a_well_formed_png_header_and_returns_dimensions()
    {
        Assert.True(SymptomPhotoInspector.TryInspect(OnePixelPng, "image/png", out var dimensions));
        Assert.Equal(new SymptomImageDimensions(1, 1), dimensions);
    }

    [Fact]
    public void Photo_inspector_rejects_type_mismatch_malformed_bytes_and_excessive_dimensions()
    {
        Assert.False(SymptomPhotoInspector.TryInspect(OnePixelPng, "image/jpeg", out _));
        Assert.False(SymptomPhotoInspector.TryInspect([1, 2, 3], "image/png", out _));

        var tooWide = OnePixelPng.ToArray();
        tooWide[16] = 0x00;
        tooWide[17] = 0x00;
        tooWide[18] = 0x20;
        tooWide[19] = 0x01; // PNG width = 8193
        Assert.False(SymptomPhotoInspector.TryInspect(tooWide, "image/png", out _));
    }

    [Fact]
    public void Report_requires_all_authorized_photos_to_finalize_before_submission()
    {
        var report = CreateReport();
        report.AddPhoto(CreatePhoto(consent: true));
        Assert.Throws<InvalidOperationException>(() => report.Submit(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Human_diagnosis_cannot_be_confirmed_when_observation_is_insufficient()
    {
        var report = CreateSubmittedReport();
        var decision = new SymptomReportDecision(Guid.NewGuid(), SymptomReviewAction.Confirmed, "agronomist", DateTimeOffset.UtcNow,
            diagnosis: "leaf spot", treatment: "review locally");

        Assert.Throws<InvalidOperationException>(() => report.AddDecision(decision));
        Assert.Null(report.HumanDiagnosis);
        Assert.Equal(SymptomReportStatus.Pending, report.Status);
    }

    [Fact]
    public void Requesting_more_evidence_keeps_report_pending_and_preserves_private_photo()
    {
        var report = CreateSubmittedReport();
        var decision = new SymptomReportDecision(Guid.NewGuid(), SymptomReviewAction.MoreEvidenceRequested, "agronomist",
            DateTimeOffset.UtcNow, requestedEvidence: "Send a photo of the underside of the leaf.");

        report.AddDecision(decision);

        Assert.Equal(SymptomReportStatus.Pending, report.Status);
        Assert.Single(report.Decisions);
        Assert.Single(report.Photos);
        Assert.Null(Assert.Single(report.Photos).DeleteAfter);
    }

    private static SymptomReport CreateReport() => new(Guid.NewGuid(), Guid.NewGuid(), "owner", null, null, DateTimeOffset.UtcNow);

    private static SymptomReport CreateSubmittedReport()
    {
        var report = CreateReport();
        var photo = CreatePhoto(consent: true);
        photo.Finalize("private/report/photo", OnePixelPng.Length, DateTimeOffset.UtcNow);
        report.AddPhoto(photo);
        report.Submit(DateTimeOffset.UtcNow);
        return report;
    }

    private static SymptomReportPhoto CreatePhoto(bool consent) => new(Guid.NewGuid(), "private/staging/photo", "image/png",
        OnePixelPng.Length, "owner", consent, consent ? "notice-v1" : null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddMinutes(10));
}
