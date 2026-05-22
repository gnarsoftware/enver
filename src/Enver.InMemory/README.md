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
var values = EnvCollection.FromAppDirectory();

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

var values = EnvCollection.FromAppDirectory();

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

## Loader variants

```csharp
// Load a specific file. Throws if missing.
EnvCollection.FromFile("/etc/myapp/.env");

// Load .env files from the current working directory rather than the app directory.
EnvCollection.FromWorkingDirectory();

// Load .env file from a specific directory.
EnvCollection.FromDirectory("/var/myapp");

// Walk up all parent directories; closer files override farther ones.
EnvCollection.FromAppDirectory(maxAncestors: int.MaxValue);

// .env then .env.local in the app directory.
EnvCollection.FromAppDirectory(variant: "local");

// Async variants for all of the above.
await EnvCollection.FromAppDirectoryAsync();
```

## Duplicate-key handling

Within a single file, defining the same key twice throws by default:

```csharp
// Throws EnvException: "Duplicate key 'DB_HOST' encountered in input."
var coll = new EnvCollection();
new EnvDictionaryParser(coll).Parse("DB_HOST=a\nDB_HOST=b");
```

To allow duplicates within a file:

```csharp
EnvCollection.FromAppDirectory(
    parseOptions: new EnvParseOptions { AllowDuplicateKeys = true });
```

---

Across files in a single load (e.g. `.env` then `.env.local`), values are intentionally overridden. Files closer to the source directory override farther files, and variants override the base file.

For example, parsing with:
```csharp
EnvCollection.FromAppDirectory(variant: "local", maxAncestors: 1);
```

with these files:

```
/path/to/your/app/.env.local -> KEY=app root + local
/path/to/your/app/.env       -> KEY=app root
/path/to/your/.env.local     -> KEY=parent dir + local
/path/to/your/.env           -> KEY=parent dir
```

will produce `KEY=app root + local`.

## License

MIT.
