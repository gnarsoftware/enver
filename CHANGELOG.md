# Changelog

All notable changes to Enver and its companion packages are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Each release covers the full Enver family of packages which are versioned and released
together.

## Unreleased

### Upgrading from 1.x

- Target `net10.0` or later.
- Replace `[EnvConfig("DB", ...)]` with the same settings on `[EnvBindable("DB", ...)]`
  (or `[EnvBindable<T>("DB", ...)]`). `[EnvConfig]` now fails to compile with a
  message that shows the new form.
- Subsection keys are named by the property, without the subsection type's own
  prefix: `SUB_VAL` rather than `SUB_<prefix>_VAL`. To keep the old keys, set the
  segment explicitly: `[EnvKey("SUB_<prefix>")]` on the property.
- `[EnvKey]` and `[EnvConfig]` are no longer needed to mark subsections. Remove
  them, or keep `[EnvKey]` where it renames the segment.
- An optional subsection (nullable or with an initializer) is now `null` or its
  initializer when none of its keys are set, instead of an instance with defaults.
- `Populate` now validates the whole instance. Pass `validate: false` to skip it.

### Removed

- **Breaking:** dropped the `net8.0` and `net9.0` targets, which are no longer
  supported by Microsoft. All packages now target `net10.0` and `net11.0`.

### Changed

- **Breaking:** `Enver.Binding`: `[EnvConfig]` is replaced by settings on
  `[EnvBindable]` and `[EnvBindable<T>]`: `[EnvBindable("DB", KeyNaming = ...,
  GeneratePopulate = true)]`. `[EnvConfig]` is now an obsolete-as-error attribute
  whose message explains the move. A type's prefix applies only when it is bound as
  the root; as a subsection it is named by its property, so the old
  `SUB_<prefix>_VAL` stacking is gone.
- **Breaking:** `Enver.Binding`: subsections are detected automatically. Any member
  typed as a non-abstract class or struct with bindable members (excluding
  collections and `System.*`/`Microsoft.*` types) binds as a subsection; `[EnvKey]`
  or `[EnvConfig]` is no longer needed to opt in. Types that previously reported
  ENVR0010 may now bind; apply `[EnvIgnore]` to skip them.
- **Breaking:** `Enver.Binding`: optional subsections (nullable, with an initializer,
  or `Requirement = Optional`) are built only when at least one of their keys is
  present, and otherwise get their initializer or `null`. Previously every
  subsection was always constructed, so a nullable subsection with no keys set was
  a default-filled instance rather than `null`, and initializers were ignored.
- `Enver.Binding`: validation runs once, after the whole object is built, instead
  of as each nested object is constructed. A missing required key is now always
  reported before any validation failure. `Populate` now validates the whole
  instance after assigning; previously only the subsections it built were checked.
- `Enver.Binding`: ENVR0012 now only reports keys that can't be used at all (empty,
  or containing whitespace, control characters, quotes or backslashes). Other
  invalid environment variable names report ENVR0038 or ENVR0039 instead.

### Added

- `net11.0` target for all packages.
- `Enver.Extensions.Options`: new package. `services.AddEnvOptions<T>()` registers an
  `[EnvBindable]` type as options bound from `IConfiguration` by the generated
  binder, so records and `required` members work. Validation runs at startup by
  default (`validateOnStart: false` defers it, `validate: false` skips the generated
  validation), configure steps and other validators still apply, and options rebind
  on configuration reload.
- `Enver.Extensions`: new metapackage referencing `Enver.Extensions.Configuration`
  and `Enver.Extensions.Options`.
- `Enver.Binding`: polymorphic binding. List a base type's cases with
  `[EnvDerivedType<T>]` (any SDK), or use C# 15 `closed` types and `union`s, detected
  automatically with the .NET 11 SDK. Object cases are selected by a type key
  (`STORAGE_TYPE=REMOTE`) with a shared or scoped key layout; all-scalar unions
  parse the value against each case in order. A polymorphic type can also be the
  binding root (`[EnvBindable] closed partial record Deployment`), selected by
  `TYPE` under the root prefix. New `[EnvDerivedType<T>]`, `[EnvPolymorphic]` and
  `[EnvCase]` attributes, the `EnvCaseKeyStyle` enum, and diagnostics
  ENVR0025-ENVR0032, ENVR0034 and ENVR0035.
- `Enver.Binding`: `[EnvFallbackKey(name, IgnorePrefix = ...)]` adds keys tried
  after a member's primary key, in declaration order. The first present key wins
  regardless of the order keys appear in, and a missing required member lists
  every key it tried. ENVR0036 warns when it is placed on a subsection or
  polymorphic member.
