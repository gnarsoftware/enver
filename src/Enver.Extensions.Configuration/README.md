# Enver.Extensions.Configuration

`Microsoft.Extensions.Configuration` integration for [Enver](https://github.com/gnarsoftware/enver).
Two things in one small package:

1. **`AddDotEnvFile(...)`** on `IConfigurationBuilder` - load a `.env` file as
   a configuration source alongside JSON, env vars, and anything else you've
   wired up.
2. **`AsEnvReader()`** on `IConfiguration` - apply Enver's typed-accessor
   surface (`GetInt32`, `GetRequired<T>`, `GetEnum<T>`, …) to
   *any* configuration source, not just values loaded from a `.env` file.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

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

The variant filename is **always lowercased**, so
`ASPNETCORE_ENVIRONMENT=Development` resolves to `.env.development` (not
`.env.Development`). This matches the universal dotenv ecosystem
convention (`.env.development`, `.env.production`, `.env.test`) even
though `appsettings.{Environment}.json` preserves case. Override when
needed:

```csharp
builder.Configuration.AddDotEnvFiles(environmentName: "Local");
// loads .env.local (lowercased)
```

### Precedence: where in the source list

`AddDotEnvFiles` inserts the entire `.env` ladder as a single block,
immediately before the first environment-variables source in the builder.
The resulting precedence from low to high:

```
host config -> appsettings.json -> appsettings.{Environment}.json -> user secrets
            -> .env -> .env.{environment} -> .env.local -> .env.{environment}.local
            -> environment variables -> command-line args
```

Inside the `.env` tier, ordering matches the four-tier convention used by
Create React App, Next.js, Vite, and dotenv-flow:

1. `.env` - shared defaults across all environments
2. `.env.{environment}` - shared defaults for this environment
3. `.env.local` - per-machine override of (1) and (2)
4. `.env.{environment}.local` - per-machine per-environment override; highest in the tier

Outside the `.env` tier:
- `.env` files override JSON config files
- Environment variables override every `.env` layer
- Command-line args override everything

If no env-vars source is registered yet, the sources are appended.

#### `.env` vs user secrets

Under this scheme `.env` files sit **after** user secrets in the list,
meaning **`.env` beats user secrets** for any overlapping key. The two
mechanisms overlap heavily in purpose (both are local-machine config
storage outside of source control), so most projects should use one or
the other for any given key.

If you specifically want user secrets to beat `.env`, re-anchor user
secrets at the end of the list by calling `AddUserSecrets<T>()` *after*
`AddDotEnvFiles`:

```csharp
builder.Configuration.AddDotEnvFiles();
builder.Configuration.AddUserSecrets<Program>();   // moves user secrets last
```

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

This adapter was built to be a drop-in replacement for `appsettings.json`,
which favors using `.env` as the checked-in source of shared config over
the more common pattern of gitignoring `.env`. However, since all files are
optional by default, you can choose to instead gitignore `.env` and omit
the usage of `.env.local` / `.env.*.local` entirely. Use this MSBuild
include and `.gitignore` pair instead:

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

In this mode, you'd duplicate the shared defaults into each `.env.{environment}`
file rather than sharing them via a checked-in `.env`. The trade-off is
some duplication for a stronger guarantee that `.env` can't accidentally
leak.

### Where do secrets go?

**Never in any `.env` file**, checked in or not. The deployed
`.env.{environment}` files should carry environment-shape defaults (values
that are the same for everyone hitting that environment and that aren't sensitive
if leaked). Anything actually secret comes from the deployment platform's
env-var injection (Azure App Service config, AWS Parameter Store / Secrets
Manager, K8s `Secret` mounted as env, Docker `--env`, etc.). Those take precedence
and override the `.env.{environment}` defaults.

For **dev-machine secrets** (your local DB password, personal API tokens),
the right home depends on which convention you've adopted:

- **Framework convention (the recommended path)**: put secrets in
  **`.env.local`** or `.env.{environment}.local` for environment-specific
  secrets. Both are gitignored and never deploy, and they sit at the top
  of the `.env` tier so they override every checked-in layer.

  **Polyglot caveat**: `.env.local` is a framework convention, not a
  base-dotenv-library feature. If your repo shares config with non-.NET
  tooling, either configure each tool to load `.env.local` explicitly, or
  adopt the inverted convention below.

- **Inverted convention** (`.env` gitignored - see
  [Prefer `.env` gitignored?](#prefer-env-gitignored) above): put secrets
  in `.env`. Universal across dotenv tooling in the same repo. Trade-off is
  the one noted in that section: you duplicate shared defaults into each
  `.env.{environment}` rather than sharing them via a checked-in `.env`.

- **User secrets** (`dotnet user-secrets set ...`): .NET-specific, stored
  in `~/.microsoft/usersecrets/<id>/` outside the repo entirely, so they
  can't be committed even with bad `.gitignore` discipline. The trade-off
  is they're invisible to non-.NET tooling, so this is the option for
  .NET-only projects.

`.env.local` and user secrets both reach the same `IConfiguration` tree
and overlap heavily in purpose; pick one per key. If both define the same
key, `.env.local` wins. See [`.env` vs user secrets](#env-vs-user-secrets) above.

## `AddDotEnvFile`

```csharp
builder.Configuration.AddDotEnvFile(
    path: ".env",
    optional: false,        // throw on missing file (default)
    reloadOnChange: true);  // hot-reload on change (default)
```

For more control, use the `Action<EnverConfigurationSource>` overload:

```csharp
builder.Configuration.AddDotEnvFile(source =>
{
    source.Path = ".env.local";
    source.Optional = true;
    source.ReloadOnChange = true;
    source.ParseOptions = new EnvParseOptions
    {
        OnDuplicate = DuplicateKeyBehavior.Allow,
        OnMissingInterpolation = MissingInterpolationBehavior.EmptyString,
    };
});
```

### Key transform: `__` -> `:`

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

### Source composition

`AddDotEnvFile` participates in the standard `IConfigurationBuilder`
last-wins ordering. Stack it however the host needs:

```csharp
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
    .AddDotEnvFile(".env", optional: true)
    .AddDotEnvFile(".env.local", optional: true)   // local override
    .AddEnvironmentVariables()
    .AddCommandLine(args);
```

#### Precedence note

Unlike `AddDotEnvFiles`, the single-file `AddDotEnvFile` does **not**
auto-slot into the config-file tier. It appends to the source list, so
the `.env` source takes the **highest** precedence under standard
last-wins IConfiguration ordering.

If you want the same "env vars override `.env`" behavior as
`AddDotEnvFiles`, either use the helper or insert manually at the
config-file tier:

```csharp
builder.Configuration.Sources.Insert(0, new EnverConfigurationSource { Path = ".env", Optional = true });
```

### Hot reload

Pass `reloadOnChange: true` and the configuration tree refreshes when the
file changes on disk. Standard `IConfiguration` change-token semantics
apply.

## `AsEnvReader` - typed access over any `IConfiguration`

`IConfiguration.GetValue<T>()` uses `Convert.ChangeType`, which is fine for
trivial conversions but doesn't give you Enver's parsing layer:

- `0x` / `0b` integer prefix support
- Strict enum (named members only, rejects arbitrary numeric casts)
- `GetRequired*` throws `EnverException` with the failing variable name
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
- **No directory walker** (`.env` + `.env.{variant}` + parent-dir walk).
  Compose multiple `AddDotEnvFile` calls in your preferred order instead.

## License

MIT.
