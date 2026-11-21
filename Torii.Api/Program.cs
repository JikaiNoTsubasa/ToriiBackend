using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Torii.Api.Data;
using Torii.Api.Handlers;
using Torii.Api.Json;
using Torii.Api.Services;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "ToriiFrontend";

// --- Configuration ---
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

// --- Base de données (PostgreSQL via EF Core) ---
builder.Services.AddDbContext<ToriiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ToriiDb")));

// --- Services applicatifs ---
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// --- Authentification JWT Bearer ---
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
                    ? new string('0', 32) // placeholder pour permettre le démarrage sans clé configurée
                    : jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

// --- CORS pour le front Angular en dev ---
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()));

// --- Gestion centralisée des erreurs au format ProblemDetails (RFC 9457) ---
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
    {
        // Contexte commun ajouté à toutes les réponses d'erreur.
        ctx.ProblemDetails.Instance ??=
            $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}";
        ctx.ProblemDetails.Extensions.TryAdd("traceId",
            Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
    });
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// --- JSON : enums en chaîne MAJUSCULE, comme en base ---
// Contrôleurs (MVC) et reste du pipeline (ProblemDetails, OpenAPI) partagent la même règle.
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.UseUpperCaseEnumStrings());
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.UseUpperCaseEnumStrings());
builder.Services.AddOpenApi();

var app = builder.Build();

// Doit précéder les autres middlewares pour capter toutes les exceptions.
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

app.Run();
