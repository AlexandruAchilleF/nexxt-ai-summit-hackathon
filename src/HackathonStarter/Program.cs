using HackathonStarter.Agents;
using HackathonStarter.Api;
using HackathonStarter.Components;
using HackathonStarter.Data;
using HackathonStarter.Features.Arena;
using HackathonStarter.Features.Assistant;
using HackathonStarter.Features.Checks;
using HackathonStarter.Features.Exports;
using HackathonStarter.Features.Items;
using HackathonStarter.Features.Scenarios;
using HackathonStarter.Features.Targets;
using HackathonStarter.Services;
using Microsoft.AspNetCore.DataProtection;
using MudBlazor.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Local secrets (git-ignored): API keys etc. Environment variables override everything (AI__ApiKey=...).
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();

builder.AddAppDatabase();
builder.AddDemoAuth();
builder.Services.AddScoped<DemoData>();

// Public-deployment protection (all off by default): access code, API key, AI rate limits.
builder.Services.Configure<SecurityOptions>(builder.Configuration.GetSection("Security"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AgentRateLimiter>();
builder.AddApiCors(); // only active for origins listed in Security:AllowedOrigins

// HTTP API docs: /openapi/v1.json and interactive /api-docs. Enums as text ("Completed", not 1), like the stream.
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    o.SerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// AI (Microsoft.Extensions.AI): Anthropic, OpenAI-compatible (Featherless, Ollama…) or Azure OpenAI.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));
builder.Services.AddSingleton<ChatClientProvider>();
builder.Services.AddScoped<StructuredAi>();

// Privacy at rest: run history is saved masked; pending approvals keep their arguments encrypted.
builder.Services.Configure<PrivacyOptions>(builder.Configuration.GetSection("Privacy"));
// Keys live in the database, so they last exactly as long as the data they protect: with a persistent database
// (Postgres) a restart no longer logs everyone out or leaves pending approvals undecryptable. With SQLite inside the
// container (Render free plan) keys and data still reset together on each deploy. Trade-off: anyone holding a copy of
// the database also holds the keys; production would keep them in a key vault (ProtectKeysWith...). /privacy says so.
// When the database is recreated on every start (Development), the default key store (user profile) is kept.
var dataProtection = builder.Services.AddDataProtection();
if (!builder.Configuration.GetValue<bool>("Database:RecreateOnStartup"))
    dataProtection.PersistKeysToDbContext<AppDbContext>();
builder.Services.AddSingleton<PiiSealer>();
builder.Services.AddSingleton<RetentionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RetentionService>());

// External systems (chatbots under test, partner APIs, models on your own server): Targets:<name>.
builder.Services.Configure<Dictionary<string, TargetOptions>>(builder.Configuration.GetSection("Targets"));
builder.Services.AddHttpClient("targets");
builder.Services.AddSingleton<TargetRecordings>();
builder.Services.AddSingleton<TargetClient>();
builder.Services.AddScoped<ConnectionCheck>();

// Deterministic checks (verdicts by code, not by a model) + exports of runs (tests, reports), approved by a person.
builder.Services.AddChecks(builder.Configuration);
builder.Services.AddSingleton<IRunExporter, JsonTestCaseExporter>();
builder.Services.AddSingleton<IRunExporter, XunitTestExporter>();
builder.Services.AddSingleton<IRunExporter, MarkdownReportExporter>();
builder.Services.AddScoped<RunExportService>();

// Agents: register yours here.
builder.Services.AddSingleton<AgentDefinition, AssistantAgent>();
builder.Services.AddSingleton<AgentDefinition, DebatePanel>();
builder.Services.AddSingleton<AgentDefinition, CustomerProbeScenario>();
builder.Services.AddSingleton<Recordings>();
builder.Services.AddScoped<RunStore>();
builder.Services.AddScoped<AgentRunner>();
builder.Services.AddSingleton<AgentRunService>();

// Feature services.
builder.Services.AddScoped<ItemService>();
// @gen:services (the feature generator inserts new services above this line)

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseLang(); // UI language from a cookie (Services/Lang.cs)
app.UseCors(); // before the access gate, so browser preflight requests from allowed origins get through
app.UseAccessGate();
app.UseDemoAuth();
app.UseAntiforgery();

app.MapStaticAssets();
// Health check for hosting platforms (Render, Azure): GET /health → 200 "ok".
app.MapGet("/health", () => "ok");
app.MapPwa();
app.MapAgentApi();
app.MapPlatformApi();
app.MapOpenApi();
app.MapScalarApiReference("/api-docs");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await app.InitializeDatabaseAsync();
app.Run();
