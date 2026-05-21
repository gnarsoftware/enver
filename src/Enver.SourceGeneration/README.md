# Enver.SourceGeneration

Attributes and a Roslyn source generator that bind `.env` values to
strongly-typed config classes.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

```sh
dotnet add package Gnar.Enver.SourceGeneration
```

Mark a `partial` type with `[EnverBindable]` and the generator emits a
`Bind` family of static methods on it:

```csharp
using Enver;
using Enver.SourceGeneration;

[EnverBindable]
public partial record DatabaseConfig(string Host, int Port)
{
    public bool UseSsl { get; init; } = true;
}

// Pick one:

// 1. Bind by loading a .env from the app directory.
var cfg = DatabaseConfig.BindFromAppDirectory();

// 2. Bind by loading a .env from the working directory.
var cfg = DatabaseConfig.BindFromWorkingDirectory();

// 3. Bind by loading a specific file.
var cfg = DatabaseConfig.BindFromFile("/etc/myapp/.env");

// 4. Bind from an existing IEnvReader (EnvCollection, Environment.Variables,
//    configuration.AsEnvReader(), …)
var cfg = DatabaseConfig.Bind(values);
```

By default, property names map to `UPPER_SNAKE_CASE` keys
(`Host` -> `HOST`, `UseSsl` -> `USE_SSL`).

## Generated surface

For a self-bindable type (`[EnverBindable]` on the type itself), the generator
emits three static factories plus a streaming `Binder`:

```csharp
public partial record DatabaseConfig
{
    public static DatabaseConfig Bind(IEnvReader reader);
    public static DatabaseConfig BindFromAppDirectory(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default);
    public static DatabaseConfig BindFromWorkingDirectory(
        string fileName = ".env",
        string? variant = null,
        int maxAncestors = 0,
        bool throwIfMissing = false,
        EnvParseOptions parseOptions = default);
    public static DatabaseConfig BindFromFile(
        string path,
        bool throwIfMissing = true,
        EnvParseOptions parseOptions = default);

    public sealed partial class Binder : EnvParser
    {
        public DatabaseConfig Build();
    }
}
```

The same surface is generated on an external host (`[EnverBindable<T>]`),
with method names suffixed by the target's simple type name
(`Configs.BindCacheConfig(...)` / `Configs.CacheConfigBinder`). See
[External host](#external-host-enverbindablet) below.

## Naming and prefix: `[EnverConfig]`

`[EnverConfig]` configures how member names map to keys. It does **not**
trigger generation on its own. Pair it with `[EnverBindable]` on the same
type:

```csharp
[EnverBindable]
[EnverConfig("DB", KeyNaming = EnverKeyNamingConvention.UpperSnakeCase)]
public partial record DatabaseConfig(string Host, int Port);
// Reads DB_HOST and DB_PORT.
```

Naming conventions:

- `UpperSnakeCase` (default): `HostName` -> `HOST_NAME`
- `SnakeCase`: `HostName` -> `host_name`
- `PreserveOriginal`: `HostName` -> `HostName`
- `Inherit`: use the nearest enclosing `[EnverConfig]` (falls back to
  `UpperSnakeCase`)

> [!NOTE]
> Prefixes and names set with `[EnverKey]` are not passed through
> the `KeyNaming` convention.

## Per-member overrides: `[EnverKey]`

```csharp
[EnverBindable]
[EnverConfig("APP")]
public partial class AppConfig
{
    // Map to APP_CUSTOM_NAME
    [EnverKey("CUSTOM_NAME")]
    public string Name { get; init; } = "";

    // Map to GLOBAL_SETTING
    [EnverKey(IgnorePrefix = true)]
    public string GlobalSetting { get; init; } = "";

    // Force optional
    [EnverKey(Required = EnverRequirementBehavior.Optional)]
    public int Port { get; init; }

    // Force required
    [EnverKey(Required = EnverRequirementBehavior.Required)]
    public string? Tag { get; init; }
}
```

> [!NOTE]
> **Records:** when annotating a primary-constructor parameter, use the
> `[property: EnverKey(...)]` target so the attribute lands on the generated
> property rather than the parameter.

## Subsections

A property whose type is itself a bindable config type is bound as a
**subsection**: its members are read from the same flat key space, under a
prefix derived from the outer property. A property is treated as a subsection
when any of these hold:

- the property is annotated with `[EnverKey]`
- the property's type carries `[EnverConfig]`
- the property's type has a member annotated with `[EnverKey]`

A subsection key is composed, in order, of:

1. the outer type's prefix (its `[EnverConfig]` prefix plus anything inherited
   from further-out subsections)
