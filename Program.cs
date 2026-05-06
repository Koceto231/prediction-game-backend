using BPFL.API.BackgroundJobs;
using BPFL.API.Config;
using BPFL.API.Data;
using BPFL.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAppCache, MemoryAppCache>();

builder.Services.AddDbContext<BPFL_DBContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(10),
                errorCodesToAdd: null);
        }));




Console.WriteLine($"[DEBUG] BaseUrl: '{builder.Configuration["FootballData:BaseUrl"]}'");
Console.WriteLine($"[DEBUG] Token set: {!string.IsNullOrWhiteSpace(builder.Configuration["FootballData:Token"])}");

builder.Services.Configure<OpenRouterSettings>(
    builder.Configuration.GetSection("OpenRouter"));

builder.Services.AddHttpClient<OpenRouterClient>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<OpenRouterSettings>>().Value;
    client.BaseAddress = new Uri(settings.BaseUrl);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<BPFLDataClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FootballData:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(90);

    var token = builder.Configuration["FootballData:Token"];
    if (!string.IsNullOrWhiteSpace(token))
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };

        // Read JWT from HttpOnly cookie instead of Authorization header
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                ctx.Token = ctx.Request.Cookies["access_token"];
                return Task.CompletedTask;
            }
        };
    });

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: allow any origin so Swagger + local frontend work freely
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Production: only explicitly configured origins
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ── Core services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<AuthServices>();
builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ProfileService>();

// ── Match / Team ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<MatchSyncService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<MatchAnalysisService>();

// ── Prediction / AI ──────────────────────────────────────────────────────────
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<PredictionModelService>();
builder.Services.AddScoped<PredictionScoringService>();
builder.Services.AddScoped<AIPredictionService>();
builder.Services.AddScoped<MatchPredictionAgent>();

// ── Betting / Wallet / Odds ───────────────────────────────────────────────────
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<OddsService>();
builder.Services.AddScoped<BetService>();

// ── Leagues / Leaderboard / Fantasy ──────────────────────────────────────────
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<FantasyServices>();
builder.Services.AddScoped<FantasyAutoSyncService>();
builder.Services.AddScoped<SportmonksMatchSyncService>();
builder.Services.AddScoped<ApiSportsPlayerSeedService>();
builder.Services.AddHttpClient<SportmonksClient>();
builder.Services.AddHttpClient<ApiSportsClient>();

// ── News ──────────────────────────────────────────────────────────────────────
// Pollinations.AI — free image generation, no API key needed
builder.Services.AddHttpClient<StabilityAIClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
builder.Services.AddSingleton<CloudinaryUploader>();
builder.Services.AddScoped<NewsAgent>();
builder.Services.AddScoped<NewsService>();

// ── Background jobs ───────────────────────────────────────────────────────────
builder.Services.AddHostedService<MatchSyncJob>();
builder.Services.AddHostedService<PredictionScoringJob>();
builder.Services.AddHostedService<NewsGenerationJob>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BPFL API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BPFL_DBContext>();
    db.Database.Migrate();
}

app.UseRouting();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowFrontend");
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
