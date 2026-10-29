using OpsPilot.Application.Users;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Validators;

public class CreateUserRequestValidatorTests
{
    private readonly CreateUserRequestValidator _validator = new();

    private static CreateUserRequest Valid() =>
        new("baru@opspilot.local", "User Baru", "Secret123!", UserRole.Responder);

    [Fact]
    public void Validate_WithValidRequest_IsValid() =>
        Assert.True(_validator.Validate(Valid()).IsValid);

    [Theory]
    [InlineData("pendek1!")]            // 8 karakter tapi tanpa huruf besar
    [InlineData("TANPAKECIL1!")]        // tanpa huruf kecil
    [InlineData("TanpaAngka!!")]        // tanpa angka
    [InlineData("TanpaSimbol123")]      // tanpa simbol
    [InlineData("Ab1!")]                // terlalu pendek
    public void Validate_WithWeakPassword_IsInvalid(string password)
    {
        var result = _validator.Validate(Valid() with { Password = password });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Password");
    }

    [Fact]
    public void Validate_WithoutRole_IsInvalid_SoNobodyBecomesAdminByAccident()
    {
        var result = _validator.Validate(Valid() with { Role = null });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Role");
    }

    [Fact]
    public void Validate_WithUndefinedRole_IsInvalid() =>
        Assert.False(_validator.Validate(Valid() with { Role = (UserRole)999 }).IsValid);

    [Theory]
    [InlineData("", "Nama")]
    [InlineData("bukan-email", "Nama")]
    [InlineData("a@b.co", "")]
    public void Validate_WithBadEmailOrName_IsInvalid(string email, string name) =>
        Assert.False(_validator.Validate(Valid() with { Email = email, DisplayName = name }).IsValid);
}

public class UpdateUserRequestValidatorTests
{
    private readonly UpdateUserRequestValidator _validator = new();

    [Fact]
    public void Validate_WithEmptyRequest_IsInvalid() =>
        Assert.False(_validator.Validate(new UpdateUserRequest()).IsValid);

    [Theory]
    [InlineData("Nama Baru", null, null)]
    [InlineData(null, UserRole.Viewer, null)]
    [InlineData(null, null, false)]
    public void Validate_WithAtLeastOneField_IsValid(string? name, UserRole? role, bool? isActive) =>
        Assert.True(_validator.Validate(new UpdateUserRequest(name, role, isActive)).IsValid);

    [Fact]
    public void Validate_WithBlankDisplayName_IsInvalid() =>
        Assert.False(_validator.Validate(new UpdateUserRequest(DisplayName: "   ")).IsValid);

    [Fact]
    public void Validate_WithUndefinedRole_IsInvalid() =>
        Assert.False(_validator.Validate(new UpdateUserRequest(Role: (UserRole)999)).IsValid);
}