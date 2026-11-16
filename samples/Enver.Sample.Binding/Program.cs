using Enver;
using Enver.Binding;
#pragma warning disable CA1050 // Declare types in namespaces
// The source generator adds static Bind* factory methods directly to each
// [EnvBindable] type, so binding is a single method call per config class.
var (app, db, api, storage) = Configuration.Bind(DotEnvPaths.AppDirectory());

Console.WriteLine($"Starting {app.Name}  [{app.Env}]  (debug={app.Debug})");
Console.WriteLine($"  Database : {db.Host}:{db.Port}/{db.Name}");
Console.WriteLine($"  Pool     : {db.Pool.MinSize}-{db.Pool.MaxSize} connections");
Console.WriteLine(
    $"  API      : {api.Url}  timeout={api.TimeoutSeconds}s  retries={api.MaxRetries}"
);
Console.WriteLine(
    storage switch
    {
        RemoteStorageConfig remote =>
            $"  Storage  : Remote - {remote.Region} region; {remote.Bucket} bucket",
        LocalStorageConfig(var path) => $"  Storage  : Local - {path}",
    }
);

// [EnvConfig] sets a key prefix and naming convention (default: UPPER_SNAKE_CASE).
// [EnvBindable] triggers the source generator.
// The type must be partial so the generator can augment it with the Bind* methods.

[EnvConfig]
[EnvBindable]
public partial record Configuration(AppConfig App, DbConfig Db, ApiConfig Api, StorageConfig Storage);

[EnvConfig]
public partial record AppConfig(
    string Name, // APP_NAME - required
    string Env, // APP_ENV - required
    bool Debug // APP_DEBUG - required
);

[EnvConfig]
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

[EnvConfig]
public partial class ApiConfig
{
    public required Uri Url { get; init; } // API_URL - requred
    public int TimeoutSeconds { get; init; } = 30; // API_TIMEOUT_SECONDS - optional, default 30
    public int MaxRetries { get; init; } = 3; // API_MAX_RETRIES - optional, default 3
}

[EnvPolymorphic(TypeKey = "TYPE")] // Explicitly set type key. Not required for 'TYPE'.
public closed record StorageConfig; // STORAGE_TYPE - required

[EnvCase("REMOTE")] // explicitly set the case name. Not required in this case.
public sealed record RemoteStorageConfig( // STORAGE_TYPE=remote
    string Bucket, // STORAGE_BUCKET - required in this case
    string Region // STORAGE_REGION - required in this case
) : StorageConfig;

public sealed record LocalStorageConfig( // STORAGE_TYPE=local
    string Path // STORAGE_PATH - required in this case
) : StorageConfig;
