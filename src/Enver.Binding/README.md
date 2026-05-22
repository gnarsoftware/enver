# Enver.Binding

Attributes and a Roslyn source generator that bind `.env` values to
strongly-typed config classes.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. See the
[main project README](https://github.com/gnarsoftware/enver#readme) for the
broader ecosystem.

## Quick start

```sh
dotnet add package Gnar.Enver.Binding
```

Mark a `partial` type with `[EnvBindable]` and the generator emits a
`Bind` family of static methods on it:

```csharp
using Enver;
using Enver.Binding;

[EnvBindable]
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

For a self-bindable type (`[EnvBindable]` on the type itself), the generator
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

The same surface is generated on an external host (`[EnvBindable<T>]`),
with method names suffixed by the target's simple type name
(`Configs.BindCacheConfig(...)` / `Configs.CacheConfigBinder`). See
[External host](#external-host-enverbindablet) below.

## Naming and prefix: `[EnvConfig]`

`[EnvConfig]` configures how member names map to keys. It does **not**
trigger generation on its own. Pair it with `[EnvBindable]` on the same
type:

```csharp
[EnvBindable]
[EnvConfig("DB", KeyNaming = EnvKeyNamingConvention.UpperSnakeCase)]
public partial record DatabaseConfig(string Host, int Port);
// Reads DB_HOST and DB_PORT.
```

Naming conventions:

- `UpperSnakeCase` (default): `HostName` -> `HOST_NAME`
- `SnakeCase`: `HostName` -> `host_name`
- `PreserveOriginal`: `HostName` -> `HostName`
- `Inherit`: use the nearest enclosing `[EnvConfig]` (falls back to
  `UpperSnakeCase`)

> [!NOTE]
> Prefixes and names set with `[EnvKey]` are not passed through
> the `KeyNaming` convention.

## Per-member overrides: `[EnvKey]`

```csharp
[EnvBindable]
[EnvConfig("APP")]
public partial class AppConfig
{
    // Map to APP_CUSTOM_NAME
    [EnvKey("CUSTOM_NAME")]
    public string Name { get; init; } = "";

    // Map to GLOBAL_SETTING
    [EnvKey(IgnorePrefix = true)]
    public string GlobalSetting { get; init; } = "";

    // Force optional
    [EnvKey(Requirement = EnvRequirement.Optional)]
    public int Port { get; init; }

    // Force required
    [EnvKey(Requirement = EnvRequirement.Required)]
    public string? Tag { get; init; }
}
```

> [!NOTE]
> **Records:** when annotating a primary-constructor parameter, use the
> `[property: EnvKey(...)]` target so the attribute lands on the generated
> property rather than the parameter.

## Subsections

A property whose type is itself a bindable config type is bound as a
**subsection**: its members are read from the same flat key space, under a
prefix derived from the outer property. A property is treated as a subsection
when any of these hold:

- the property is annotated with `[EnvKey]`
- the property's type carries `[EnvConfig]`
- the property's type has a member annotated with `[EnvKey]`

A subsection key is composed, in order, of:

1. the outer type's prefix (its `[EnvConfig]` prefix plus anything inherited
   from further-out subsections)
2. the subsection property's segment (its `[EnvKey]` name, or the property
   name run through the naming convention)
3. the subsection type's own `[EnvConfig]` prefix
4. the member's key

```csharp
record Sub(string Val);

[EnvBindable]
partial class Base
{
    [EnvKey]
    public required Sub Sub { get; init; }
}
// Sub.Val -> SUB_VAL
```

| Attributes | Key for `Sub.Val` |
|---|---|
| (as above) | `SUB_VAL` |
| `Sub` has `[EnvConfig("K1")]` | `SUB_K1_VAL` |
| `Base` has `[EnvConfig("K2")]` | `K2_SUB_VAL` |
| both of the above | `K2_SUB_K1_VAL` |

Opt out of the property-name segment with an empty key:

```csharp
[EnvBindable]
partial class Base
{
    [EnvKey("")]
    public required Sub Sub { get; init; }
}
// Sub.Val -> VAL
```

`[EnvKey(IgnorePrefix = true)]` on a subsection property drops the inherited
(ancestor) prefix but keeps the property's own segment and the subsection
type's `[EnvConfig]` prefix. Requiredness is controlled the same way as a
leaf member, with `[EnvKey(Requirement = ...)]`.

## Other attributes

| Attribute | Effect |
|---|---|
| `[EnvIgnore]` | Skip a member that would otherwise be bound. |
| `[EnvUri(UriKind)]` | Specify `UriKind` for `Uri` members (default `Absolute`). |
| `[EnvFormatProvider(type, memberName)]` | Point at a static `IFormatProvider` member used when parsing numbers, dates, etc. Applies type-wide when placed on the class/struct; per-member when placed on a field/property. |

## Required vs. optional

Each member is classified as **required**, **optional**, or
**with-default**. The generator infers from C# signals, in order:

1. `required` modifier -> required
2. Nullable value or reference type -> optional
3. Property initializer -> optional with default
4. Non-nullable type -> required
5. Reference type under `#nullable disable` -> optional

Required members that are missing throw `EnvException` at `Bind()` time
with the failing key. Optional members fall back to `default(T)` or the
declared initializer.

Override the inferred classification with
`[EnvKey(Requirement = EnvRequirement.Required | .Optional)]`.

## External host: `[EnvBindable<T>]`

Generate binders on a separate `partial` class:

```csharp
public sealed record CacheConfig(int Ttl, string Region);

[EnvBindable<CacheConfig>]
public partial class Configs;
```

The static factories on the host are suffixed with the target's
name, and the streaming binder lives alongside them:

```csharp
var cfg = Configs.BindCacheConfig(reader);
var cfg = Configs.BindCacheConfigFromAppDirectory();
var binder = new Configs.CacheConfigBinder();
```

`[EnvBindable<T>]` is repeatable. One host can have binders for several targets.

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
