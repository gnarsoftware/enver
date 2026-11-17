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

// [EnvBindable] triggers the source generator. It can also set a key prefix and
// naming convention (default: UPPER_SNAKE_CASE), e.g. [EnvBindable("MYAPP")].
// The type must be partial so the generator can augment it with the Bind* methods.
//
// Members typed as other config objects bind as subsections, keyed by the
// property name: App.Name reads APP_NAME. Use [EnvKey("...")] on the property to
// rename the segment, or [EnvKey("")] to drop it.

[EnvBindable]
public partial record Configuration(
    AppConfig App,
    DbConfig Db,
    ApiConfig Api,
    StorageConfig Storage
);

public record AppConfig(
    string Name, // APP_NAME - required
    string Env, // APP_ENV - required
    bool Debug // APP_DEBUG - required
);

public class DbConfig
{
    // DB_HOST - required. [EnvFallbackKey] adds keys to try when DB_HOST isn't
    // set, in order: DB_SERVER (the prefix still applies), then PGHOST (used as
    // written). DB_HOST wins whenever it is set, wherever it appears in the file.
    [EnvFallbackKey("SERVER")]
    [EnvFallbackKey("PGHOST", IgnorePrefix = true)]
    public required string Host { get; init; }
    public required int Port { get; init; } // DB_PORT - required
    public required string Name { get; init; } // DB_NAME - required
    public required PoolConfig Pool { get; init; } // DB_POOL_* - subsection
}

public class PoolConfig
{
    public int MinSize { get; init; } = 1; // DB_POOL_MIN_SIZE - optional, default 1
    public int MaxSize { get; init; } = 10; // DB_POOL_MAX_SIZE - optional, default 10
}

public class ApiConfig
{
    public required Uri Url { get; init; } // API_URL - required
    public int TimeoutSeconds { get; init; } = 30; // API_TIMEOUT_SECONDS - optional, default 30
    public int MaxRetries { get; init; } = 3; // API_MAX_RETRIES - optional, default 3
}

// Polymorphic binding:
// Binds automatically to closed types or unions.
// Use [EnvPolymorphic(TypeKey = "TYPE")] to manually opt in on any type along
// with [EnvDerivedType<T>] for each bindable case.
// [EnvPolymorphic] can also be used on closed/union types to set an alternative type key
// or case style.

public closed record StorageConfig; // STORAGE_TYPE - required

// Use [EnvCase("REMOTE")] to explicitly set the case name.
public sealed record RemoteStorageConfig( // STORAGE_TYPE=remote
    string Bucket, // STORAGE_BUCKET - required in this case
    string Region // STORAGE_REGION - required in this case
) : StorageConfig;

public sealed record LocalStorageConfig( // STORAGE_TYPE=local
    string Path // STORAGE_PATH - required in this case
) : StorageConfig;
