using System.ComponentModel.DataAnnotations;
using Enver;
using Enver.Binding;
using Enver.Extensions.Configuration;
using Enver.Extensions.Options;
using Microsoft.Extensions.Options;
#pragma warning disable CA1050 // Declare types in namespaces

var builder = WebApplication.CreateBuilder(args);

// Insert the four-tier .env ladder into the configuration pipeline, slotted
// immediately before the environment-variables source so that platform env vars
// and command-line args still win.
//
// Files loaded (all optional), in ascending precedence:
//   .env
//   .env.{environment}          e.g. .env.development
//   .env.local
//   .env.{environment}.local    e.g. .env.development.local
//
// The environment name is inferred from ASPNETCORE_ENVIRONMENT (default: production).
builder.Configuration.AddDotEnvFiles();

// Bind SampleConfig (below) from configuration as options. Its generated binder
// builds the records, so no parameterless constructors or setters are needed. A
// missing key or a failed [Range] stops the app at startup.
builder.Services.AddEnvOptions<SampleConfig>();

var app = builder.Build();

// Standard IConfiguration access. Keys are read as-is from the .env file.
// Tip: double-underscore (__) in .env keys maps to the IConfiguration section
// delimiter (:), e.g. DB__HOST becomes accessible as config["DB:HOST"].
app.MapGet("/", (IConfiguration config) => new Dictionary<string, Dictionary<string, string?>?>
    {
        ["app"] = new()
        {
            ["name"] = config["APP_NAME"],
            ["env"] = config["APP_ENV"],
            ["debug"] = config["APP_DEBUG"],
        },
        ["db"] = new()
        {
            ["host"] = config["DB_HOST"],
            ["port"] = config["DB_PORT"],
            ["name"] = config["DB_NAME"],
        },
        ["api"] = new()
        {
            ["url"] = config["API_URL"],
            ["timeout"] = config["API_TIMEOUT_SECONDS"],
            ["retries"] = config["API_MAX_RETRIES"],
        },
    });

// Typed binding through the options package: inject IOptions<SampleConfig> (or
// IOptionsMonitor, which rebinds when configuration reloads).
app.MapGet("/typed", (IOptions<SampleConfig> options) => options.Value);

// AsEnvReader() bridges IConfiguration to IEnvReader for one-off typed reads
// (GetInt32, GetBoolean, GetUri, ...) over any config source.
app.MapGet("/reader", (IConfiguration config) =>
{
    var env = config.AsEnvReader();
    return new ReaderValues(
        env.GetBoolean("APP_DEBUG", false),
        env.GetInt32("API_TIMEOUT_SECONDS", 30)
    );
});

app.Run();

// [EnvBindable] generates the binder. Members typed as other records bind as
// subsections keyed by the property name: App.Name reads APP_NAME, Db.Port reads
// DB_PORT. Parameters with defaults are optional.
[EnvBindable]
public partial record SampleConfig(AppSettings App, DbSettings Db, ApiSettings Api);

public sealed record AppSettings(string Name, string Env = "production", bool Debug = false);

public sealed record DbSettings(
    string Host,
    string Name,
    [property: Range(1, 65535)] int Port = 5432
);

public sealed record ApiSettings(
    Uri Url,
    [property: Range(1, 300)] int TimeoutSeconds = 30,
    int MaxRetries = 3
);

// "/reader" returns values read one at a time through IEnvReader.
public sealed record ReaderValues(bool Debug, int ApiTimeoutSeconds);
