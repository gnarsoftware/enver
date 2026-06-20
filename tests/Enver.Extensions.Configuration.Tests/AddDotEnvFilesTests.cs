using Enver.Extensions.Configuration;
using Microsoft.Extensions.Configuration;

namespace Enver.Tests;

public class AddDotEnvFilesTests
{
    private string _tempDir = null!;
    private string _originalCwd = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Directory.CreateTempSubdirectory("enver-config-files-").FullName;
        // AddDotEnvFiles uses Environment.CurrentDirectory to resolve the
        // bare ".env" filename, so the test must chdir into the fixture.
        _originalCwd = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalCwd);
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private void WriteFile(string fileName, string contents)
    {
        File.WriteAllText(Path.Combine(_tempDir, fileName), contents);
    }

    [Test]
    public void LoadsBaseFileOnlyWhenEnvSpecificDoesNotExist()
    {
        WriteFile(".env", "KEY=from-base\n");
        var config = new ConfigurationManager();
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-base"));
    }

    [Test]
    public void EnvSpecificFileOverridesBaseFile()
    {
        // Mirrors appsettings.{Environment}.json overriding appsettings.json.
        // The env-specific source is added second, so it wins under IConfiguration's
        // last-source-wins semantics.
        WriteFile(".env", "KEY=from-base\n");
        WriteFile(".env.development", "KEY=from-development\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-development"));
    }

    [Test]
    public void EnvSpecificFileExtendsBaseFile()
    {
        // Keys only present in the base file remain visible after the
        // env-specific layer is applied.
        WriteFile(".env", "BASE_KEY=base-value\n");
        WriteFile(".env.development", "DEV_KEY=dev-value\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["BASE_KEY"], Is.EqualTo("base-value"));
            Assert.That(config["DEV_KEY"], Is.EqualTo("dev-value"));
        }
    }

    [Test]
    [TestCase("environment")]
    [TestCase("ASPNETCORE_ENVIRONMENT")]
    [TestCase("DOTNET_ENVIRONMENT")]
    public void EnvironmentIsAutoDiscoveredFromConfiguration(string envKey)
    {
        WriteFile(".env.staging", "KEY=staging\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([new KeyValuePair<string, string?>(envKey, "Staging")]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("staging"));
    }

    [Test]
    public void EnvironmentKeyWinsOverPrefixedKeys()
    {
        // "environment" is HostDefaults.EnvironmentKey -- the single key the host
        // resolves IHostEnvironment.EnvironmentName from, after the prefixed env
        // vars are stripped into it. It holds the winning value (including a
        // command-line --environment override), so Enver honors it first.
        WriteFile(".env.staging", "KEY=from-environment\n");
        WriteFile(".env.development", "KEY=from-aspnetcore\n");
        WriteFile(".env.production", "KEY=from-dotnet\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("environment", "Staging"),
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
            new KeyValuePair<string, string?>("DOTNET_ENVIRONMENT", "Production"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-environment"));
    }

    [Test]
    public void AspNetCoreEnvironmentWinsOverDotNetEnvironment()
    {
        // When the stripped "environment" key is absent, ASPNETCORE_ENVIRONMENT
        // takes priority over DOTNET_ENVIRONMENT, matching ASP.NET Core: its
        // prefixed source is added after the generic host's DOTNET_ one.
        WriteFile(".env.development", "KEY=from-aspnetcore\n");
        WriteFile(".env.production", "KEY=from-dotnet\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
            new KeyValuePair<string, string?>("DOTNET_ENVIRONMENT", "Production"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-aspnetcore"));
    }

    [Test]
    public void DefaultsToProductionWhenNoEnvironmentVariableIsSet()
    {
        // ASP.NET Core's documented default is "Production" when none of the
        // discovered keys (environment, ASPNETCORE_ENVIRONMENT, DOTNET_ENVIRONMENT)
        // is set.
        WriteFile(".env.production", "KEY=prod\n");
        var config = new ConfigurationManager();
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("prod"));
    }

    [Test]
    public void ExplicitPathsOverloadOverridesConventionAutoDiscovery()
    {
        WriteFile(".env.custom", "KEY=custom\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Staging"),
        ]);
        config.AddDotEnvFiles(DotEnvPaths.Relative().Standard("custom"));
        Assert.That(config["KEY"], Is.EqualTo("custom"));
    }

    [Test]
    public void MissingBaseAndVariantFilesProduceEmptyConfig()
    {
        var config = new ConfigurationManager();
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config.AsEnumerable().Where(kv => kv.Value is not null), Is.Empty);
    }

    [Test]
    public void LowercasesEnvironmentNameForVariantFilename()
    {
        // Auto-discovered ASPNETCORE_ENVIRONMENT is conventionally PascalCase
        // ("Development", "Staging", "Production"), but the .env ecosystem
        // convention is lowercase filenames. Verify the discovery path
        // lowercases regardless of the source casing.
        WriteFile(".env.staging", "KEY=lowercased\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Staging"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("lowercased"));
    }

    [Test]
    public void EnvironmentVariablesOverrideDotEnvFiles()
    {
        // The whole point of inserting .env sources at the config-file tier:
        // a value set in the process env still beats the .env value, matching
        // the universal dotenv-ecosystem precedence rule and the user's
        // expectation that deployment-platform env vars are authoritative.
        const string testKey = "ENVER_COMPAT_TEST_PRECEDENCE_KEY";
        Environment.SetEnvironmentVariable(testKey, "from-env-var");
        try
        {
            WriteFile(".env", $"{testKey}=from-dotenv\n");
            var config = new ConfigurationManager();
            config.AddEnvironmentVariables();
            config.AddDotEnvFiles(s => s.ReloadOnChange = false);
            Assert.That(config[testKey], Is.EqualTo("from-env-var"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    [Test]
    public void AppendsToEndOfListWhenNoEnvironmentVariablesSourceIsRegistered()
    {
        WriteFile(".env", "KEY=from-dotenv\n");
        var config = new ConfigurationManager();
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        config.AddInMemoryCollection([new KeyValuePair<string, string?>("KEY", "from-memory")]);
        Assert.That(config["KEY"], Is.EqualTo("from-memory"));
    }

    // --- .env.local + .env.{env}.local (framework convention) ---

    [Test]
    public void DotEnvLocalOverridesEnvironmentVariantFile()
    {
        // Per the framework convention (CRA / Next.js / Vite / dotenv-flow):
        // .env.local is the per-machine override layer and wins over the
        // environment-shared variant file.
        WriteFile(".env", "KEY=from-base\n");
        WriteFile(".env.development", "KEY=from-env-variant\n");
        WriteFile(".env.local", "KEY=from-local\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-local"));
    }

    [Test]
    public void DotEnvEnvironmentLocalIsTheHighestDotEnvLayer()
    {
        // The four-tier ladder ends at .env.{environment}.local
        WriteFile(".env", "KEY=from-base\n");
        WriteFile(".env.development", "KEY=from-env-variant\n");
        WriteFile(".env.local", "KEY=from-local\n");
        WriteFile(".env.development.local", "KEY=from-env-variant-local\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["KEY"], Is.EqualTo("from-env-variant-local"));
    }

    [Test]
    public void LocalLayersAreSkippedSilentlyWhenAbsent()
    {
        // Only the deployed tier files exist. AddDotEnvFiles must not throw.
        WriteFile(".env", "BASE_KEY=base\n");
        WriteFile(".env.development", "DEV_KEY=dev\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(config["BASE_KEY"], Is.EqualTo("base"));
            Assert.That(config["DEV_KEY"], Is.EqualTo("dev"));
        }
    }

    [Test]
    public void EnvironmentVariablesOverrideEvenDotEnvEnvironmentLocal()
    {
        // The .env / .env.local layers stay below env vars
        // in the source list. Deployment-platform env vars
        // win over even the most-specific .env file.
        const string testKey = "ENVER_COMPAT_TEST_FULL_LADDER_KEY";
        Environment.SetEnvironmentVariable(testKey, "from-env-var");
        try
        {
            WriteFile(".env.development.local", $"{testKey}=from-env-variant-local\n");
            var config = new ConfigurationManager();
            config.AddInMemoryCollection([
                new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
            ]);
            config.AddEnvironmentVariables();
            config.AddDotEnvFiles(s => s.ReloadOnChange = false);
            Assert.That(config[testKey], Is.EqualTo("from-env-var"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(testKey, null);
        }
    }

    // --- Cross-file interpolation ---
    //
    // The ladder loads all four files into a single shared EnvCollection so
    // ${VAR} references in later files resolve against values defined in
    // earlier files. Same semantic as EnvFileLoader in the directory walker.

    [Test]
    public void EnvironmentVariantFileInterpolatesAgainstBaseFile()
    {
        WriteFile(".env", "NAME=Alice\n");
        WriteFile(".env.development", "GREETING=Hello, ${NAME}\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["GREETING"], Is.EqualTo("Hello, Alice"));
    }

    [Test]
    public void LocalFileInterpolatesAgainstEnvironmentVariantFile()
    {
        // Proves chain order: .env.local sees values defined in
        // .env.{environment}, not just .env.
        WriteFile(".env", "HOST=fallback.example\n");
        WriteFile(".env.development", "HOST=dev.example\n");
        WriteFile(".env.local", "API_URL=https://${HOST}/api\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        // .env.{env} ran before .env.local, so HOST=dev.example wins
        // for the interpolation.
        Assert.That(config["API_URL"], Is.EqualTo("https://dev.example/api"));
    }

    [Test]
    public void EnvironmentVariantLocalInterpolatesAcrossAllEarlierLayers()
    {
        // The most-specific layer can reference values from any earlier
        // layer in one composed interpolation.
        WriteFile(".env", "PROTO=https\n");
        WriteFile(".env.development", "HOST=dev.example\n");
        WriteFile(".env.local", "PORT=8443\n");
        WriteFile(".env.development.local", "API_URL=${PROTO}://${HOST}:${PORT}/api\n");
        var config = new ConfigurationManager();
        config.AddInMemoryCollection([
            new KeyValuePair<string, string?>("ASPNETCORE_ENVIRONMENT", "Development"),
        ]);
        config.AddDotEnvFiles(s => s.ReloadOnChange = false);
        Assert.That(config["API_URL"], Is.EqualTo("https://dev.example:8443/api"));
    }

    [Test]
    public void CrossFileInterpolationFailsWhenReferenceIsForwardInLadder()
    {
        // The ladder loads .env first, so a ${VAR} reference in .env that
        // points at a key defined in .env.local cannot resolve.
        WriteFile(".env", "GREETING=Hello, ${NAME}\n");
        WriteFile(".env.local", "NAME=Alice\n");
        var config = new ConfigurationManager();
        Assert.Throws<InvalidDataException>(() =>
            config.AddDotEnvFiles(s => s.ReloadOnChange = false)
        );
    }
}
