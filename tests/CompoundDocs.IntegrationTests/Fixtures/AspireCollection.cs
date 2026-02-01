namespace CompoundDocs.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for Aspire-based integration tests.
/// All tests in this collection share the same Aspire fixture instance.
/// </summary>
[CollectionDefinition(Name)]
public sealed class AspireCollection : ICollectionFixture<AspireIntegrationFixture>
{
    public const string Name = "Aspire";
}
