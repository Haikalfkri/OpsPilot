using OpsPilot.Application.Auth;
using Xunit;

namespace OpsPilot.Application.Tests.Validators
{
    public class LoginRequestValidatorTests
    {
        private readonly LoginRequestValidator _validator = new();

        [Fact]
        public void Validate_WithValidRequest_IsValid()
        {
            var result = _validator.Validate(new LoginRequest("admin@opspilot.local", "Secret123!"));

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData("", "Secret123!", "Email")]
        [InlineData("bukan-email", "Secret123!", "Email")]
        [InlineData("admin@opspilot.local", "", "Password")]
        public void Validate_WithInvalidField_ReportsThatField(string email, string password, string badField)
        {
            var result = _validator.Validate(new LoginRequest(email, password));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.PropertyName == badField);
        }

        [Fact]
        public void Validate_WithTooLongEmail_IsInvalid()
        {
            var longEmail = new string('a', 250) + "@opspilot.local";

            var result = _validator.Validate(new LoginRequest(longEmail, "Secret123!"));

            Assert.False(result.IsValid);
        }
    }
}
