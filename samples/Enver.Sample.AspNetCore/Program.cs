using Enver;
using Enver.Extensions.Configuration;
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

// AsEnvReader() bridges IConfiguration to IEnvReader, giving access to the full
// typed-accessor surface (GetInt32, GetBoolean, GetUri, …) over any config source,
// not just .env files.
app.MapGet("/typed", (IConfiguration config) =>
{
    var env = config.AsEnvReader();

    string appName = env.GetString("APP_NAME");
    string appEnv = env.GetString("APP_ENV", "production");
    bool debug = env.GetBoolean("APP_DEBUG", false);
    string dbHost = env.GetString("DB_HOST");
    int dbPort = env.GetInt32("DB_PORT", 5432);
    string dbName = env.GetString("DB_NAME");
    Uri apiUrl = env.GetUri("API_URL");
    int apiTimeout = env.GetInt32("API_TIMEOUT_SECONDS", 30);
    int maxRetries = env.GetInt32("API_MAX_RETRIES", 3);

    return new TypedConfig(
        new TypedApp(appName, appEnv, debug),
        new TypedDb(dbHost, dbPort, dbName),
        new TypedApi(apiUrl, apiTimeout, maxRetries)
    );
});

app.Run();

// "/typed" returns values parsed through IEnvReader.
public sealed record TypedConfig(TypedApp App, TypedDb Db, TypedApi Api);

public sealed record TypedApp(string Name, string Env, bool Debug);

public sealed record TypedDb(string Host, int Port, string Name);

public sealed record TypedApi(Uri Url, int Timeout, int Retries);
