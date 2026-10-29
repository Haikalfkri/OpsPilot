using Xunit;

namespace OpsPilot.Api.IntegrationTests;

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
}