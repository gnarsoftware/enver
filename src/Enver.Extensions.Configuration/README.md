# Enver.Extensions.Configuration

`Microsoft.Extensions.Configuration` integration for [Enver](https://github.com/gnarsoftware/enver).
Two things in one small package:

1. **`AddDotEnvFiles(...)`** on `IConfigurationBuilder` / `IConfigurationManager`
   - load `.env` files as a configuration source alongside JSON, env vars,
   and anything else you've wired up.
2. **`AsEnvReader()`** on `IConfiguration`
   - apply Enver's typed-accessor surface (`GetInt32`, `GetRequired<T>`, `GetEnum<T>`, ...)
   to any configuration source.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

```sh
dotnet add package Gnar.Enver.Extensions.Configuration
```

The drop-in replacement for `appsettings.json` / `appsettings.{Environment}.json`:

```csharp
using Enver.Extensions.Configuration;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddDotEnvFiles();
//  loads .env, .env.{environment}, .env.local, .env.{environment}.local
//  in precedence order; every file is optional

string? dbHost = builder.Configuration["Database:Host"];
int port = builder.Configuration.AsEnvReader().GetInt32("Database:Port", 5432);
```

For the deployed-tier files to reach the published artifact, add this to
your `.csproj`:

```xml
<ItemGroup>
  <Content Include=".env;.env.*" Exclude=".env.local;.env.*.local"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

And the matching `.gitignore` entries to keep local files out of source control:

```gitignore
.env.local
.env.*.local
```

That's it for the common case. The rest of this README covers the lower-level
extensions for when you need more control.

## `AddDotEnvFiles`: the convention helper

Mirrors how the default host already loads `appsettings.json` +
`appsettings.{Environment}.json`. Both files are optional; the
environment name is auto-discovered from the configuration tree (reading
`ASPNETCORE_ENVIRONMENT` then `DOTNET_ENVIRONMENT`, falling back to
`Production`), and `reloadOnChange` defaults to `true`.

```csharp
builder.Configuration.AddDotEnvFiles();
```

Use the [explicit-paths overload](#adddotenvfiles-with-explicit-paths) to
specify your own set of files to load.

Auto-discovered environment names are lowercased, so
`ASPNETCORE_ENVIRONMENT=Development` resolves to `.env.development` (not
`.env.Development`). This matches the dotenv ecosystem convention
(`.env.development`, `.env.production`, `.env.test`).

### Source-control and deployment convention

The four-tier `.env` ladder splits cleanly into **shared** files that are
checked in and deployed, and **per-machine** files that stay local:

| File | Source-control | Deploy with app | Purpose |
|---|---|---|---|
| `.env` | check in | yes | shared defaults across all environments |
| `.env.{environment}` | check in | yes | shared defaults for a specific environment (e.g. `.env.development`, `.env.production`) |
| `.env.local` | never check in (`.gitignore`) | no | per-machine overrides |
| `.env.{environment}.local` | never check in (`.gitignore`) | no | per-machine per-environment overrides |

The MSBuild snippet from Quick start
(`<Content Include=".env;.env.*" Exclude=".env.local;.env.*.local">`)
ships the checked-in tier into the publish output. The `.local` files
are gitignored *and* excluded from publish.

#### Prefer `.env` gitignored?

Since all files are optional, you can choose to instead gitignore
`.env` and omit the usage of `.env.local` / `.env.*.local` entirely. Use this
MSBuild include and `.gitignore` pair instead:

```xml
<ItemGroup>
  <!-- Only .env.{environment} ships; .env stays local -->
  <Content Include=".env.*" Exclude=".env.local;.env.*.local"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

```gitignore
.env
.env.local
.env.*.local
```

## `AddDotEnvFiles` with explicit paths

Use the explicit-paths overload to specify your own set of files to load:

```csharp
// use your own variant with the conventional pattern:
builder.Configuration.AddDotEnvFiles(DotEnvPaths.Relative().Standard("dev"));
// loads .env, .env.dev, .env.local, .env.dev.local

// load your own pipeline:
builder.Configuration.AddDotEnvFiles([
  .. DotEnvPaths.Relative().WithFileName("development.env").WithLocal(),
  "/my/special/.env"
]);
// loads: development.env development.env.local /my/special/.env

// or just specify your own set of paths:
builder.Configuration.AddDotEnvFiles([".env", ".env.local"]);
```

## Precedence: where in the source list

`AddDotEnvFiles` inserts the entire `.env` ladder as a single block,
immediately before the first environment-variables source in the builder.
The resulting precedence from low to high:

```
host config -> appsettings.json -> appsettings.{Environment}.json -> user secrets
            -> AddDotEnvFiles(...)
            -> environment variables -> command-line args
```

Inside the `.env` tier, ordering matches the order they are declared.

`AddDotEnvFiles()` just reads the configured environment as the variant and wires up the standard
four-tier file convention. These three calls are roughly equivalent:

```csharp
builder.Configuration.AddDotEnvFiles();
```
```csharp
var env = builder.Environment.EnvironmentName.ToLowerInvariant();
builder.Configuration.AddDotEnvFiles(DotEnvPaths.Relative().Standard(env));
```
```csharp
var env = builder.Environment.EnvironmentName.ToLowerInvariant();
builder.Configuration.AddDotEnvFiles([".env", $".env.{env}", ".env.local", $".env.{env}.local"])
```

Outside the `.env` tier:
- `.env` files override JSON config files
- Environment variables override every `.env` layer
- Command-line args override everything

If no env-vars source is registered yet, the sources are appended.

## Key transform: `__` -> `:`

`.env` keys can't contain `:`, so the `__` convention is used to express
nested configuration sections, matching
`Microsoft.Extensions.Configuration.EnvironmentVariables`:

```
# .env
DATABASE__HOST=localhost
DATABASE__PORT=5432
```

```csharp
// Reachable as a nested section in IConfiguration:
var dbSection = configuration.GetSection("Database");
string host   = dbSection["Host"];          // "localhost"
int    port   = dbSection.GetValue<int>("Port"); // 5432
```

## Hot reload, parse options, and other knobs

Configure the source via the `configureSource` callback:

```csharp
builder.Configuration.AddDotEnvFiles(
    [".env"],
    s =>
    {
        s.ReloadOnChange = false;
        s.ParseOptions = new EnvParseOptions { AllowDuplicateKeys = true };
    });
```

### Opting out of smart insertion

The smart-insert is the convention-respecting default and the right choice
in almost every case. If you genuinely want `.env` values to override env
vars, add the source directly:

```csharp
builder.Configuration.Sources.Add(
    new DotEnvFilesSource { Paths = { ".env" } });
```

## `AsEnvReader` - typed access over any `IConfiguration`

`IConfiguration.GetValue<T>()` uses `Convert.ChangeType`, which is fine for
trivial conversions but doesn't give you Enver's parsing layer:

- `0x` / `0b` integer prefix support
- Strict enum (named members only, rejects arbitrary numeric casts)
- `GetRequired*` throws `EnvVariableException` with the failing variable name
- `Get*(key, default)` overloads that don't go through `Nullable<T>` boxing

`AsEnvReader()` is a thin bridge that gives you all of that against any
`IConfiguration` regardless of which source actually provided the value:

```csharp
var env = configuration.AsEnvReader();

int     port      = env.GetInt32("Database:Port", 5432);        // 0x/0b prefix supported
string  apiKey    = env.GetRequiredString("Auth:ApiKey");       // throws if missing
LogLevel level    = env.GetEnum<LogLevel>("Logging:Level");     // strict, declared-only
Uri     apiUrl    = env.GetUri("Api:Url");                      // UriKind.Absolute
TimeSpan timeout  = env.GetRequired<TimeSpan>("Request:Timeout");
```

Keys use `IConfiguration`'s `:` section delimiter.

## What this does **not** do

- **No `IOptions<T>` source-gen binder.** `services.Configure<MyOptions>(config.GetSection("My"))`
  still uses `Microsoft.Extensions.Configuration.Binder`, which uses
  `Convert.ChangeType` and won't pick up Enver's number-prefix/strict-enum
  behavior. Prefer `AsEnvReader().GetX(...)` for values where Enver's value parsing matters.
- **No cross-source `${VAR}` resolution.** Interpolation happens at .env
  file parse time, against earlier entries in the same file and the
  process environment. It does **not** resolve against the in-flight
  `IConfiguration` tree being built (e.g., a key set by an earlier
  `AddJsonFile`).

## License

MIT.
