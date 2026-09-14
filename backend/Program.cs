using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Trippy.Backend.Data;
using Trippy.Backend.Interfaces;
using Trippy.Backend.Options;
using Trippy.Backend.Repositories;
using Trippy.Backend.Services;
using Trippy.Backend.Services.Tools;

var builder = WebApplication.CreateBuilder(args);

// Env vars that Railway / docker-compose commonly set
builder.Configuration.AddEnvironmentVariables();

// Railway sets PORT; local launchSettings uses 8000.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://*:{port}");

// ---- Options (bind from appsettings + common env var names) ----
builder.Services.Configure<OpenAiOptions>(opts =>
{
    builder.Configuration.GetSection(OpenAiOptions.SectionName).Bind(opts);
    opts.ApiKey = FirstNonEmpty(
        opts.ApiKey,
        builder.Configuration["OPENAI_API_KEY"],
        Environment.GetEnvironmentVariable("OPENAI_API_KEY")) ?? "";
    opts.Model = FirstNonEmpty(
        opts.Model,
        builder.Configuration["OPENAI_MODEL"],
        Environment.GetEnvironmentVariable("OPENAI_MODEL"),
        "gpt-4o-mini")!;
});

// ---- Database (Railway injects DATABASE_URL; local dev uses ConnectionStrings:Default) ----
var connectionString = ResolveConnectionString(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

// ---- Repositories ----
builder.Services.AddScoped<IPlaceRepository, PlaceRepository>();
builder.Services.AddScoped<IItineraryRepository, ItineraryRepository>();
builder.Services.AddScoped<ISectionRepository, SectionRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IPendingActionRepository, PendingActionRepository>();

// ---- Services ----
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<PendingActionExecutor>();
builder.Services.AddScoped<IPlaceService, PlaceService>();
builder.Services.AddScoped<IItineraryService, ItineraryService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IPendingActionService, PendingActionService>();

// Agent tools — register additional IAgentTool implementations here.
builder.Services.AddScoped<IAgentTool, QueryDbTool>();
builder.Services.AddScoped<IAgentTool, DistanceTool>();
builder.Services.AddScoped<IAgentTool, ProposeMutationTool>();
builder.Services.AddScoped<IChatAgent, ChatAgentService>();

// ---- MVC / JSON ----
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        // Match the snake_case field names Orval generates for the React client.
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// ---- Swagger / OpenAPI (source of truth for Orval codegen) ----
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Trippy API", Version = "v1" });
    // Prefer explicit [HttpGet(Name=...)] operationIds for stable Orval hooks.
    c.CustomSchemaIds(t => t.Name);
    c.SupportNonNullableReferenceTypes();
});

// ---- CORS ----
var corsOrigins = builder.Configuration["CORS_ORIGINS"]
    ?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await seeder.SeedAsync();
}

app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();

// Serve the Vite build when present (Docker / Railway). API routes take precedence.
var staticRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(staticRoot))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapControllers();

// SPA fallback — keep API 404s intact.
if (Directory.Exists(staticRoot))
{
    app.MapFallbackToFile("index.html");
}

app.Run();

// ---- Local helpers ----

static string? FirstNonEmpty(params string?[] values) =>
    values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

/// <summary>Railway injects postgresql://... — Npgsql wants Host=... format.</summary>
static string ResolveConnectionString(IConfiguration configuration)
{
    var url = configuration["DATABASE_URL"]
              ?? Environment.GetEnvironmentVariable("DATABASE_URL")
              ?? configuration.GetConnectionString("Default");

    if (string.IsNullOrWhiteSpace(url))
        throw new InvalidOperationException(
            "No database connection configured. Set DATABASE_URL or ConnectionStrings:Default.");

    if (url.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        return ConvertPostgresUrl(url);
    }

    return url;
}

static string ConvertPostgresUrl(string url)
{
    var uri = new Uri(url);
    var userInfo = uri.UserInfo.Split(':', 2);
    var username = Uri.UnescapeDataString(userInfo[0]);
    var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
    var database = uri.AbsolutePath.Trim('/');
    return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={database};Username={username};Password={password};SSL Mode=Prefer";
}

// Needed so integration tests / orval dump tooling can reference the assembly.
public partial class Program;
