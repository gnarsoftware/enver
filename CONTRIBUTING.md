# Contributing to Enver

## Prerequisites

- .NET SDK 11 (the repo targets `net10.0` and `net11.0`, and builds a Roslyn 5.12
  variant of the binding generator). Install the .NET 10 runtime as well to run
  the `net10.0` tests.

## Building

```sh
dotnet build Enver.slnx
```

The solution includes the sample apps under `samples/`, so they build (and must
keep compiling) along with the libraries.

## Testing

```sh
dotnet test Enver.slnx
```

The binding source generator ships two builds: a baseline one (Roslyn 4.8) for
older SDKs and a Roslyn 5.12 one that adds polymorphic binding for `closed` types
and unions. The generator tests run against the 5.12 build by default; CI also
runs them against the baseline build:

```sh
dotnet test tests/Enver.Binding.Generator.Tests -p:EnverGeneratorRoslyn=4.8
```

## Formatting

[CSharpier](https://csharpier.com/) is used for formatting.

```sh
dotnet tool restore
dotnet csharpier format .  # format
dotnet csharpier check .   # verify
```

## Submitting changes

**Bug fixes**: open a PR. Include a test that reproduces the bug.

**New features or behavior changes**: open an issue first.

**Parsing/grammar changes**: open an issue and reference [`PARSING.md`](PARSING.md). Changes to parsing behavior need to be reflected in that document.

PRs should target `main`. Commits within your branch don't need to follow any convention, but the **PR title must follow [Conventional Commits](https://www.conventionalcommits.org/)**

By submitting a pull request, you agree that your contributions will be licensed under the [MIT License](LICENSE).
