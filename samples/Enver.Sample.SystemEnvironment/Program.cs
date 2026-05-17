// Load .env into the process environment block via Environment.SetEnvironmentVariable.
// After this call, any code that reads Environment.GetEnvironmentVariable will see
// the values.
//
// overrideExisting: false (default) means variables already set in the shell are kept,
// so deployment-platform env vars always win over .env file values.

using Enver;

// (C# 14 extension-member syntax; use EnvironmentExtensions on older compilers.)
Environment.LoadDotEnvFromAppDirectory();

// Legacy-style access
string? appName = Environment.GetEnvironmentVariable("APP_NAME");
string? dbHost = Environment.GetEnvironmentVariable("DB_HOST");

Console.WriteLine($"App : {appName}  [{Environment.GetEnvironmentVariable("APP_ENV")}]");
Console.WriteLine($"DB  : {dbHost}");

// Typed access via Environment.Variables: the full IEnvReader surface over the process env.
// (C# 14 extension-member syntax; call EnvironmentExtensions.Variables on older compilers.)
int dbPort = Environment.Variables.GetInt32("DB_PORT");
string dbName = Environment.Variables.GetString("DB_NAME");
Uri apiUrl = Environment.Variables.GetUri("API_URL");
int apiTimeout = Environment.Variables.GetInt32("API_TIMEOUT_SECONDS", 30);
int maxRetries = Environment.Variables.GetInt32("API_MAX_RETRIES", 3);
bool debug = Environment.Variables.GetBoolean("APP_DEBUG", false);

Console.WriteLine($"  Database : {dbHost}:{dbPort}/{dbName}");
Console.WriteLine($"  API      : {apiUrl}  timeout={apiTimeout}s  retries={maxRetries}");
Console.WriteLine($"  Debug    : {debug}");
