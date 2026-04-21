using BPFL.API.BackgroundJobs;
using BPFL.API.Config;
using BPFL.API.Data;
using BPFL.API.Middleware;
using BPFL.API.Modules.AI.Application.Interfaces;
using BPFL.API.Modules.AI.Application.UseCases;
using BPFL.API.Modules.AI.Applications.Interfaces;
using BPFL.API.Modules.AI.Infrastructures.Repositories;
using BPFL.API.Modules.Odds.Application.Interfaces;
using BPFL.API.Modules.Odds.Application.Repositories;
using BPFL.API.Modules.Wallet.Applications.Interfaces;
using BPFL.API.Modules.Wallet.Applications.UseCases;
using BPFL.API.Modules.Wallet.Infrastructures.Repositories;
using BPFL.API.Services;
using BPFL.API.Services.Agents;
using BPFL.API.Services.External;
using BPFL.API.Services.FantasyServices;
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

string baseUrl = builder.Configuration["FootballData:BaseUrl"]!;
string token = builder.Configuration["FootballData:Token"]!;

Console.WriteLine($"[DEBUG] BaseUrl: '{baseUrl}'");
Console.WriteLine($"[DEBUG] Token set: {!string.IsNullOrWhiteSpace(token)}");

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

builder.Services.AddScoped<MatchPredictionAgent>();

builder.Services.AddHttpClient<BPFLDataClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["FootballData:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(90);

    var token = builder.Configuration["FootballData:Token"];
    if (!string.IsNullOrWhiteSpace(token))
    {
        client.DefaultRequestHeaders.Add("X-Auth-Token", token);
    }
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("https://predictionfootballgame.vercel.app")
              .AllowAnyHeader()
              .AllowAnyMethod()
             .AllowCredentials();
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


// Add services to the container.
builder.Services.AddScoped<AuthServices>();
builder.Services.AddScoped<TeamService>();
builder.Services.AddScoped<TeamSyncService>();
builder.Services.AddScoped<MatchSyncService>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<MatchAnalysisService>();
builder.Services.AddScoped<PredictionModelService>();
builder.Services.AddScoped<PredictionScoringService>();
builder.Services.AddScoped<AIPredictionService>();
builder.Services.AddScoped<LeaderboardService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddScoped<GoogleAuthService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<FantasyServices>();
builder.Services.AddScoped<FantasyAutoSyncService>();
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<OddsService>();
builder.Services.AddScoped<BetService>();

builder.Services.AddHostedService<MatchSyncJob>();
builder.Services.AddHostedService<PredictionScoringJob>();

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<GetWallet>();

builder.Services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
builder.Services.AddScoped<ResetDemoBalanceUseCase>();

builder.Services.AddScoped<IMatchMarketOddsRepository, MatchMarketOddsRepository>();

builder.Services.AddScoped<IMatchContextRepository, MatchContextRepository>();

builder.Services.AddScoped<IHeadToHeadRepository, HeadToHeadRepository>();

builder.Services.AddScoped<GetMatchContext>();
builder.Services.AddScoped<GetHeadToHead>();



builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
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

// Configure the HTTP request pipeline.
/*
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
*/

app.UseSwagger();
app.UseSwaggerUI();


app.UseCors("AllowFrontend");
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
