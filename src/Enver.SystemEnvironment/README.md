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
Environment.LoadDotEnvFromAppDirectory();
// OR
EnvironmentExtensions.LoadDotEnvFromAppDirectory();

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

## Loader variants

```csharp
// Load a specific file. Throws if missing.
Environment.LoadDotEnv("/etc/myapp/.env");

// Load .env files from the current working directory rather than the app directory.
Environment.LoadDotEnvFromWorkingDirectory();

// Load .env file from a specific directory.
Environment.LoadDotEnvFromDirectory("/var/myapp");

// Walk up all parent directories; closer files override farther ones.
Environment.LoadDotEnvFromAppDirectory(maxAncestors: int.MaxValue);

// .env then .env.production in the app directory.
Environment.LoadDotEnvFromAppDirectory(variant: "production");

// Async variants for all of the above.
await Environment.LoadDotEnvFromAppDirectoryAsync();
```

## Cross-file precedence

Across files in a single load (e.g. `.env` then `.env.production`), values are
intentionally overridden. Files closer to the source directory override farther
files, and variants override the base file.

For example, parsing with:
```csharp
Environment.LoadDotEnvFromAppDirectory(variant: "production", maxAncestors: 1);
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
Environment.LoadDotEnvFromAppDirectory(overrideExisting: true);
```

## License

MIT.
