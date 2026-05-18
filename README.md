# Enver

A fast, strict, opinionated `.env` file parser for .NET.

---

Enver is built to:

- Parse common `.env` syntax
- Reject malformed or ambiguous input
- Fail fast with clear error messages

See [`PARSING.md`](PARSING.md) for the full grammar and enforcement philosophy.

It supports:

- Value interpolation (`NAME=World` + `GREETING="Hello ${NAME}!"` = `GREETING="Hello World!"`)
- Line-level and inline comments (`KEY=value # This comment won't be parsed`)
- Multi-level `.env` file loading
- Variant files (`.env.{variant}`) with correct precedence

Parsed output can be sent to:

- An in-memory dictionary
- `System.Environment`
- ASP.NET Core configuration
- A strongly-typed class via source-generated binding

## What's in the box

The base package contains the core parser. Pick an integration package based on where you want the values to go:

| Package | Use case |
|---|---|
| **`Enver`** | Core lexer + parser. Reference directly only if you're writing a custom consumer. |
| **`Enver.SourceGeneration`** | Strongly-typed configuration binding. |
| **`Enver.InMemory`** | Load `.env` files into an `EnvCollection` (a `Dictionary<string, string>` subclass with platform-appropriate key comparison). |
| **`Enver.SystemEnvironment`** | Load `.env` files into the process environment block via `Environment.SetEnvironmentVariable`. Useful when downstream code already uses `Environment.GetEnvironmentVariable(...)` and you want to keep that flow. |
| **`Enver.Extensions.Configuration`** | `IConfigurationSource` integration for ASP.NET Core / `Microsoft.Extensions.Configuration`. Plus `IConfiguration.AsEnvReader()` so Enver's typed accessors apply to any configuration source, not just `.env` files. |

## Quick start

### Load `.env` into a dictionary

```sh
dotnet add package Gnar.Enver.InMemory
```

```csharp
using Enver;

// Reads .env from the executable's directory.
var values = EnvCollection.FromAppDirectory();

var dbHost = values["DB_HOST"];
```

`EnvCollection` is a `Dictionary` subclass, so all standard dictionary operations apply.

### Load `.env` into the process environment

```sh
dotnet add package Gnar.Enver.SystemEnvironment
```

```csharp
using Enver;

// C# 14+ only
Environment.LoadDotEnvFromAppDirectory();
// OR
EnvironmentExtensions.LoadDotEnvFromAppDirectory();

var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
```

### Chain-load multiple files

The directory-based loaders accept a `variant` parameter for
`.env.{variant}` overrides and a `maxAncestors` parameter to walk up
parent directories.

```csharp
// Looks for .env then .env.local in every parent directory up to the root.
// Closer files override farther files; variants override bare .env
var values = EnvCollection.FromAppDirectory(
    variant: "local",
    maxAncestors: int.MaxValue
);
```

## Typed accessors

The `IEnvReader` interface provides typed accessors for a collection of environment
variables. These come in four patterns:

- `Get*(key)`: throws `EnverException` on missing or unparseable.
- `Get*(key, default)`: returns `default` on missing; throws on unparseable.
- `GetOptional*(key)`: returns `null` on missing; throws on unparseable.
- `TryGet*(key, out value)`: returns `false` on missing **or** unparseable.

`EnvCollection` (Enver.InMemory), `Environment.Variables` (Enver.SystemEnvironment),
and `configuration.AsEnvReader()` (Enver.Extensions.Configuration) all expose this interface.

### Enver.InMemory

```csharp
using System.Net;
using Enver;

var values = EnvCollection.FromAppDirectory();

string dbHost     = values.GetString("DB_HOST");
int    port       = values.GetInt32("DB_PORT", 5432);    // default if missing; 0x/0b prefixes supported
bool   debug      = values.GetBoolean("DEBUG", false);   // default if missing; strict true/false
Uri    apiUrl     = values.GetUri("API_URL");
IPAddress bindIp  = values.Get<IPAddress>("BIND_ADDRESS");
```

### Enver.SystemEnvironment

```csharp
using System.Net;
using Enver;

string dbHost     = Environment.Variables.GetString("DB_HOST");
int    port       = Environment.Variables.GetInt32("DB_PORT", 5432);    // default if missing; 0x/0b prefixes supported
bool   debug      = Environment.Variables.GetBoolean("DEBUG", false);   // default if missing; strict true/false
Uri    apiUrl     = Environment.Variables.GetUri("API_URL");
IPAddress bindIp  = Environment.Variables.Get<IPAddress>("BIND_ADDRESS");
```

### Enver.Extensions.Configuration

```csharp
using System.Net;
using Enver;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration.AsEnvReader();

string dbHost     = configuration.GetString("DB_HOST");
int    port       = configuration.GetInt32("DB_PORT", 5432);    // default if missing; 0x/0b prefixes supported
bool   debug      = configuration.GetBoolean("DEBUG", false);   // default if missing; strict true/false
Uri    apiUrl     = configuration.GetUri("API_URL");
IPAddress bindIp  = configuration.Get<IPAddress>("BIND_ADDRESS");
```

### Supported types

| Type | Methods | Notes |
|---|---|---|
| `string` | `GetString` / `GetOptionalString` / `TryGetString` | |
| `bool` | `GetBoolean` / `GetOptionalBoolean` / `TryGetBoolean` | `true` / `false` (case-insensitive) |
| `int` | `GetInt32` / `GetOptionalInt32` / `TryGetInt32` | Honors `0x` / `0b` prefixes |
| `long` | `GetInt64` / `GetOptionalInt64` / `TryGetInt64` | Honors `0x` / `0b` prefixes |
| `double` | `GetDouble` / `GetOptionalDouble` / `TryGetDouble` | |
| `Guid` | `GetGuid` / `GetOptionalGuid` / `TryGetGuid` | |
| `Uri` | `GetUri` / `GetOptionalUri` / `TryGetUri` | `UriKind` parameter (defaults to `Absolute`) |
| `Version` | `GetVersion` / `GetOptionalVersion` / `TryGetVersion` | |
| any `enum` | `GetEnum<T>` / `GetOptionalEnum<T>` / `TryGetEnum<T>` | Rejects raw numeric values that don't correspond to a declared member |
| any `IParsable<T>` | `Get<T>` / `GetOptional<T>` / `GetOptionalRef<T>` / `TryGet<T>` / `TryGetRef<T>` | Covers `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`, `IPAddress`, `IPNetwork`, `char`, etc. |
| any `INumberBase<T>` | `GetNumber<T>` / `GetOptionalNumber<T>` / `TryGetNumber<T>` | Covers `byte`, `sbyte`, `short`, `ushort`, `uint`, `ulong`, `decimal`, `float`, `Half`, etc. Honors `0x` / `0b` prefixes. |

Every row also has a `Get*(key, defaultValue)` overload that returns the
supplied default if the key is missing (parse failures still throw).

For custom env sources, implement `IEnvReader`'s single `TryGetValue` method;
the full typed surface then applies via extension methods.

## Supported runtimes

`net8.0`, `net9.0`, `net10.0`.

The `Environment.LoadDotEnv*` extension-member call syntax requires C# 14.
Earlier C# versions can call the same methods via `EnvironmentExtensions.LoadDotEnv*` directly.

## License

[MIT](LICENSE).
