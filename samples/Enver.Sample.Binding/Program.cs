using Enver;
using Enver.Binding;
#pragma warning disable CA1050 // Declare types in namespaces
// The source generator adds static Bind* factory methods directly to each
// [EnvBindable] type, so binding is a single method call per config class.
var path = DotEnvPaths.AppDirectory();
var app = AppConfig.Bind(path);
var db = DbConfig.Bind(path);
var api = ApiConfig.Bind(path);

Console.WriteLine($"Starting {app.Name}  [{app.Env}]  (debug={app.Debug})");
Console.WriteLine($"  Database : {db.Host}:{db.Port}/{db.Name}");
Console.WriteLine($"  Pool     : {db.Pool.MinSize}-{db.Pool.MaxSize} connections");
Console.WriteLine(
    $"  API      : {api.Url}  timeout={api.TimeoutSeconds}s  retries={api.MaxRetries}"
);

// [EnvConfig] sets a key prefix and naming convention (default: UPPER_SNAKE_CASE).
// [EnvBindable] triggers the source generator.
// The type must be partial so the generator can augment it with the Bind* methods.

[EnvConfig("APP")]
[EnvBindable]
public partial record AppConfig(
    string Name, // APP_NAME - required
    string Env, // APP_ENV - required
    bool Debug // APP_DEBUG - required
);

[EnvConfig("DB")]
[EnvBindable]
public partial class DbConfig
{
    public required string Host { get; init; } // DB_HOST - required
    public required int Port { get; init; } // DB_PORT - required
    public required string Name { get; init; } // DB_NAME - required

    [EnvKey("POOL")]
    public required PoolConfig Pool { get; init; }
}

public class PoolConfig
{
    public int MinSize { get; init; } = 1; // DB_POOL_MIN_SIZE - optional, default 1
    public int MaxSize { get; init; } = 10; // DB_POOL_MAX_SIZE - optional, default 10
}

[EnvConfig("API")]
[EnvBindable]
public partial class ApiConfig
{
    public required Uri Url { get; init; } // API_URL - requred
    public int TimeoutSeconds { get; init; } = 30; // API_TIMEOUT_SECONDS - optional, default 30
    public int MaxRetries { get; init; } = 3; // API_MAX_RETRIES - optional, default 3
}
