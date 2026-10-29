using OpsPilot.Application.Users;
using OpsPilot.Domain.Enums;
using Xunit;

namespace OpsPilot.Application.Tests.Validators
{
    public class ListUsersQueryValidatorTests
    {
        private readonly ListUsersQueryValidator _validator = new();

        [Fact]
        public void Validate_WithDefaults_IsValid()
        {
            Assert.True(_validator.Validate(new ListUsersQuery()).IsValid);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 100)]
        [InlineData(50, 20)]
        public void Validate_WithPagingInsideLimits_IsValid(int page, int pageSize)
        {
            Assert.True(_validator.Validate(new ListUsersQuery(page, pageSize)).IsValid);
        }

        [Theory]
        [InlineData(0, 20)]     // halaman 0
        [InlineData(-1, 20)]    // halaman negatif
        [InlineData(1, 0)]      // ukuran 0
        [InlineData(1, 101)]    // melewati batas
        public void Validate_WithPagingOutsideLimits_IsInvalid(int page, int pageSize)
        {
            Assert.False(_validator.Validate(new ListUsersQuery(page, pageSize)).IsValid);
        }

        [Fact]
        public void Validate_WithTooLongSearch_IsInvalid()
        {
            var query = new ListUsersQuery(Search: new string('x', 101));

            Assert.False(_validator.Validate(query).IsValid);
        }

        [Fact]
        public void Validate_WithUndefinedRole_IsInvalid()
        {
            var query = new ListUsersQuery(Role: (UserRole)999);

            Assert.False(_validator.Validate(query).IsValid);
        }
    }
}
