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

// 1. Bind by loading a single .env file. Missing files are silent.
var cfg = DatabaseConfig.Bind("/path/to/my/.env");

// 2. Bind by loading a path list. Later files override earlier ones, with
//    shared ${VAR} interpolation across the sequence. Pair with DotEnvPaths
//    to build the canonical ladder.
var cfg = DatabaseConfig.Bind(DotEnvPaths.AppDirectory());                   // .env in app dir
var cfg = DatabaseConfig.Bind(DotEnvPaths.WorkingDirectory().Standard("dev")); // 4-tier ladder

// 3. Bind from an existing IEnvReader (EnvCollection, Environment.Variables,
//    configuration.AsEnvReader(), ...)
var cfg = DatabaseConfig.Bind(values);
```

By default, property names map to `UPPER_SNAKE_CASE` keys
(`Host` -> `HOST`, `UseSsl` -> `USE_SSL`).

## Generated surface

For a self-bindable type (`[EnvBindable]` on the type itself), the generator
emits three static factories, a `Validate` method and a streaming `Binder`:

```csharp
public partial record DatabaseConfig
{
    public static DatabaseConfig Bind(IEnvReader reader, bool validate = true);
    public static DatabaseConfig Bind(
        string path,
        EnvParseOptions parseOptions = default,
        bool validate = true);
    public static DatabaseConfig Bind(
        IEnumerable<string> paths,
        EnvParseOptions parseOptions = default,
        bool validate = true);

    public static void Validate(DatabaseConfig instance);

    public sealed partial class Binder : EnvParser
    {
        public DatabaseConfig Build(bool validate = true);
    }
}
```

The `IEnumerable<string>` overload is the primitive. Pair it with
`DotEnvPaths` to compose the canonical ladder. Missing files are silently
skipped.

The same surface is generated on an external host (`[EnvBindable<T>]`),
with method names suffixed by the target's simple type name
(`Configs.BindCacheConfig(...)` / `Configs.CacheConfigBinder`). See
[External host](#external-host-enverbindablet) below.

## Prefix and naming

`[EnvBindable]` (and `[EnvBindable<T>]`) takes an optional key prefix and the
naming convention used for member names:

```csharp
[EnvBindable("DB", KeyNaming = EnvKeyNamingConvention.UpperSnakeCase)]
public partial record DatabaseConfig(string Host, int Port);
// Reads DB_HOST and DB_PORT.
```

Naming conventions:

- `UpperSnakeCase` (default): `HostName` -> `HOST_NAME`
- `SnakeCase`: `HostName` -> `host_name`
- `PreserveOriginal`: `HostName` -> `HostName`
- `Inherit`: use the enclosing convention (on `[EnvBindable]` itself, `UpperSnakeCase`)

> [!NOTE]
> Prefixes and names set with `[EnvKey]` are used literally. They are not passed
> through the `KeyNaming` convention.

The prefix applies only when the type is bound as the root. A type used as a
[subsection](#subsections) is named by the property that holds it.

### Separator

Key segments (the prefix, each subsection or polymorphic case, and the member
name) are joined with a single underscore by default. Set
`Separator = EnvKeySeparator.DoubleUnderscore` to use the ASP.NET Core convention:

```csharp
[EnvBindable("APP", Separator = EnvKeySeparator.DoubleUnderscore)]
public partial record AppConfig(string LogLevel, Db Db);
public record Db(string Host, Pool Pool);
public record Pool(int MaxSize);
// Reads APP__LOG_LEVEL, APP__DB__HOST and APP__DB__POOL__MAX_SIZE.
```

Underscores inside a name stay single: those produced by the naming convention
(`LOG_LEVEL`) and those in an explicit `[EnvKey("DB_URL")]` name. Polymorphic type
keys follow the separator too (`STORAGE__TYPE`).

## Per-member overrides: `[EnvKey]`

```csharp
[EnvBindable("APP")]
public partial class AppConfig
{
    // Map to APP_CUSTOM_NAME
    [EnvKey("CUSTOM_NAME")]
    public string Name { get; init; } = "";

    // Map to GLOBAL_SETTING
    [EnvKey(IgnorePrefix = true)]
    public string GlobalSetting { get; init; } = "";

    // Map to APP_LogLevel
    [EnvKey(KeyNaming = EnvKeyNamingConvention.PreserveOriginal)]
    public string LogLevel { get; init; } = "";

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

A property whose type is another config object is bound as a **subsection**: its
members are read from the same flat key space, under a prefix derived from the
property. No opt-in is needed. A type is treated as a config object when it is a
non-abstract class or struct that isn't a collection or a `System.*` /
`Microsoft.*` framework type, and it has members to bind. Anything else is
reported as an unsupported member type (ENVR0010); apply `[EnvIgnore]` to skip it.

A subsection key is composed, in order, of:

1. the outer prefix (the root's `[EnvBindable]` prefix plus the segments of any
   further-out subsections)
2. the subsection property's segment (its `[EnvKey]` name, or the property
   name run through the naming convention)