- `Enver.Binding`: generated `EnvKeys` and `EnvKeysUtf8` classes (`{Type}EnvKeys` on
  an external host) expose every bound key as a `const string` or UTF-8 span,
  nested like the config: `AppConfig.EnvKeys.Db.Pool.MaxSize`. Set
  `GenerateKeys = false` to skip them. ENVR0037 reports a constant renamed to
  avoid clashing with its enclosing class.
- `Enver.Binding`: a generated `Validate(instance)` (`Validate{Type}` on an external
  host) checks an instance and everything beneath it on demand. `Bind`, `Build`
  and `Populate` take `validate: false` to skip validation. Both are generated
  only for types with validation rules somewhere in their tree.
- `Enver.Binding`: `Separator = EnvKeySeparator.DoubleUnderscore` on `[EnvBindable]`
  and `[EnvBindable<T>]` joins key segments with `__` (`APP__DB__POOL__MAX_SIZE`),
  the ASP.NET Core convention. Underscores inside a name stay single.
- `Enver.Binding`: keys that aren't valid environment variable names, such as
  configuration paths (`App:Db:Host`), are now generated so they can be read from
  other configuration sources. A member's key like this reports ENVR0038 (error)
  and a fallback ENVR0039 (info), unless `[EnvKey]` or `[EnvFallbackKey]` sets
  `AllowNonEnvironmentKey = true`.
- `Enver.Binding`: `Visibility = EnvVisibility.Internal` on `[EnvBindable]` and
  `[EnvBindable<T>]` makes all generated members `internal`.
- `Enver.Binding`: `IEnvBinding<T>` with static `Bind` and `Validate`, matching the
  generated methods, implemented by public `[EnvBindable]` types and by a nested
  `EnvBinding` class for external hosts and internal bindings.
- `Enver.Binding`: `[EnvKey(KeyNaming = ...)]` overrides the naming convention for
  a member's derived key and, on a subsection, everything beneath it.
- `Enver.Binding`: ENVR0033 reports a config type that contains itself, which
  previously overflowed the generator's stack.
- `Enver.Binding`: the package now ships one generator build per Roslyn version
  (`analyzers/dotnet/roslyn5.0` and `roslyn5.12`). The baseline build now
  requires Roslyn 5.0, the compiler in every .NET 10 SDK; older SDKs could not
  build `net10.0` consumers anyway.

### Fixed

- `Enver.Binding`: a default on a record's primary-constructor parameter
  (`record Db(string Host, int Port = 5432)`) now makes the member optional with that
  default, like a property initializer. Previously the key was required.
- `Enver.Binding`: members whose names are C# keywords (`public string @event`) no
  longer produce generated code that fails to compile.
- `Enver.Binding`: a bindable type deriving from another bindable type no longer
  gets CS0108 warnings for the generated members that hide the base type's.
- `Enver.Binding`: a member whose name clashes with generated code (`Binder`,
  `EnvKeys`, `EnvKeysUtf8`, `EnvBinding`, or a non-method `Bind`, `Validate` or
  `Populate`) reports ENVR0040 instead of compile errors in generated code.
- `Enver.Binding`: the Binder's `Populate` no longer fails to compile for an
  optional non-nullable value-type member (`public int Port { get; set; } = 1`).

## [1.0.2] - 2026-06-19

### Fixed

- `Enver.Extensions.Configuration`: Added plain `environment` key to the host environment lookup.

## [1.0.1] - 2026-06-15

### Fixed

- Fixed issue in `Enver.Binding` source generation causing excessive re-runs
- Fixed binding when target type is subclassed in `Enver.Binding`
- Fixed binding to nullable subsections in `Enver.Binding`

## [1.0.0] - 2026-05-29

Initial stable release of the Enver `.env` parser and its integration family.

Targets `net8.0`, `net9.0`, `net10.0`.

### Added

- **`Enver`**: core strict `.env` parser with an explicit, documented grammar.
  See [PARSING.md](PARSING.md).
- **`Enver.InMemory`**: load `.env` files into `EnvCollection`, a
  `Dictionary<string, string>` subclass with platform-appropriate key
  comparison and typed accessors.
- **`Enver.SystemEnvironment`**: load `.env` files into the process
  environment block; typed accessors over `Environment.Variables`.
- **`Enver.Extensions.Configuration`**: `AddDotEnvFiles(...)` for ASP.NET Core
  and `Microsoft.Extensions.Configuration`, plus `AsEnvReader()` to layer
  Enver's typed accessors over any `IConfiguration`.
- **`Enver.Binding`**: `[EnvBindable]` source generator for strongly-typed
  configuration binding, with reflection-free `DataAnnotations` /
  `IValidatableObject` validation at bind time.
