using OpsPilot.Domain.Enums;
using OpsPilot.Domain.Sla;
using Xunit;

namespace OpsPilot.Domain.Tests;

public class SlaEvaluatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Due = T0.AddMinutes(60);

    private static DateTimeOffset? At(int? minutes) => minutes is null ? null : T0.AddMinutes(minutes.Value);

    // ==================== Target penyelesaian ====================

    [Theory]
    //   resolvedAt, sekarang, hasil
    [InlineData(null, 30, SlaState.Pending)]     // belum selesai, belum lewat batas
    [InlineData(null, 60, SlaState.Pending)]     // belum selesai, TEPAT di batas = masih aman
    [InlineData(null, 61, SlaState.Breached)]    // belum selesai, sudah lewat batas
    [InlineData(40, 500, SlaState.Met)]          // selesai tepat waktu: tetap Met walau sudah lama berlalu
    [InlineData(60, 500, SlaState.Met)]          // selesai TEPAT di batas = Met
    [InlineData(61, 500, SlaState.Breached)]     // selesai terlambat 1 menit = Breached
    public void EvaluateResolution_FollowsTheRules(int? resolvedAtMinutes, int nowMinutes, SlaState expected)
    {
        var result = SlaEvaluator.EvaluateResolution(Due, At(resolvedAtMinutes), T0.AddMinutes(nowMinutes));

        Assert.Equal(expected, result);
    }

    // ==================== Target respons ====================

    [Theory]
    //   respondedAt, resolvedAt, sekarang, hasil
    [InlineData(null, null, 30, SlaState.Pending)]    // belum direspons, belum lewat batas
    [InlineData(null, null, 60, SlaState.Pending)]    // tepat di batas = masih aman
    [InlineData(null, null, 61, SlaState.Breached)]   // belum direspons, lewat batas
    [InlineData(20, null, 500, SlaState.Met)]         // direspons tepat waktu
    [InlineData(60, null, 500, SlaState.Met)]         // direspons tepat di batas
    [InlineData(61, null, 500, SlaState.Breached)]    // direspons terlambat
    public void EvaluateResponse_WhenInvestigated_FollowsTheRules(
        int? respondedAtMinutes, int? resolvedAtMinutes, int nowMinutes, SlaState expected)
    {
        var result = SlaEvaluator.EvaluateResponse(
            Due, At(respondedAtMinutes), At(resolvedAtMinutes), T0.AddMinutes(nowMinutes));

        Assert.Equal(expected, result);
    }

    [Theory]
    // Incident langsung Resolved dari Open (tanpa Investigating): waktu Resolved = waktu respons
    [InlineData(30, SlaState.Met)]
    [InlineData(60, SlaState.Met)]
    [InlineData(61, SlaState.Breached)]
    public void EvaluateResponse_WhenResolvedDirectly_UsesTheResolvedTimeAsTheResponseTime(
        int resolvedAtMinutes, SlaState expected)
    {
        var result = SlaEvaluator.EvaluateResponse(
            Due, firstRespondedAtUtc: null, resolvedAtUtc: At(resolvedAtMinutes), nowUtc: T0.AddMinutes(500));

        Assert.Equal(expected, result);
    }

    [Fact]
    public void EvaluateResponse_PrefersTheRealFirstResponse_OverTheResolvedTime()
    {
        // Direspons menit ke-10 (tepat waktu), baru diselesaikan menit ke-90.
        // Respons tetap dinilai dari menit ke-10, bukan menit ke-90.
        var result = SlaEvaluator.EvaluateResponse(Due, At(10), At(90), T0.AddMinutes(500));

        Assert.Equal(SlaState.Met, result);
    }
}