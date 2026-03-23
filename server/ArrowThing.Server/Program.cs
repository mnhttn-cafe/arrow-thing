var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok());

app.Run();

// Make the implicit Program class accessible to integration tests.
public partial class Program { }
