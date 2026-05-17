using Enver;
using Enver.Extensions.Configuration;

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
app.MapGet(
    "/",
    (IConfiguration config) =>
    {
        return new
        {
            app = new
            {
                name = config["APP_NAME"],
                env = config["APP_ENV"],
                debug = config["APP_DEBUG"],
            },
            db = new
            {
                host = config["DB_HOST"],
                port = config["DB_PORT"],
                name = config["DB_NAME"],
            },
            api = new { url = config["API_URL"], timeout = config["API_TIMEOUT_SECONDS"] },
        };
    }
);

// AsEnvReader() bridges IConfiguration to IEnvReader, giving access to the full
// typed-accessor surface (GetInt32, GetBoolean, GetUri, …) over any config source,
// not just .env files.
app.MapGet(
    "/typed",
    (IConfiguration config) =>
    {
        var env = config.AsEnvReader();

        string appName = env.GetString("APP_NAME");
        bool debug = env.GetBoolean("APP_DEBUG", false);
        string dbHost = env.GetString("DB_HOST");
        int dbPort = env.GetInt32("DB_PORT", 5432);
        string dbName = env.GetString("DB_NAME");
        Uri apiUrl = env.GetUri("API_URL");
        int apiTimeout = env.GetInt32("API_TIMEOUT_SECONDS", 30);
        int maxRetries = env.GetInt32("API_MAX_RETRIES", 3);

        return new
        {
            app = new { name = appName, debug },
            db = new
            {
                host = dbHost,
                port = dbPort,
                name = dbName,
            },
            api = new
            {
                url = apiUrl.ToString(),
                timeout = apiTimeout,
                retries = maxRetries,
            },
        };
    }
);

app.Run();
