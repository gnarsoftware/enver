using System.Collections;

namespace Enver;

/// <summary>
/// Composable builder for the canonical .env load ladder. Enumerates resolved
/// file paths in load order; later files override earlier ones.
/// </summary>
/// <remarks>
/// Modifier call order does not affect the generated load order. The canonical
/// precedence per directory is <c>.env</c>, then <c>.env.{variant}</c>.
/// Local-tier files (<c>.env.local</c>, <c>.env.{variant}.local</c>) emit at the
/// start directory only, after the ancestor walk.
/// </remarks>
public readonly record struct DotEnvPaths : IEnumerable<string>
{
    internal enum RootKind
    {
        Unconfigured = 0,
        AppDirectory,
        WorkingDirectory,
        Explicit,
        Relative,
    }

    internal RootKind Root { get; init; }
    internal string? ExplicitDir { get; init; }
    internal string? FileName { get; init; }
    internal string? Variant { get; init; }
    internal bool Local { get; init; }
    internal int ParentDirs { get; init; }

    // --- Roots ---

    /// <summary>
    /// Roots the ladder at <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    public static DotEnvPaths AppDirectory()
    {
        return new() { Root = RootKind.AppDirectory, FileName = ".env" };
    }

    /// <summary>
    /// Roots the ladder at <see cref="System.IO.Directory.GetCurrentDirectory"/>,
    /// resolved at enumeration time.
    /// </summary>
    public static DotEnvPaths WorkingDirectory()
    {
        return new() { Root = RootKind.WorkingDirectory, FileName = ".env" };
    }

    /// <summary>
    /// Roots the ladder at the given directory.
    /// </summary>
    public static DotEnvPaths Directory(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        return new()
        {
            Root = RootKind.Explicit,
            ExplicitDir = directory,
            FileName = ".env",
        };
    }

    /// <summary>
    /// Emits relative filenames. Resolution is determined by the consumer
    /// (ASP.NET configuration sources resolve via their IFileProvider; other
    /// consumers resolve via the process working directory).
    /// </summary>
    public static DotEnvPaths Relative()
    {
        return new() { Root = RootKind.Relative, FileName = ".env" };
    }

    // --- Modifiers ---

    /// <summary>
    /// Overrides the base filename. Defaults to <c>.env</c>.
    /// </summary>
    public DotEnvPaths WithFileName(string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        return this with { FileName = fileName };
    }

    /// <summary>
    /// Adds the <c>.env.{variant}</c> tier (and <c>.env.{variant}.local</c>
    /// when <see cref="WithLocal"/> is also set).
    /// </summary>
    public DotEnvPaths WithVariant(string variant)
    {
        ArgumentException.ThrowIfNullOrEmpty(variant);
        return this with { Variant = variant };
    }

    /// <summary>
    /// Adds <c>.env.local</c> (and <c>.env.{variant}.local</c> when a variant
    /// is also configured), emitted at the start directory only.
    /// </summary>
    public DotEnvPaths WithLocal()
    {
        return this with { Local = true };
    }

    /// <summary>
    /// Walks up to <paramref name="max"/> ancestor directories, emitting
    /// <c>.env</c> and <c>.env.{variant}</c> in each (farthest-first). Local-tier
    /// files are not propagated into ancestors. Not supported on
    /// <see cref="Relative"/> builders, which have no anchor to walk from.
    /// </summary>
    public DotEnvPaths WithParentDirectories(int max)
    {
        if (Root == RootKind.Relative)
        {
            throw new InvalidOperationException(
                "WithParentDirectories cannot be applied to a Relative builder; "
                    + "anchor with Directory(...), AppDirectory(), or WorkingDirectory()."
            );
        }
        ArgumentOutOfRangeException.ThrowIfNegative(max);
        return this with { ParentDirs = max };
    }

    /// <summary>
    /// Adds the conventional ladder: a variant tier (if provided) and local-tier files.
    /// <br />
    /// Equivalent to <c>WithVariant(variant).WithLocal()</c>.
    /// </summary>
    public DotEnvPaths Standard(string? variant = null)
    {
        var withVariant = string.IsNullOrEmpty(variant) ? this : WithVariant(variant);
        return withVariant.WithLocal();
    }

    // --- Enumeration ---

    /// <summary>
    /// Enumerates the resolved file paths in load order.
    /// </summary>
    public Stack<string>.Enumerator GetEnumerator()
    {
        if (FileName is null)
        {
            throw new InvalidOperationException(
                "DotEnvPaths has no root configured. Use AppDirectory(), "
                    + "WorkingDirectory(), or Directory(...) to create a builder."
            );
        }

        var startDir = Root switch
        {
            RootKind.AppDirectory => AppContext.BaseDirectory,
            RootKind.WorkingDirectory => System.IO.Directory.GetCurrentDirectory(),
            RootKind.Explicit => ExplicitDir!,
            RootKind.Relative => string.Empty,
            _ => throw new InvalidOperationException("DotEnvPaths has no root configured."),
        };

        // Fill the stack in REVERSE load order: Stack<T>.Enumerator yields LIFO
        // Load order is farthest-ancestor -> start dir -> local tiers, so we push in the
        // opposite order: locals first, then start dir tiers, then ascending
        // parents. Within each directory, .env precedes .env.{variant}, so
        // PushTiers pushes the variant first (deeper in the stack) and the
        // base second (on top, yields first).
        var stack = new Stack<string>();

        if (Local)
        {
            if (!string.IsNullOrEmpty(Variant))
            {
                stack.Push(Path.Combine(startDir, $"{FileName}.{Variant}.local"));
            }
            stack.Push(Path.Combine(startDir, $"{FileName}.local"));
        }

        PushTiers(stack, startDir, FileName, Variant);
        var current = startDir;
        for (int i = 0; i < ParentDirs; i++)
        {
            var parent = System.IO.Directory.GetParent(current)?.FullName;
            if (parent is null || PathsEqual(parent, current))
            {
                break;
            }
            PushTiers(stack, parent, FileName, Variant);
            current = parent;
        }

        return stack.GetEnumerator();

        static void PushTiers(Stack<string> s, string dir, string fileName, string? variant)
        {
            if (!string.IsNullOrEmpty(variant))
            {
                s.Push(Path.Combine(dir, $"{fileName}.{variant}"));
            }
            s.Push(Path.Combine(dir, fileName));
        }
    }

    IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    // --- Internals ---

    private static bool PathsEqual(string a, string b)
    {
        return Path.TrimEndingDirectorySeparator(a.AsSpan())
            .Equals(
                Path.TrimEndingDirectorySeparator(b.AsSpan()),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal
            );
    }
}
