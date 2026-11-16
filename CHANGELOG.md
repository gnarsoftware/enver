# Changelog

All notable changes to Enver and its companion packages are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Each release covers the full Enver family of packages which are versioned and released
together.

## Unreleased

### Removed

- **Breaking:** dropped the `net8.0` and `net9.0` targets, which are no longer
  supported by Microsoft. All packages now target `net10.0` and `net11.0`.

### Added

- `net11.0` target for all packages.
- `Enver.Binding`: polymorphic binding. List a base type's cases with
  `[EnvDerivedType<T>]` (any SDK), or use C# 15 `closed` types and `union`s, detected
  automatically with the .NET 11 SDK. Object cases are selected by a type key
  (`STORAGE_TYPE=REMOTE`) with a shared or scoped key layout; all-scalar unions
  parse the value against each case in order. New `[EnvDerivedType<T>]`,
  `[EnvPolymorphic]` and `[EnvCase]` attributes, the `EnvCaseKeyStyle` enum, and
  diagnostics ENVR0025-ENVR0032.
- `Enver.Binding`: the package now ships one generator build per Roslyn version
  (`analyzers/dotnet/roslyn5.0` and `roslyn5.12`). The baseline build now
  requires Roslyn 5.0, the compiler in every .NET 10 SDK; older SDKs could not
  build `net10.0` consumers anyway.

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
