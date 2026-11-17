# Enver.Extensions

One package reference for using [Enver](https://github.com/gnarsoftware/enver)
in an app built on `Microsoft.Extensions` (ASP.NET Core, the generic host, workers).
It has no code of its own and brings in:

- [`Gnar.Enver.Extensions.Configuration`](https://github.com/gnarsoftware/enver/tree/main/src/Enver.Extensions.Configuration):
  load `.env` files into `IConfiguration`.
- [`Gnar.Enver.Extensions.Options`](https://github.com/gnarsoftware/enver/tree/main/src/Enver.Extensions.Options):
  bind `[EnvBindable]` types (records included) from configuration as validated
  options. This also brings `Gnar.Enver.Binding` and its source generator.

```sh
dotnet add package Gnar.Enver.Extensions
```

```csharp
using Enver.Binding;
using Enver.Extensions.Configuration;
using Enver.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddDotEnvFiles();
builder.Services.AddEnvOptions<AppOptions>();

[EnvBindable("APP")]
public partial record AppOptions(string Name, int Workers = 4);
```

Either half works on its own: reference only the package you need.

## License

MIT.
