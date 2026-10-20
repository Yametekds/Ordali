using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Ordali.Api.Catalog;
using Ordali.Api.Data;
using Ordali.Api.Identity;
using Ordali.Api.Inventory;
using Ordali.Api.Locations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);

// The web client is served from another origin (Docker on :8081, GitHub Pages).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Ordali API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token de POST /api/v1/auth/login (admin@demo.local o mozo@demo.local, contraseña Demo1234!).",
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
    });
});

var connectionString = builder.Configuration.GetConnectionString("Ordali");
builder.Services.AddOrdaliDatabase(builder.Configuration);
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddScoped<MovementService>();
builder.Services.AddScoped<ReceiptService>();

var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddSqlServer(connectionString, name: "sqlserver", tags: ["ready"]);
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
// Before authorization: the fallback policy would otherwise also lock the Swagger UI.
if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();
}

app.UseAuthentication();
app.UseAuthorization();

// Liveness only says the process is up; readiness also checks the database.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

var v1 = app.MapGroup("/api/v1");
v1.MapIdentityEndpoints();
v1.MapUsersEndpoints();
v1.MapCatalogEndpoints();
v1.MapLocationsEndpoints();
v1.MapInventoryEndpoints();
v1.MapReceiptEndpoints();

await app.InitializeDatabaseAsync();
await app.RunAsync();

public partial class Program;
