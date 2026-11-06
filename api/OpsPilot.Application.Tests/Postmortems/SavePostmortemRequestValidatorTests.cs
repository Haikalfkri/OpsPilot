using OpsPilot.Application.Postmortems;
using Xunit;

namespace OpsPilot.Application.Tests.Postmortems;

public class SavePostmortemRequestValidatorTests
{
    private readonly SavePostmortemRequestValidator _validator = new();

    private static SavePostmortemRequest Valid() =>
        new("Summary", "Root cause", "Impact", "Corrective actions");

    /// <summary>Mengganti satu field (berdasarkan nama) dengan nilai tertentu.</summary>
    private static SavePostmortemRequest With(string field, string? value) => field switch
    {
        "Summary" => Valid() with { Summary = value! },
        "RootCause" => Valid() with { RootCause = value! },
        "Impact" => Valid() with { Impact = value! },
        "CorrectiveActions" => Valid() with { CorrectiveActions = value! },
        _ => throw new ArgumentException($"Unknown field {field}")
    };

    [Fact]
    public void Validate_WithAFullRequest_IsValid() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("Summary")]
    [InlineData("RootCause")]
    [InlineData("Impact")]
    [InlineData("CorrectiveActions")]
    public void Validate_WhenAFieldIsEmptyBlankOrMissing_ReportsThatField(string field)
    {
        foreach (var value in new string?[] { "", "   ", null })
        {
            var result = _validator.Validate(With(field, value));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == field);
        }
    }

    [Theory]
    [InlineData("Summary", 2000)]
    [InlineData("RootCause", 4000)]
    [InlineData("Impact", 2000)]
    [InlineData("CorrectiveActions", 4000)]
    public void Validate_AtExactlyTheLimit_IsValid(string field, int limit) =>
        Assert.True(_validator.Validate(With(field, new string('a', limit))).IsValid);

    [Theory]
    [InlineData("Summary", 2000)]
    [InlineData("RootCause", 4000)]
    [InlineData("Impact", 2000)]
    [InlineData("CorrectiveActions", 4000)]
    public void Validate_OneCharacterOverTheLimit_IsInvalid(string field, int limit)
    {
        var result = _validator.Validate(With(field, new string('a', limit + 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == field);
    }

    [Fact]
    public void Validate_CountsLengthAfterTrimming()
    {
        // 2000 huruf dikelilingi spasi: setelah dipangkas tepat 2000, jadi masih sah
        var padded = "  " + new string('a', 2000) + "  ";

        Assert.True(_validator.Validate(With("Summary", padded)).IsValid);
    }

    [Fact]
    public void Validate_ReportsEveryBrokenFieldAtOnce()
    {
        var result = _validator.Validate(new SavePostmortemRequest("", "", "", ""));

        var fields = result.Errors.Select(e => e.PropertyName).Distinct().ToArray();
        Assert.Equal(4, fields.Length);
    }
}