2. the subsection property's segment (its `[EnverKey]` name, or the property
   name run through the naming convention)
3. the subsection type's own `[EnverConfig]` prefix
4. the member's key

```csharp
record Sub(string Val);

[EnverBindable]
partial class Base
{
    [EnverKey]
    public required Sub Sub { get; init; }
}
// Sub.Val -> SUB_VAL
```

| Attributes | Key for `Sub.Val` |
|---|---|
| (as above) | `SUB_VAL` |
| `Sub` has `[EnverConfig("K1")]` | `SUB_K1_VAL` |
| `Base` has `[EnverConfig("K2")]` | `K2_SUB_VAL` |
| both of the above | `K2_SUB_K1_VAL` |

Opt out of the property-name segment with an empty key:

```csharp
[EnverBindable]
partial class Base
{
    [EnverKey("")]
    public required Sub Sub { get; init; }
}
// Sub.Val -> VAL
```

`[EnverKey(IgnorePrefix = true)]` on a subsection property drops the inherited
(ancestor) prefix but keeps the property's own segment and the subsection
type's `[EnverConfig]` prefix. Requiredness is controlled the same way as a
leaf member, with `[EnverKey(Required = ...)]`.

## Other attributes

| Attribute | Effect |
|---|---|
| `[EnverIgnore]` | Skip a member that would otherwise be bound. |
| `[EnverUri(UriKind)]` | Specify `UriKind` for `Uri` members (default `Absolute`). |
| `[EnverFormatProvider(type, memberName)]` | Point at a static `IFormatProvider` member used when parsing numbers, dates, etc. Applies type-wide when placed on the class/struct; per-member when placed on a field/property. |

## Required vs. optional

Each member is classified as **required**, **optional**, or
**with-default**. The generator infers from C# signals, in order:

1. `required` modifier -> required
2. Nullable value or reference type -> optional
3. Property initializer -> optional with default
4. Non-nullable type -> required
5. Reference type under `#nullable disable` -> optional

Required members that are missing throw `EnverException` at `Bind()` time
with the failing key. Optional members fall back to `default(T)` or the
declared initializer.

Override the inferred classification with
`[EnverKey(Required = EnverRequirementBehavior.Required | .Optional)]`.

## External host: `[EnverBindable<T>]`

Generate binders on a separate `partial` class:

```csharp
public sealed record CacheConfig(int Ttl, string Region);

[EnverBindable<CacheConfig>]
public partial class Configs;
```

The static factories on the host are suffixed with the target's
name, and the streaming binder lives alongside them:

```csharp
var cfg = Configs.BindCacheConfig(reader);
var cfg = Configs.BindCacheConfigFromAppDirectory();
var binder = new Configs.CacheConfigBinder();
```

`[EnverBindable<T>]` is repeatable. One host can have binders for several targets.

## Custom parsing

The generated `Binder` derives from `EnvParser`, so you can control parsing directly.

Binding directly to UTF-8 content:

```csharp
var binder = new DatabaseConfig.Binder();
binder.Parse(
    """
    HOST=db.internal
    URL="postgres://${HOST}:5432"
    """u8
);
var cfg = binder.Build();
```

## Supported types

- `string`, `bool`, `Guid`, `Uri`
- `int`, `long`, and any numeric type implementing
  `INumber<T>` / `IParsable<T>` (including `0x` / `0b` prefix support)
- Enums
- Any `IUtf8SpanParsable<T>`, `ISpanParsable<T>`, or `IParsable<T>`
  (such as `IPAddress`, `IPNetwork`, `Version`)

See the [main project README][main-readme] for the full list.

[main-readme]: https://github.com/gnarsoftware/enver#supported-types

## License

MIT.
