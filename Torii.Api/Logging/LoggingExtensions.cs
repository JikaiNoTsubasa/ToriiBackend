using Serilog;
using Serilog.Events;
using Serilog.Extensions.Hosting;
using Serilog.Sinks.SystemConsole.Themes;

namespace Torii.Api.Logging;

/// <summary>
/// Serilog setup: colored console + daily rolling text files, with shared templates.
/// Sinks and templates live here so the API logs correctly even without a <c>Serilog</c>
/// configuration section; that section only tunes levels (and can add sinks).
/// </summary>
public static class LoggingExtensions
{
    /// <summary>Short template for the console: time, level, source, message.</summary>
    public const string ConsoleTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Full template for the files: date with offset, level, source, message.</summary>
    public const string FileTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    private const long FileSizeLimitBytes = 10 * 1024 * 1024;
    private const int RetainedFileCount = 14;

    /// <summary>
    /// Minimal console logger used until the host is built, so startup failures are visible.
    /// </summary>
    public static ReloadableLogger CreateBootstrapLogger() => new LoggerConfiguration()
        .WriteTo.Console(outputTemplate: ConsoleTemplate, theme: AnsiConsoleTheme.Code)
        .CreateBootstrapLogger();

    /// <summary>
    /// Replaces the default logging providers with Serilog.
    /// Files go to <c>logs/torii-YYYYMMDD.log</c> under the content root, roll daily or
    /// at 10 MB, and the last 14 files are kept.
    /// </summary>
    public static WebApplicationBuilder AddToriiLogging(this WebApplicationBuilder builder)
    {
        var logFile = Path.Combine(builder.Environment.ContentRootPath, "logs", "torii-.log");

        builder.Services.AddSerilog((services, logger) => logger
            // Defaults, overridable from the "Serilog" configuration section.
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            // EF Core is silenced (SQL commands, warnings, errors): a failing query still
            // reaches GlobalExceptionHandler, which logs it once.
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Fatal)
            .MinimumLevel.Override("System.Net.Http", LogEventLevel.Warning)
            .ReadFrom.Configuration(builder.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: ConsoleTemplate, theme: AnsiConsoleTheme.Code)
            .WriteTo.File(
                logFile,
                outputTemplate: FileTemplate,
                rollingInterval: RollingInterval.Day,
                rollOnFileSizeLimit: true,
                fileSizeLimitBytes: FileSizeLimitBytes,
                retainedFileCountLimit: RetainedFileCount));

        return builder;
    }

    /// <summary>
    /// One concise line per HTTP request (method, path, status, duration), instead of the
    /// many lines ASP.NET Core writes by default. 5xx → error, 4xx → warning.
    /// </summary>
    public static WebApplication UseToriiRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate =
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0} ms";
            options.GetLevel = (httpContext, _, exception) =>
                exception is not null || httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error
                : httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning
                : LogEventLevel.Information;
        });

        return app;
    }
}
