namespace ToggleMesh.IntegrationTests.Infrastructure;

[CollectionDefinition("SharedEnv1")]
public class SharedTestCollection1 : ICollectionFixture<TestWebApplicationFactory> { }

[CollectionDefinition("SharedEnv2")]
public class SharedTestCollection2 : ICollectionFixture<TestWebApplicationFactory> { }

[CollectionDefinition("SharedEnv3")]
public class SharedTestCollection3 : ICollectionFixture<TestWebApplicationFactory> { }

[CollectionDefinition("SharedEnv4")]
public class SharedTestCollection4 : ICollectionFixture<TestWebApplicationFactory> { }
