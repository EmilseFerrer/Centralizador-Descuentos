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
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connection, new MySqlServerVersion(version)));
var app = builder.Build();
app.UseExceptionHandler();
app.UseCors("Frontend");
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
