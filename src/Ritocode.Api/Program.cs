using Ritocode.Api.Setup;

var builder = WebApplication.CreateBuilder(args);

builder.AddRitocodeApi();

var app = builder.Build();

app.UseRitocodeApi();

await app.RunAsync();

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in Ritocode.Api.Tests can boot the real host.
/// </summary>
public partial class Program;
