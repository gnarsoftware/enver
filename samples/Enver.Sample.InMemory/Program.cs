// Load .env from the app's output directory.
// Passing variant: "local" also loads .env.local (if present), with .env.local winning
// on any keys that appear in both files. Useful for per-machine developer overrides.

using Enver;

var env = EnvCollection.FromAppDirectory(variant: "local");

// Required values. EnverException is thrown if a key is missing or cannot be parsed.
string appName = env.GetString("APP_NAME");
string dbHost = env.GetString("DB_HOST");
int dbPort = env.GetInt32("DB_PORT");
string dbName = env.GetString("DB_NAME");
Uri apiUrl = env.GetUri("API_URL");

// Optional values. supply a default that is used when the key is absent.
// A present-but-invalid value (e.g. "yes" for a bool) still throws.
bool debug = env.GetBoolean("APP_DEBUG", false);
int apiTimeout = env.GetInt32("API_TIMEOUT_SECONDS", 30);
int maxRetries = env.GetInt32("API_MAX_RETRIES", 3);

Console.WriteLine($"Starting {appName}  [{env.GetString("APP_ENV")}]  (debug={debug})");
Console.WriteLine($"  Database : {dbHost}:{dbPort}/{dbName}");
Console.WriteLine($"  API      : {apiUrl}  timeout={apiTimeout}s  retries={maxRetries}");
Console.WriteLine();

// EnvCollection is a Dictionary<string, string>, so all standard dictionary
// operations are available.
Console.WriteLine($"Loaded {env.Count} variable(s):");
foreach (var (key, value) in env)
{
    Console.WriteLine($"  {key} = {value}");
}
