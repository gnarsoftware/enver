# Contributing to Enver

## Prerequisites

- .NET SDK 8, 9, or 10

## Building

```sh
dotnet build Enver.slnx
```

## Testing

```sh
dotnet test Enver.slnx
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
