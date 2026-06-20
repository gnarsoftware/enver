# Changelog

All notable changes to Enver and its companion packages are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Each release covers the full Enver family of packages which are versioned and released
together.

## Unreleased

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