3. the member's key

```csharp
public record Sub(string Val);

[EnvBindable]
public partial record Base(Sub Sub);
// Sub.Val -> SUB_VAL

[EnvBindable("APP")]
public partial record Prefixed(Sub Sub);
// Sub.Val -> APP_SUB_VAL
```

`[EnvKey]` on the subsection property adjusts its segment:

| Property attribute | Key for `Sub.Val` (root prefix `APP`) |
|---|---|
| (none) | `APP_SUB_VAL` |
| `[EnvKey("S")]` | `APP_S_VAL` |
| `[EnvKey("")]` | `APP_VAL` |
| `[EnvKey(IgnorePrefix = true)]` | `SUB_VAL` |
| `[EnvKey(KeyNaming = SnakeCase)]` | `APP_sub_val` |

`IgnorePrefix` drops the inherited prefix but keeps the property's own segment.
`KeyNaming` applies to the segment and every key beneath it.

### Optional subsections

A subsection's requirement follows the same rules as a leaf member: it is optional
when it's nullable, has an initializer, or sets `[EnvKey(Requirement = Optional)]`,
and required otherwise (including with the `required` keyword).

- A **required** subsection is always built. Each of its required members must be
  present.
- An **optional** subsection is built only when at least one key beneath it is
  present (any member key, or the type key of a polymorphic member). Then it binds
  in full, so its required members must be present too. With no keys, it gets its
  initializer, or `null`.

```csharp
public record Smtp
{
    public required string Host { get; init; }
    public int Port { get; init; } = 587;
}

[EnvBindable]
public partial record AppConfig(string Name, Smtp? Smtp);
// No SMTP_* keys        -> Smtp is null
// SMTP_HOST=mail        -> Smtp { Host = mail, Port = 587 }
// SMTP_PORT=25 only     -> throws: SMTP_HOST is missing
```

With `GeneratePopulate`, the Binder's `Populate` replaces an optional subsection
only when one of its keys was seen.

A type can't contain itself, directly or through its members (ENVR0033). Use
`[EnvIgnore]` on the member that leads back to it.

## Polymorphic binding

A property is bound polymorphically when its type is:

- a base class, record, or interface that lists its cases with
  `[EnvDerivedType<T>]` (any SDK), or
- a C# 15 `closed` class/record or `union`, which is detected automatically
  (**.NET 11 SDK** only).

### Explicit derived types: `[EnvDerivedType<T>]`

List each concrete case on the base type, as with System.Text.Json's
`[JsonDerivedType]`:

```csharp
[EnvDerivedType<RemoteStorage>]
[EnvDerivedType<LocalStorage>("DISK")]
public abstract record Storage;
public sealed record RemoteStorage(string Bucket, string Region) : Storage;
public sealed record LocalStorage(string Path) : Storage;
```

- Listed types must be concrete and derive from (or implement) the annotated
  type. A concrete base may list itself.
- The optional name overrides the case name. Without it, the case type's
  `[EnvCase]` applies, then the default name.
- The list is authoritative: unlisted subtypes are not bindable. Placed on a
  `closed` type, it also takes precedence over the discovered subtypes.
- The cases of a generic base (`Option<T>`) can't be listed, because C# doesn't
  allow type parameters in attribute arguments. Make the base `closed` instead
  (.NET 11 SDK), or list cases on a non-generic base.
- On SDKs before .NET 11, `[EnvPolymorphic]` on a type with no
  `[EnvDerivedType<T>]` reports **ENVR0031**, since closed types and unions can't
  be detected there.

Everything below applies to explicit hierarchies and closed types alike.

### Object cases: selected by a type key

When the cases are config objects (explicit derived types, closed types, or a
union of config records), a **type key** under the property's
section prefix names the case to bind. The type key is always required to
select a case: there is no guessing from which keys happen to be present.

```csharp
public closed record Storage;
public sealed record RemoteStorage(string Bucket, string Region) : Storage;
public sealed record LocalStorage(string Path) : Storage;

[EnvBindable]
public partial record AppConfig(Storage Storage);
```

```sh
STORAGE_TYPE=REMOTE
STORAGE_BUCKET=media
STORAGE_REGION=eu-west-1
```

The same works for a `closed` hierarchy or a union of config types:

```csharp
public closed record Storage;          // RemoteStorage / LocalStorage derive from it
public union Storage(RemoteStorage, LocalStorage);
```

