namespace Enver.Tests;

public class GeneratorDriverTests
{
    // --- Happy paths ---

    [Test]
    public void EmitsBinderForRecordWithPrimaryConstructor()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial record DbConfig(int Port, string Host);
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("public static global::Test.DbConfig Bind("));
            Assert.That(src, Does.Contain("BindFromAppDirectory"));
            Assert.That(src, Does.Contain("BindFromFile"));
            Assert.That(src, Does.Contain("class Binder"));
            // The members must survive analysis
            Assert.That(src, Does.Contain("_val_Port"));
            Assert.That(src, Does.Contain("_val_Host"));
            Assert.That(src, Does.Contain("\"PORT\"u8"));
            Assert.That(src, Does.Contain("\"HOST\"u8"));
        }
    }

    [Test]
    public void EmitsBinderForClassWithInitOnlyProperties()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class AppConfig
            {
                public string Name { get; init; } = "";
                public int Workers { get; init; }
            }
            """
        );

        Assert.That(result.SingleSource().Text, Does.Contain("_val_Name"));
    }

    [Test]
    public void AppliesPrefixAndKeyNamingFromEnverConfig()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig("DB", KeyNaming = Enver.SourceGeneration.EnverKeyNamingConvention.UpperSnakeCase)]
            public partial record PrefixedConfig(string HostName);
            """
        );

        // "HostName" -> UpperSnakeCase "HOST_NAME", prefixed with "DB_".
        Assert.That(result.SingleSource().Text, Does.Contain("DB_HOST_NAME"));
    }

    [Test]
    public void EmitsCompilableBinderAcrossTheSupportedTypeMatrix()
    {
        // Exercises every emitter dispatch arm on both the byte-path Binder and the
        // string-path Bind(IEnvReader). RunExpectingSuccess compiles the
        // generated code, so a broken arm fails the build here.
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public enum Mode { Off, On }

            [Enver.SourceGeneration.EnverBindable]
            public partial record FullConfig(
                string S,
                int N,
                long L,
                float F,
                double Dbl,
                decimal Dec,
                bool Flag,
                char C,
                Mode M,
                System.Guid Id,
                System.Version Ver,
                System.DateTime When,
                System.TimeSpan Span,
                System.Net.IPAddress Ip);
            """
        );
    }

    [Test]
    public void EmitsCompilableBinderForOptionalAndDefaultedMembers()
    {
        // Optional members exercise GetOptional* / GetOptionalRef<T> selection:
        // char? is a value type (GetOptional<char>), IPAddress? a reference
        // type (GetOptionalRef<IPAddress>). A defaulted member takes the
        // Get*(key, defaultValue) overload.
        GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class OptionalConfig
            {
                public char? MaybeChar { get; init; }
                public System.Net.IPAddress? MaybeIp { get; init; }
                public int? MaybeInt { get; init; }
                public System.Version? MaybeVersion { get; init; }
                public string Defaulted { get; init; } = "fallback";
            }
            """
        );
    }

    [Test]
    public void EmitsCompilableBinderWithEnverUriKind()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class UriConfig
            {
                [Enver.SourceGeneration.EnverUri(System.UriKind.Relative)]
                public System.Uri Endpoint { get; init; }
            }
            """
        );

        // [EnverUri] threads UriKind.Relative into both the byte-path
        // new Uri(...) and the string-path GetUri(...) calls.
        Assert.That(result.SingleSource().Text, Does.Contain("global::System.UriKind.Relative"));
    }

    [Test]
    public void EmitsCompilableBinderWithFormatProviderPrecedence()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverFormatProvider(typeof(System.Globalization.CultureInfo), "InvariantCulture")]
            public partial class FpConfig
            {
                public double UsesDefault { get; init; }

                [Enver.SourceGeneration.EnverFormatProvider(typeof(System.Globalization.CultureInfo), "CurrentCulture")]
                public double UsesOverride { get; init; }
            }
            """
        );

        // Type-level provider applies to UsesDefault; the member-level
        // [EnverFormatProvider] overrides it on UsesOverride.
        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                src,
                Does.Contain("global::System.Globalization.CultureInfo.InvariantCulture")
            );
            Assert.That(
                src,
                Does.Contain("global::System.Globalization.CultureInfo.CurrentCulture")
            );
        }
    }

    [Test]
    public void AcceptsPrivateFormatProviderWithinHost()
    {
        // A private static member of the host itself is accessible to the
        // generated code (which lives in the host's partial / nested Binder).
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverFormatProvider(typeof(SelfFp), "s_culture")]
            public partial class SelfFp
            {
                private static readonly System.Globalization.CultureInfo s_culture =
                    System.Globalization.CultureInfo.InvariantCulture;

                public int Count { get; init; }
            }
            """
        );

        Assert.That(result.SingleSource().Text, Does.Contain("global::Test.SelfFp.s_culture"));
    }

    // --- External hosts: [EnverBindable<T>] ---

    [Test]
    public void EmitsBinderForExternalTarget()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public record DbConfig(int Port, string Host);

            [Enver.SourceGeneration.EnverBindable<DbConfig>]
            public partial class AppHost;
            """
        );

        // Methods are suffixed with the target's simple name; the binder is
        // nested in the host (AppHost), not the target.
        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("public static global::Test.DbConfig BindDbConfig("));
            Assert.That(src, Does.Contain("BindDbConfigFromFile"));
            Assert.That(src, Does.Contain("BindDbConfigFromAppDirectory"));
            Assert.That(src, Does.Contain("class DbConfigBinder"));
            Assert.That(src, Does.Contain("partial class AppHost"));
        }
    }

    [Test]
    public void EmitsSeparateFilesPerExternalTarget()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public record DbConfig(int Port);
            public record CacheConfig(int Ttl);

            [Enver.SourceGeneration.EnverBindable<DbConfig>]
            [Enver.SourceGeneration.EnverBindable<CacheConfig>]
            public partial class AppHost;
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratedSources, Has.Length.EqualTo(2));
            var combined = string.Concat(result.GeneratedSources.Select(s => s.Text));
            Assert.That(combined, Does.Contain("BindDbConfig"));
            Assert.That(combined, Does.Contain("BindCacheConfig"));
        }
    }

    [Test]
    public void EmitsSeparateFilesForSelfBindAndExternalTargetOnOneHost()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public record DbConfig(int Port);

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverBindable<DbConfig>]
            public partial record AppConfig(string Name);
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratedSources, Has.Length.EqualTo(2));
            var combined = string.Concat(result.GeneratedSources.Select(s => s.Text));
            Assert.That(combined, Does.Contain("global::Test.AppConfig Bind("));
            Assert.That(combined, Does.Contain("BindDbConfig"));
        }
    }

    [Test]
    public void ExternalTargetSilentlySkipsMembersWithHostInaccessibleSetters()
    {
        // Secret has a private setter, so the external host AppHost cannot
        // assign it. Without an explicit [EnverKey] opt-in it's dropped quietly.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            public class DbConfig
            {
                public string Host { get; init; } = "";
                public string Secret { get; private set; } = "";
            }

            [Enver.SourceGeneration.EnverBindable<DbConfig>]
            public partial class AppHost;
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("_val_Host"));
            Assert.That(src, Does.Not.Contain("Secret"));
        }
    }

    // --- Diagnostics ---

    [Test]
    public void ReportsEnvr0001WhenHostIsNotPartial()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public record NotPartialConfig(int Port);
            """
        );

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0001"));
            Assert.That(result.GeneratedSources, Is.Empty);
        }
    }

    [Test]
    public void ReportsEnvr0012WhenPrefixProducesInvalidKey()
    {
        // A prefix with a hyphen yields an invalid env-var key.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig("bad-prefix")]
            public partial record InvalidKeyConfig(int Port);
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0012"));
    }

    [Test]
    public void ReportsEnvr0008WhenIgnoredMemberAlsoHasKey()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class RedundantKey
            {
                public string Kept { get; init; } = "";

                [Enver.SourceGeneration.EnverIgnore]
                [Enver.SourceGeneration.EnverKey("UNUSED")]
                public string Dropped { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0008"));
    }

    [Test]
    public void ReportsEnvr0013WhenTypeIsUtf8ParsableButNotIParsable()
    {
        // Utf8Only implements IUtf8SpanParsable<T> but neither IParsable<T> nor
        // ISpanParsable<T>, so it can't be bound from the string-reader path.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            public readonly struct Utf8Only : System.IUtf8SpanParsable<Utf8Only>
            {
                public static Utf8Only Parse(System.ReadOnlySpan<byte> utf8Text, System.IFormatProvider? provider) => default;
                public static bool TryParse(System.ReadOnlySpan<byte> utf8Text, System.IFormatProvider? provider, out Utf8Only result)
                {
                    result = default;
                    return true;
                }
            }

            [Enver.SourceGeneration.EnverBindable]
            public partial class HasUtf8Only
            {
                public string Name { get; init; } = "";
                public Utf8Only Value { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0013"));
    }

    [Test]
    public void ReportsEnvr0003WhenEnverUriOnNonUriMember()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class BadUri
            {
                [Enver.SourceGeneration.EnverUri(System.UriKind.Relative)]
                public int Port { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0003"));
    }

    [Test]
    public void ReportsEnvr0004WhenFormatProviderMemberInvalid()
    {
        // string.Empty exists and is static, but string does not implement
        // IFormatProvider, so it can't supply one.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverFormatProvider(typeof(string), "Empty")]
            public partial class BadFp
            {
                public int Count { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0004"));
    }

    [Test]
    public void ReportsEnvr0014WhenFormatProviderHasNoEffect()
    {
        // Guid is parsed culture-invariantly, so a member-level
        // [EnverFormatProvider] on a Guid member has no effect.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class GuidFp
            {
                [Enver.SourceGeneration.EnverFormatProvider(typeof(System.Globalization.CultureInfo), "InvariantCulture")]
                public System.Guid Id { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0014"));
    }

    [Test]
    public void ReportsEnvr0006WhenOptionalNonNullableHasNoInitializer()
    {
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class OptionalConfig
            {
                [Enver.SourceGeneration.EnverKey(Required = Enver.SourceGeneration.EnverRequirementBehavior.Optional)]
                public int Count { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0006"));
    }

    [Test]
    public void ReportsEnvr0007WhenPrefixCasingMismatchesKeyNaming()
    {
        // Prefix "Db" is used literally; with UpperSnakeCase keys it yields
        // the inconsistently-cased "Db_HOST".
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig("Db", KeyNaming = Enver.SourceGeneration.EnverKeyNamingConvention.UpperSnakeCase)]
            public partial record MixedPrefix(string Host);
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0007"));
    }

    [Test]
    public void ReportsEnvr0015WhenKeyMemberInaccessibleFromHost()
    {
        // Secret is explicitly opted in with [EnverKey], but its setter is
        // private to DbConfig.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            public class DbConfig
            {
                public string Host { get; init; } = "";

                [Enver.SourceGeneration.EnverKey("SECRET")]
                private string Secret { get; init; } = "";
            }

            [Enver.SourceGeneration.EnverBindable<DbConfig>]
            public partial class AppHost;
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0015"));
    }

    // --- Subsection support ---

    [Test]
    public void EmitsSubSectionFlatFieldsAndOnNextCases()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Host
            {
                public required string Name { get; init; }
                public required Sub Nested { get; init; }
            }

            public class Sub
            {
                [Enver.SourceGeneration.EnverKey("PORT")]
                public required int Port { get; init; }

                [Enver.SourceGeneration.EnverKey("HOST")]
                public required string Addr { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            // Subsection fields are prefixed with the outer member name.
            Assert.That(src, Does.Contain("_val_Nested_Port"));
            Assert.That(src, Does.Contain("_val_Nested_Addr"));
            Assert.That(src, Does.Contain("_set_Nested_Port"));
            Assert.That(src, Does.Contain("_set_Nested_Addr"));
            // OnNext routes subsection keys to the prefixed fields.
            Assert.That(src, Does.Contain("\"PORT\"u8"));
            Assert.That(src, Does.Contain("\"HOST\"u8"));
            // Build() checks required sub-members, builds a local, assigns it.
            Assert.That(src, Does.Contain("_set_Nested_Port"));
            Assert.That(src, Does.Contain("var _built_Nested"));
            Assert.That(src, Does.Contain("Nested = _built_Nested"));
            // Bind(IEnvReader) inlines a new Sub() expression.
            Assert.That(src, Does.Contain("new global::Test.Sub()"));
        }
    }

    [Test]
    public void SubSectionWithIgnorePrefixKeyBindsCorrectly()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig("APP")]
            public partial class Config
            {
                public required string Version { get; init; }
                public required DbSection Db { get; init; }
            }

            public class DbSection
            {
                [Enver.SourceGeneration.EnverKey("DB_HOST")]
                public required string Host { get; init; }

                [Enver.SourceGeneration.EnverKey("ABSOLUTE_KEY", IgnorePrefix = true)]
                public required string Absolute { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("\"DB_HOST\"u8"));
            Assert.That(src, Does.Contain("\"ABSOLUTE_KEY\"u8"));
            Assert.That(src, Does.Contain("_val_Db_Host"));
            Assert.That(src, Does.Contain("_val_Db_Absolute"));
        }
    }

    [Test]
    public void SubSectionDetectedViaEnverConfigAttribute()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Root
            {
                public required Sub Section { get; init; }
            }

            [Enver.SourceGeneration.EnverConfig]
            public class Sub
            {
                public required string Val { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        Assert.That(src, Does.Contain("_val_Section_Val"));
    }

    [Test]
    public void SubSectionInheritsKeyNamingFromParent()
    {
        // The parent uses SnakeCase. The subsection type has no [EnverConfig],
        // so KeyNaming = Inherit. Its members should be keyed using the parent's
        // SnakeCase convention, not the UpperSnakeCase default.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(KeyNaming = Enver.SourceGeneration.EnverKeyNamingConvention.SnakeCase)]
            public partial class Config
            {
                public required Sub Database { get; init; }
            }

            public class Sub
            {
                [Enver.SourceGeneration.EnverKey("db_host")]
                public required string HostName { get; init; }

                public required int MaxConnections { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            // MaxConnections has no [EnverKey], so the name is transformed.
            // With inherited SnakeCase it becomes "max_connections", not "MAX_CONNECTIONS".
            Assert.That(src, Does.Contain("\"max_connections\"u8"));
            Assert.That(src, Does.Not.Contain("\"MAX_CONNECTIONS\"u8"));
        }
    }

    [Test]
    public void SubSectionWithExplicitKeyNamingOverridesInheritance()
    {
        // The parent uses SnakeCase but the subsection declares its own UpperSnakeCase.
        // The subsection's explicit convention must win.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(KeyNaming = Enver.SourceGeneration.EnverKeyNamingConvention.SnakeCase)]
            public partial class Config
            {
                public required Sub Database { get; init; }
            }

            [Enver.SourceGeneration.EnverConfig(KeyNaming = Enver.SourceGeneration.EnverKeyNamingConvention.UpperSnakeCase)]
            public class Sub
            {
                public required int MaxConnections { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("\"MAX_CONNECTIONS\"u8"));
            Assert.That(src, Does.Not.Contain("\"max_connections\"u8"));
        }
    }

    // --- ENVR0016 / ENVR0017 diagnostics ---

    [Test]
    public void ReportsEnvr0016WhenKeyOnGetterOnlyProperty()
    {
        // [EnverKey] on a property with no setter can never be bound.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string Name { get; init; } = "";

                [Enver.SourceGeneration.EnverKey("COMPUTED")]
                public string Computed => "constant";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0016"));
    }

    [Test]
    public void ReportsEnvr0017WhenKeyNameSpecifiedOnSubSectionProperty()
    {
        // [EnverKey] with a name override on a subsection property has no effect
        // because subsections don't use a key name for dispatch.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string AppName { get; init; } = "";

                [Enver.SourceGeneration.EnverKey("IGNORED_NAME")]
                public Sub Database { get; init; } = new();
            }

            [Enver.SourceGeneration.EnverConfig("DB")]
            public class Sub
            {
                public string Host { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0017"));
    }

    [Test]
    public void ReportsEnvr0017WhenIgnorePrefixSpecifiedOnSubSectionProperty()
    {
        // [EnverKey(IgnorePrefix = true)] on a subsection property also has no effect.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig("APP")]
            public partial class Config
            {
                [Enver.SourceGeneration.EnverKey(IgnorePrefix = true)]
                public Sub Database { get; init; } = new();
            }

            [Enver.SourceGeneration.EnverConfig("DB")]
            public class Sub
            {
                public string Host { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0017"));
    }

    [Test]
    public void ReportsEnvr0017WhenEnverKeyWithOnlyRequiredOnSubSectionProperty()
    {
        // [EnverKey] is now entirely disallowed on subsection properties,
        // even if only Required is set.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string AppName { get; init; } = "";

                [Enver.SourceGeneration.EnverKey(Required = Enver.SourceGeneration.EnverRequirementBehavior.Required)]
                public Sub Database { get; init; } = new();
            }

            [Enver.SourceGeneration.EnverConfig("DB")]
            public class Sub
            {
                public string Host { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0017"));
    }

    // --- [EnverSubsection] attribute ---

    [Test]
    public void EnverSubsectionOnTypeMarksCandidateWithoutOtherMarkers()
    {
        // A type with only [EnverSubsection] (no [EnverConfig], no [EnverKey] on members)
        // is detected as a subsection candidate.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string AppName { get; init; } = "";
                public Sub Database { get; init; } = new();
            }

            [Enver.SourceGeneration.EnverSubsection]
            public class Sub
            {
                public string Host { get; init; } = "";
                public int Port { get; init; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("_val_Database_Host"));
            Assert.That(src, Does.Contain("_val_Database_Port"));
            Assert.That(src, Does.Contain("\"HOST\"u8"));
            Assert.That(src, Does.Contain("\"PORT\"u8"));
        }
    }

    [Test]
    public void EnverSubsectionOnPropertyExplicitlyOptsInWithoutTypeMarker()
    {
        // [EnverSubsection] on the property binds it as a subsection even though
        // the type has no [EnverConfig], [EnverSubsection], or [EnverKey] markers.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string AppName { get; init; } = "";

                [Enver.SourceGeneration.EnverSubsection]
                public Sub Database { get; init; } = new();
            }

            public class Sub
            {
                public string Host { get; init; } = "";
            }
            """
        );

        var src = result.SingleSource().Text;
        Assert.That(src, Does.Contain("_val_Database_Host"));
    }

    [Test]
    public void EnverSubsectionRequiredOnPropertyControlsRequirement()
    {
        // [EnverSubsection(Required = ...)] is the valid way to control requirement on a
        // subsection property. No ENVR0017 should fire.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            public partial class Config
            {
                public string AppName { get; init; } = "";

                [Enver.SourceGeneration.EnverSubsection(Required = Enver.SourceGeneration.EnverRequirementBehavior.Required)]
                public Sub Database { get; init; } = new();
            }

            [Enver.SourceGeneration.EnverConfig("DB")]
            public class Sub
            {
                public string Host { get; init; } = "";
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Not.Contain("ENVR0017"));
    }

    // --- GeneratePopulate ---

    [Test]
    public void EmitsPopulateMethodsWhenGeneratePopulateIsTrue()
    {
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(GeneratePopulate = true)]
            public partial class Config
            {
                public string Name { get; set; } = "";
                public int Count { get; set; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            // Static string-path populate method
            Assert.That(src, Does.Contain("public static void Populate("));
            Assert.That(
                src,
                Does.Contain("global::Test.Config instance, global::Enver.IEnvReader reader)")
            );
            // Binder instance populate method
            Assert.That(src, Does.Contain("public void Populate(global::Test.Config instance)"));
            // Both mutable members should appear in populate
            Assert.That(src, Does.Contain("instance.Name ="));
            Assert.That(src, Does.Contain("instance.Count ="));
        }
    }

    [Test]
    public void PopulateSkipsInitOnlyMembersAndEmitsEnvr0018()
    {
        // init-only members are skipped in Populate; ENVR0018 fires for each.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(GeneratePopulate = true)]
            public partial class Config
            {
                public string Mutable { get; set; } = "";
                public int ReadOnly { get; init; }
            }
            """
        );

        var diagnosticIds = result.GeneratorDiagnostics.Select(d => d.Id).ToList();
        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(diagnosticIds, Does.Contain("ENVR0018"));
            // Mutable member appears in Populate; init-only does not.
            Assert.That(src, Does.Contain("instance.Mutable ="));
            Assert.That(src, Does.Not.Contain("instance.ReadOnly ="));
        }
    }

    [Test]
    public void ReportsEnvr0019WhenAllMembersAreInitOnlyWithGeneratePopulate()
    {
        // Every member is init-only: no Populate can be generated; ENVR0019 fires.
        var result = GeneratorTestHarness.Run(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(GeneratePopulate = true)]
            public partial class Config
            {
                public string Name { get; init; } = "";
                public int Count { get; init; }
            }
            """
        );

        Assert.That(result.GeneratorDiagnostics.Select(d => d.Id), Does.Contain("ENVR0019"));
    }

    [Test]
    public void PopulateBinderMethodAssignsOnlyWhenValueWasSeen()
    {
        // The Binder's Populate uses _set_* guards so only parsed values are assigned.
        var result = GeneratorTestHarness.RunExpectingSuccess(
            """
            namespace Test;

            [Enver.SourceGeneration.EnverBindable]
            [Enver.SourceGeneration.EnverConfig(GeneratePopulate = true)]
            public partial class Config
            {
                public string Name { get; set; } = "";
                public int Count { get; set; }
            }
            """
        );

        var src = result.SingleSource().Text;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(src, Does.Contain("if (_set_Name) instance.Name ="));
            Assert.That(src, Does.Contain("if (_set_Count) instance.Count ="));
        }
    }
}
