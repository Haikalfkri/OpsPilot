using OpsPilot.Application.Comments;
using OpsPilot.Application.Common.Models;
using Xunit;

namespace OpsPilot.Application.Tests.Validators;

public class CreateCommentRequestValidatorTests
{
    private readonly CreateCommentRequestValidator _validator = new();

    [Theory]
    [InlineData("Sudah dicek, penyebabnya disk penuh.")]
    [InlineData("a")]                      // satu karakter sudah cukup
    public void Validate_WithNormalText_IsValid(string body) =>
        Assert.True(_validator.Validate(new CreateCommentRequest(body)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]                    // hanya spasi
    public void Validate_WithEmptyOrBlankText_IsInvalid(string body) =>
        Assert.False(_validator.Validate(new CreateCommentRequest(body)).IsValid);

    [Fact]
    public void Validate_WithNull_IsInvalid() =>
        Assert.False(_validator.Validate(new CreateCommentRequest(null!)).IsValid);

    [Fact]
    public void Validate_AtExactlyTheLimit_IsValid() =>
        Assert.True(_validator.Validate(new CreateCommentRequest(new string('a', 2000))).IsValid);

    [Fact]
    public void Validate_OneCharacterOverTheLimit_IsInvalid() =>
        Assert.False(_validator.Validate(new CreateCommentRequest(new string('a', 2001))).IsValid);

    [Fact]
    public void Validate_CountsLengthAfterTrimming()
    {
        // 2000 huruf + spasi di ujung: setelah di-trim tepat 2000, jadi masih sah
        var body = "  " + new string('a', 2000) + "  ";

        Assert.True(_validator.Validate(new CreateCommentRequest(body)).IsValid);
    }
}

public class PageQueryValidatorTests
{
    private readonly PageQueryValidator _validator = new();

    [Fact]
    public void Validate_WithDefaults_IsValid() =>
        Assert.True(_validator.Validate(new PageQuery()).IsValid);

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    [InlineData(7, 50)]
    public void Validate_InsideLimits_IsValid(int page, int pageSize) =>
        Assert.True(_validator.Validate(new PageQuery(page, pageSize)).IsValid);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Validate_OutsideLimits_IsInvalid(int page, int pageSize) =>
        Assert.False(_validator.Validate(new PageQuery(page, pageSize)).IsValid);
}