- **Case names** default to the case type's name with the polymorphic type's
  name trimmed off (`RemoteStorage` in `Storage` -> `Remote`), run through the
  naming convention (`REMOTE`). Set one explicitly with `[EnvCase("...")]` on
  the case type, or with the `[EnvDerivedType<T>]` name. Names that contain
  digits are split like keys (`S3Storage` -> `S_3`), so use `[EnvCase("S3")]`
  for those.
- **Type key values match case-insensitively.** An unknown value throws
  `EnvInvalidValueException` listing the valid names.
- **Only the selected case is parsed.** Keys belonging to other cases are
  ignored, even if they hold values the other cases couldn't parse.
- **Nested closed hierarchies are flattened** to their concrete types, including
  a closed type used as a union case. Members declared on the closed base bind
  for every case.
- **Required vs. optional** follows the usual rules. A required member whose
  type key is missing throws `EnvMissingVariableException`; an optional one
  falls back to `null` or its initializer.
- Prefixes compose like a [subsection](#subsections): the outer prefix, then the
  property's segment (or its `[EnvKey]` name).

#### Key layout: `[EnvPolymorphic]`

`[EnvPolymorphic]` on the polymorphic type sets its defaults; on a property, it
overrides them for that property.

| `CaseKeys` | Keys for `RemoteStorage.Bucket` | Use when |
|---|---|---|
| `Shared` (default) | `STORAGE_BUCKET` | Cases are alternatives; one set of keys at a time |
| `Scoped` | `STORAGE_REMOTE_BUCKET` | Several cases' keys must coexist in one file |

```csharp
[EnvPolymorphic(TypeKey = "KIND", CaseKeys = EnvCaseKeyStyle.Scoped)]
public closed record Storage;
// STORAGE_KIND=REMOTE
// STORAGE_REMOTE_BUCKET=media
```

`TypeKey` is a literal (not run through the naming convention) and defaults to
`TYPE`.

### Scalar unions: tried in order (.NET 11 SDK)

A union whose cases are all scalar types binds from a single key. The value is
parsed against each case **in declaration order**, and the first case that
accepts it wins:

```csharp
public union Timeout(int, TimeSpan);
// TIMEOUT=30        -> int
// TIMEOUT=00:00:30  -> TimeSpan
```

If no case accepts the value, `EnvInvalidValueException` lists every case tried.
A `string` case accepts anything, so declare it last; cases after it are
unreachable (**ENVR0029**). A union cannot mix scalar and object cases
(**ENVR0028**).

### Polymorphic root

A polymorphic type can be bound directly. The type key and case keys sit under
the root prefix:

```csharp
[EnvBindable]
public closed partial record Deployment(string Region);
public sealed record Canary(string Region, int Percent) : Deployment(Region);
public sealed record BlueGreen(string Region, string Slot) : Deployment(Region);

// TYPE=CANARY, REGION=eu-west-1, PERCENT=10
var deployment = Deployment.Bind(DotEnvPaths.AppDirectory());
```

The same works with `[EnvDerivedType<T>]` on any SDK, with an object union, and on
an external host (`[EnvBindable<Deployment>("DEPLOY")]` reads `DEPLOY_TYPE`). The
type key is always required. Two limits:

- A scalar union has no key of its own, so it can't be the root (**ENVR0034**).
  Bind it as a member instead.
- `GeneratePopulate` is ignored (**ENVR0035**): an existing instance can't change
  its case.

### Streaming binder

The generated `Binder` buffers the raw values of a polymorphic member's keys
and resolves them in `Build()`, so the type key may appear anywhere in the
input, including after the case's own keys. With `GeneratePopulate`, the
binder's `Populate` replaces a polymorphic member only when its type key (or
scalar key) was seen.

## Other attributes

