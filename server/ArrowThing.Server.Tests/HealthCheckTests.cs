namespace ArrowThing.Server.Tests;

public class HealthCheckTests : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public HealthCheckTests(TestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
    }
}
