# Enver.InMemory

Load `.env` files into an in-memory dictionary with platform-appropriate key comparison (case-insensitive on Windows, case-sensitive on Unix).

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

```sh
dotnet add package Gnar.Enver.InMemory
```

```csharp
using Enver;

// Reads .env from the executable's directory.
var values = EnvCollection.From(DotEnvPaths.AppDirectory());

string dbHost = values.GetString("DB_HOST");
int    port   = values.GetInt32("DB_PORT");

// EnvCollection is a Dictionary. Use it however you'd use one.
foreach (var (key, value) in values)
{
    Console.WriteLine($"{key} = {value}");
}
```

## Typed accessors

`EnvCollection` implements `IEnvReader`, so all typed accessors
(`Get*` / `Get*(key, default)` / `GetOptional*` / `TryGet*`) apply directly:

```csharp
using System.Net;
using Enver;

var values = EnvCollection.From(DotEnvPaths.AppDirectory());

string dbHost    = values.GetString("DB_HOST");
int    port      = values.GetInt32("DB_PORT", 5432);    // default if missing; 0x/0b prefixes supported
bool   debug     = values.GetBoolean("DEBUG", false);   // default if missing; strict true/false
Uri    apiUrl    = values.GetUri("API_URL");
IPAddress bindIp = values.Get<IPAddress>("BIND_ADDRESS");
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

`EnvCollection.From` has two shapes:

```csharp
// Single file. Missing files are silent.
EnvCollection.From("/etc/myapp/.env");

// Path list. Files load in order; later files override earlier ones, with
// shared ${VAR} interpolation across the whole sequence. Pair with
// DotEnvPaths to compose the canonical ladder.
EnvCollection.From([file1, file2]);
EnvCollection.From(DotEnvPaths.AppDirectory());                          // .env in app dir
EnvCollection.From(DotEnvPaths.WorkingDirectory().Standard("dev"));      // 4-tier ladder
EnvCollection.From(DotEnvPaths.AppDirectory().WithParentDirectories(int.MaxValue)); // walk to root

// Async variants:
await EnvCollection.FromAsync("/etc/myapp/.env");
await EnvCollection.FromAsync(DotEnvPaths.AppDirectory().Standard("dev"));
```

Missing files are silently skipped. Callers needing
strict "this file must exist" semantics should check with `File.Exists`
before calling.

## Composing paths with `DotEnvPaths`

`DotEnvPaths` is a composable builder for the canonical .env load ladder.
The canonical precedence is fixed:

`.env < .env.{variant} < .env.local < .env.{variant}.local`

```csharp
// Roots
DotEnvPaths.AppDirectory()         // AppContext.BaseDirectory
DotEnvPaths.WorkingDirectory()     // Directory.GetCurrentDirectory() at load time
DotEnvPaths.Directory("/var/app")  // explicit
DotEnvPaths.Relative()             // bare filenames; resolution deferred to consumer

// Modifiers
.WithFileName("config.env")        // base filename (defaults to ".env")
.WithVariant("dev")                // adds .env.{variant} tier
.WithLocal()                       // adds .env.local (+ .env.{variant}.local if variant set)
.WithParentDirectories(2)          // walks ancestors (base + variant only; local stays at start; not supported on Relative)
.Standard("dev")                   // equivalent to .WithVariant("dev").WithLocal()
```

A builder implements `IEnumerable<string>`, so it composes directly into
collection-expression spreads:

```csharp
EnvCollection.From([
    .. DotEnvPaths.AppDirectory().Standard("dev"),
    "/etc/myapp/overrides.env",
]);
```

## Duplicate-key handling

Within a single file, defining the same key twice throws by default:

```csharp
// Throws EnvDuplicateKeyException: "Duplicate key 'DB_HOST' encountered in input."
var coll = new EnvCollection();
new EnvDictionaryParser(coll).Parse("DB_HOST=a\nDB_HOST=b");
```

To allow duplicates within a file:

```csharp
EnvCollection.From(
    DotEnvPaths.AppDirectory(),
    parseOptions: new EnvParseOptions { AllowDuplicateKeys = true });
```

---

Across files in a single load (e.g. `.env` then `.env.local`), values are
intentionally overridden. Files later in the path list override earlier
ones. Combined with `DotEnvPaths.WithParentDirectories`, files in closer
directories override files in farther ancestors.

For example:

```csharp
EnvCollection.From(DotEnvPaths.AppDirectory().Standard().WithParentDirectories(1));
```

with these files:

```
/path/to/your/app/.env.local -> KEY=app root + local
/path/to/your/app/.env       -> KEY=app root
/path/to/your/.env           -> KEY=parent dir
```

will produce `KEY=app root + local`.

## License

MIT.
