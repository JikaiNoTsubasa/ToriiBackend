using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Torii.Api.Data;
using Torii.Api.Data.Seeders;
using Torii.Api.Handlers;
using Torii.Api.Json;
using Torii.Api.Managers;
using Torii.Api.Services;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "ToriiFrontend";

// --- Configuration ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
builder.Services.Configure<FileStorageOptions>(builder.Configuration.GetSection("Storage"));

// --- Database (PostgreSQL through EF Core) ---
builder.Services.AddDbContext<ToriiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ToriiDb")));

// --- Application services ---
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<AdminSeeder>();
builder.Services.AddScoped<TicketManager>();
builder.Services.AddScoped<CustomerManager>();
builder.Services.AddScoped<UserManager>();
builder.Services.AddScoped<TicketEdgeManager>();
builder.Services.AddScoped<ProfileManager>();
builder.Services.AddScoped<NotificationManager>();
builder.Services.AddSingleton<IFileStorage, DiskFileStorage>();

// --- JWT Bearer authentication ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(string.IsNullOrEmpty(jwt.Key)
                    ? new string('0', 32) // placeholder so the API can start without a configured key
                    : jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// --- CORS for the Angular frontend in dev ---
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// --- Centralized error handling as ProblemDetails (RFC 9457) ---
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
    {
        // Common context added to every error response.
        ctx.ProblemDetails.Instance ??=
            $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions.TryAdd("traceId",
            Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- JSON: enums as UPPERCASE strings, as in the database ---
// Controllers (MVC) and the rest of the pipeline (ProblemDetails, OpenAPI) share the same rule.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.UseUpperCaseEnumStrings());
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UseUpperCaseEnumStrings());
builder.Services.AddOpenApi();

var app = builder.Build();

// Must come before the other middlewares to catch every exception.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// --- Initial data (first admin account) ---
await app.SeedDatabaseAsync();

app.Run();
