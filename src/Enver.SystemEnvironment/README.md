# Enver.SystemEnvironment

Load `.env` files into the process environment block via
`Environment.SetEnvironmentVariable`. Useful when downstream code already reads
from `Environment.GetEnvironmentVariable(...)` and you want `.env` files to
participate in that lookup without changing the consuming code.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

```sh
dotnet add package Gnar.Enver.SystemEnvironment
```

```csharp
using Enver;

// Reads .env from the executable's directory and writes each entry to the
// process environment.
// C# 14+ only
Environment.LoadDotEnv(DotEnvPaths.AppDirectory());
// OR
EnvironmentExtensions.LoadDotEnv(DotEnvPaths.AppDirectory());

var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
```

## Typed accessors

`Environment.Variables` is a zero-cost `IEnvReader` over the process env block,
so the full typed accessor surface
(`Get*` / `Get*(key, default)` / `GetOptional*` / `TryGet*`) applies directly:

```csharp
using System.Net;
using Enver;

string dbHost    = Environment.Variables.GetString("DB_HOST");
int    port      = Environment.Variables.GetInt32("DB_PORT", 5432);    // default if missing; 0x/0b prefixes supported
bool   debug     = Environment.Variables.GetBoolean("DEBUG", false);   // default if missing; strict true/false
Uri    apiUrl    = Environment.Variables.GetUri("API_URL");
IPAddress bindIp = Environment.Variables.Get<IPAddress>("BIND_ADDRESS");
```

The four patterns:

- `Get*(key)`: throws `EnvException` on missing or unparseable.
- `Get*(key, default)`: returns `default` on missing; throws on unparseable.
- `GetOptional*(key)`: returns `null` on missing; throws on unparseable.
- `TryGet*(key, out value)`: returns `false` on missing **or** unparseable.

See the [main project README][main-readme] for the full list of supported
types.

[main-readme]: https://github.com/gnarsoftware/enver#supported-types

## Loading

`Environment.LoadDotEnv` has two shapes:

```csharp
// Single file. Missing files are silent.
Environment.LoadDotEnv("/etc/myapp/.env");

// Path list. Files load in order; later files override earlier ones, with
// shared ${VAR} interpolation across the whole sequence. Pair with
// DotEnvPaths to compose the canonical ladder.
Environment.LoadDotEnv([file1, file2]);
Environment.LoadDotEnv(DotEnvPaths.AppDirectory());                          // .env in app dir
Environment.LoadDotEnv(DotEnvPaths.WorkingDirectory().Standard("production")); // 4-tier ladder
Environment.LoadDotEnv(DotEnvPaths.AppDirectory().WithParentDirectories(int.MaxValue)); // walk to root

// Async variants:
await Environment.LoadDotEnvAsync("/etc/myapp/.env");
await Environment.LoadDotEnvAsync(DotEnvPaths.AppDirectory().Standard("production"));
```

Missing files are silently skipped. See the
`DotEnvPaths` reference in
[Enver.InMemory's README](https://www.nuget.org/packages/Gnar.Enver.InMemory/)
for the full builder surface.

## Cross-file precedence

Across files in a single load (e.g. `.env` then `.env.production`), values are
intentionally overridden. Files later in the path list override earlier ones.

For example:

```csharp
Environment.LoadDotEnv(
    DotEnvPaths.AppDirectory().WithVariant("production").WithParentDirectories(1));
```

with these files:

```
/path/to/your/app/.env.production -> KEY=app root + production
/path/to/your/app/.env            -> KEY=app root
/path/to/your/.env.production     -> KEY=parent dir + production
/path/to/your/.env                -> KEY=parent dir
```

will produce `KEY=app root + production`.

## Existing env vars

By default, `.env` entries are **skipped** if the variable is already set in
the process environment.

To make the `.env` file authoritative, pass `overrideExisting: true`:

```csharp
Environment.LoadDotEnv(DotEnvPaths.AppDirectory(), overrideExisting: true);
```

## License

MIT.
