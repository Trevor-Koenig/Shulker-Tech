namespace ShulkerTech.Tests.Infrastructure;

/// <summary>
/// Shared collection fixture that provides a single ShulkerTechWebApplicationFactory
/// (one Postgres container) across all integration test classes.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<ShulkerTechWebApplicationFactory> { }
