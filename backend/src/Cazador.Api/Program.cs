using Cazador.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.WithOrigins(builder.Configuration.GetSection("Frontend:Origins").Get<string[]>() ?? [])
        .AllowAnyHeader().AllowAnyMethod()));
var connection = builder.Configuration.GetConnectionString("MySql");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Configure ConnectionStrings:MySql mediante user-secrets o variables de entorno. Consulte README.md.");
var version = Version.Parse(builder.Configuration["MySql:ServerVersion"] ?? "8.0.0");
builder.Services.AddSingleton<ConnectionDiagnostics>();
builder.Services.AddDbContext<AppDbContext>((services, options) =>
    options.UseMySql(connection, new MySqlServerVersion(version))
        .AddInterceptors(services.GetRequiredService<ConnectionDiagnostics>()));
builder.Services.AddHttpClient("Mostaza", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CentralizadorDescuentosAcademico/0.1");
});
builder.Services.AddHttpClient("Enlaces", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CentralizadorDescuentosAcademico/0.1");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHostedService<Cazador.Api.Background.MostazaWorker>();
builder.Services.AddHttpClient("BurgerKing", client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CentralizadorDescuentosAcademico/0.1");
    client.DefaultRequestHeaders.Add("x-ui-region", "AR");
    client.DefaultRequestHeaders.Add("x-ui-language", "es");
    client.DefaultRequestHeaders.Add("x-ui-platform", "web");
    client.DefaultRequestHeaders.Add("apollo-require-preflight", "true");
});
builder.Services.AddHostedService<Cazador.Api.Background.BurgerKingWorker>();
builder.Services.AddHttpClient<Cazador.Api.Modules.Obtencion.Ocr.OcrSpaceReader>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddHttpClient<Cazador.Api.Modules.Obtencion.Ia.GeminiOfferReader>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(90);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<Cazador.Api.Modules.Obtencion.Materiales.MostazaMaterialCache>();
builder.Services.AddHttpClient<Cazador.Api.Modules.Obtencion.Materiales.MostazaMaterialReader>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("CentralizadorDescuentosAcademico/0.1");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("Frontend");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
