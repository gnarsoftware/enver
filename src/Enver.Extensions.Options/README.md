# Enver.Extensions.Options

`Microsoft.Extensions.Options` integration for [Enver.Binding](https://github.com/gnarsoftware/enver/tree/main/src/Enver.Binding).
Register an `[EnvBindable]` type as options in one call. It's bound from
`IConfiguration` by the generated binder and validated when the host starts.

The built-in options binder needs a parameterless constructor and settable
properties. This one creates instances through the generated binder, so records,
primary constructors and `required` members work.

Part of the [Enver](https://github.com/gnarsoftware/enver) family. To also load
`.env` files into configuration, reference the `Gnar.Enver.Extensions`
metapackage, which brings in both.

## Quick start

```sh
dotnet add package Gnar.Enver.Extensions.Options
```

```csharp
using System.ComponentModel.DataAnnotations;
using Enver.Binding;
using Enver.Extensions.Options;

[EnvBindable("APP")]
public partial record AppOptions(string Name, [property: Range(1, 64)] int Workers = 4);

builder.Services.AddEnvOptions<AppOptions>();

// Later, anywhere:
public sealed class Worker(IOptions<AppOptions> options) { ... }
```

`AppOptions` reads `APP_NAME` and `APP_WORKERS` from configuration, so environment
variables, `.env` files (with `Gnar.Enver.Extensions.Configuration`) and any other
source work.

## Behavior

- **Validated at startup.** The generated `Validate` runs as an options validator,
  and `ValidateOnStart` is on, so an invalid value fails host startup with
  `OptionsValidationException`. A missing required key fails it with
  `EnvMissingVariableException`. Pass `validateOnStart: false` to defer both to
  first use.
- **Skipping Enver validation.** Pass `validate: false` to leave the generated
  `Validate` out, for example when your own validators cover it. Binding errors
  and validators added through the returned `OptionsBuilder<T>` still run, at
  startup unless `validateOnStart` is also off.
- **Keys are the flat Enver keys**, read from the root of the configuration.
  With `Separator = EnvKeySeparator.DoubleUnderscore`, `APP__DB__HOST` reads the
  section path `APP:DB:HOST`. That's where the environment-variable and `.env`
  providers put it.
- **Another configuration.** Pass one to bind from it instead of the
  `IConfiguration` service, such as a section:
  `AddEnvOptions<AppOptions>(configuration.GetSection("Enver"))`.
- **Reloads.** `IOptionsMonitor<T>` and `IOptionsSnapshot<T>` rebind when the
  configuration reloads.
- **Configure steps still run.** `Configure` and `PostConfigure` on the returned
  `OptionsBuilder<T>` run after binding (for types with settable members), and
  other validators run alongside the generated one.

## External hosts and internal bindings

`AddEnvOptions<T>()` needs `T` to implement `IEnvBinding<T>`, which the generator
adds to public `[EnvBindable]` types. For an external host (`[EnvBindable<T>]`) or a
type generated with `Visibility = EnvVisibility.Internal`, pass the generated
binding class:

```csharp
[EnvBindable<DbOptions>("DB")]
public static partial class Bindings;

builder.Services.AddEnvOptions<DbOptions, Bindings.DbOptionsEnvBinding>();
```

## License

MIT.