| Attribute | Effect |
|---|---|
| `[EnvIgnore]` | Skip a member that would otherwise be bound. |
| `[EnvUri(UriKind)]` | Specify `UriKind` for `Uri` members (default `Absolute`). |
| `[EnvDerivedType<T>(name?)]` | List a concrete case of a polymorphic base type. See [Polymorphic binding](#polymorphic-binding). |
| `[EnvPolymorphic]` | Type key name and key layout for a polymorphic member. |
| `[EnvCase(name)]` | Name a polymorphic case type is selected by. |
| `[EnvFormatProvider(type, memberName)]` | Point at a static `IFormatProvider` member used when parsing numbers, dates, etc. Applies type-wide when placed on the class/struct; per-member when placed on a field/property. |

## Required vs. optional

Each member is classified as **required**, **optional**, or
**with-default**. The generator infers from C# signals, in order:

1. `required` modifier -> required
2. Nullable value or reference type -> optional
3. Property initializer, or a default on the record constructor parameter that
   binds it (`record Db(string Host, int Port = 5432)`) -> optional with default
4. Non-nullable type -> required
5. Reference type under `#nullable disable` -> optional

Required members that are missing throw `EnvMissingVariableException` at `Bind()` time
with the failing key. Optional members fall back to `default(T)` or the
declared default. The same rules decide whether a
[subsection is optional](#optional-subsections).

Override the inferred classification with
`[EnvKey(Requirement = EnvRequirement.Required | .Optional)]`.

## Validation

Annotate members with `System.ComponentModel.DataAnnotations` attributes and they
are checked automatically after binding. Any failure throws
`EnvValidationException` at `Bind()` / `Build()` time, so an invalid environment
never reaches your app:

```csharp
using System.ComponentModel.DataAnnotations;

[EnvBindable]
public partial class ServerConfig
{
    [Range(1, 65535)]
    public int Port { get; init; }

    [StringLength(253)]
    public required string Host { get; init; }
}

// Throws EnvValidationException if PORT is outside 1..65535 or HOST exceeds 253 chars.
var cfg = ServerConfig.Bind(DotEnvPaths.AppDirectory());
```

> [!NOTE]
> **Validation is separate from required-ness.** Whether a key must be *present*
> is Enver's decision (the `required` keyword, nullability, or
> `[EnvKey(Requirement)]`) and a missing key throws `EnvMissingVariableException`
> *before* validation runs. DataAnnotations validate the *value* after binding and
> throw `EnvValidationException`. The two compose: `[Required]` adds a non-empty
> check on top of presence (so it catches `HOST=`), but an entirely absent key is
> still reported earlier by Enver. Prefer Enver's mechanisms for presence and
> DataAnnotations for value shape.

- **All failures are aggregated.** `EnvValidationException.Failures` lists every
  validation message.
- **Custom and cross-field rules:** implement `IValidatableObject` on the config
  type (or any subsection) for logic the attributes can't express.
- **Subsections validate too.** Each nested config validates itself, so a
  `[Range]` on a subsection member is enforced when the parent binds. Nested
  objects (subsections and the selected polymorphic case) are checked before
  their parent, and the first object that fails is reported.

### Validating separately

Pass `validate: false` to `Bind`, `Build` or `Populate` to get the bound instance
without checking it, and call the generated `Validate` when you're ready. It
checks the instance and everything beneath it, and throws the same
`EnvValidationException`:

```csharp
var cfg = ServerConfig.Bind(DotEnvPaths.AppDirectory(), validate: false);
// ... adjust cfg, or decide when to fail ...
ServerConfig.Validate(cfg);
```

`Validate` is generated for every bindable type, including ones with no rules,
where it does nothing. On an external host it is suffixed like the factories:
`Configs.ValidateCacheConfig(cfg)`. With `GeneratePopulate`, `Populate` validates
the whole instance after assigning, including members it didn't touch.
- **Discovered at compile time.** The generator reads your attributes during
  generation and invokes them directly, rather than scanning your type with
  `Validator.ValidateObject`. `[Display(Name = "...")]` (including resource-based
  names) feeds the error messages so they match standard DataAnnotations output.

Custom validators work the same way. Derive from `ValidationAttribute`:

```csharp
public sealed class UppercaseAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
        => value is string s && s == s.ToUpperInvariant()
            ? ValidationResult.Success
            : new ValidationResult($"{ctx.DisplayName} must be uppercase");
}
```

> [!NOTE]
> **The typed `[Range(Type, string, string)]` form is not supported.** It parses its
> bounds reflectively at runtime, which Enver's reflection-free model can't honor. The
> generator reports **ENVR0020**. Use the numeric `[Range(min, max)]` overload, a
> `[CustomValidation]` method, or `IValidatableObject` instead.

### Trimming and AOT

The generator never calls `Validator.ValidateObject`. It discovers your attributes
at compile time and either **invokes them directly** or **synthesizes an equivalent
inline check**, so validation is fully reflection-free.

However, a handful of built-in attributes carry `[RequiresUnreferencedCode]` on their
constructor (`[MinLength]`, `[MaxLength]`, `[Length]`, `[Compare]`). Because of this,
you'll see an **IL2026** warning at the attribute in your own source, even though
Enver's generated check for it is reflection-free:

```csharp
[MinLength(3)] // warning IL2026 here, at the attribute
public string Name { get; init; } = "";
```

The warning isn't wrong in the general case, but it is safe to suppress if you only
use the generated bindings to validate your class:

```csharp
[UnconditionalSuppressMessage("Trimming", "IL2026",
    Justification = "Validated only via Enver's reflection-free generated binder.")]
[MinLength(3)]
public string Name { get; init; } = "";
```

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
var cfg = Configs.BindCacheConfig(DotEnvPaths.AppDirectory());
Configs.ValidateCacheConfig(cfg);
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
