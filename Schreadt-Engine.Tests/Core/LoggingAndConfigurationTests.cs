using Schreadt_Engine.Core;

namespace Schreadt_Engine.Tests.Core;

[Collection("Engine lifecycle")]
public sealed class LoggingAndConfigurationTests
{
    [Fact]
    public void ConfigParser_LoadsValidatedConfiguration()
    {
        const string json = """
            {
              "window": {
                "title": " Test Game ",
                "default-size": { "width": 1280, "height": 720 }
              },
              "assetLibraries": ["default", "gameplay"]
            }
            """;

        var config = Config.Parse(json, "test-config.json");

        Assert.Equal("Test Game", config.Window.Title);
        Assert.Equal(1280, config.Window.DefaultSize.Width);
        Assert.Equal(720, config.Window.DefaultSize.Height);
        Assert.Equal(["default", "gameplay"], config.AssetLibraries);
    }

    [Fact]
    public void ConfigParser_AggregatesPathSpecificValidationProblems()
    {
        const string json = """
            {
              "windwo": {},
              "window": {
                "title": "  ",
                "default-size": { "width": 0 }
              },
              "assetLibraries": ["default", "DEFAULT", "folder/library", "extra.json", 4]
            }
            """;

        var exception = Assert.Throws<GameConfigurationException>(() =>
            Config.Parse(json, "broken-config.json"));

        Assert.Equal("broken-config.json", exception.SourcePath);
        Assert.Contains(exception.Problems, problem => problem.Contains("windwo is not recognized"));
        Assert.Contains(exception.Problems, problem => problem.Contains("window.title must not be empty"));
        Assert.Contains(exception.Problems, problem => problem.Contains("window.default-size.width must be greater"));
        Assert.Contains(exception.Problems, problem => problem.Contains("window.default-size.height is required"));
        Assert.Contains(exception.Problems, problem => problem.Contains("duplicates asset library"));
        Assert.Contains(exception.Problems, problem => problem.Contains("without directory separators"));
        Assert.Contains(exception.Problems, problem => problem.Contains("omit the '.json' extension"));
        Assert.Contains(exception.Problems, problem => problem.Contains("assetLibraries[4] must be a string"));
        Assert.Contains("\n - ", exception.Message);
    }

    [Fact]
    public void ConfigParser_ReportsJsonLineAndPosition()
    {
        var exception = Assert.Throws<GameConfigurationException>(() =>
            Config.Parse("{\n  \"window\": ]\n}", "syntax.json"));

        Assert.Single(exception.Problems);
        Assert.Contains("JSON syntax error at line", exception.Problems[0]);
        Assert.Contains("position", exception.Problems[0]);
    }

    [Fact]
    public void ConfigParser_RejectsNonObjectRootAndUnknownNestedProperties()
    {
        var rootException = Assert.Throws<GameConfigurationException>(() =>
            Config.Parse("[]", "array.json"));
        Assert.Contains("root value must be a JSON object", rootException.Message);

        const string unknownPropertyJson = """
            {
              "window": {
                "title": "Game",
                "default-size": { "width": 800, "height": 600, "heigth": 600 }
              }
            }
            """;
        var propertyException = Assert.Throws<GameConfigurationException>(() =>
            Config.Parse(unknownPropertyJson));
        Assert.Contains("window.default-size.heigth is not recognized", propertyException.Message);
    }

    [Fact]
    public void EngineLog_WritesStructuredEntriesAndFullExceptionsToFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"schreadt-log-test-{Guid.NewGuid():N}");
        string? logPath = null;
        try
        {
            EngineLog.Shutdown();
            EngineLog.Initialize(directory);
            logPath = EngineLog.CurrentLogFilePath;
            EngineLog.Error("Asset decode failed.", new InvalidOperationException("bad pixels"), "Assets");
            EngineLog.Shutdown();

            Assert.NotNull(logPath);
            var contents = File.ReadAllText(logPath!);
            Assert.Contains("[ERROR] [Assets] Asset decode failed.", contents);
            Assert.Contains("System.InvalidOperationException: bad pixels", contents);
            Assert.Contains("[INFORMATION] [Engine] Logging initialized.", contents);
            Assert.Contains("Minimum level:", contents);
            Assert.Contains($"File: {logPath}.", contents);
            Assert.Contains("[INFORMATION] [Environment] Runtime:", contents);
            Assert.Contains("[INFORMATION] [Logging] Logging is shutting down.", contents);
        }
        finally
        {
            EngineLog.Shutdown();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EngineLog_RespectsMinimumLevelAndPublishesEntries()
    {
        var previousLevel = EngineLog.MinimumLevel;
        var entries = new List<EngineLogEntry>();
        void Capture(EngineLogEntry entry) => entries.Add(entry);
        EngineLog.EntryWritten += Capture;
        try
        {
            EngineLog.MinimumLevel = EngineLogLevel.Warning;
            EngineLog.Information("hidden");
            EngineLog.Warning("visible", "Test");

            var entry = Assert.Single(entries);
            Assert.Equal(EngineLogLevel.Warning, entry.Level);
            Assert.Equal("visible", entry.Message);
            Assert.Equal("Test", entry.Category);
        }
        finally
        {
            EngineLog.EntryWritten -= Capture;
            EngineLog.MinimumLevel = previousLevel;
        }
    }

    [Fact]
    public void FatalErrorMessage_IsConciseAndPointsToTechnicalLog()
    {
        var exception = new InvalidOperationException("Renderer initialization failed.");

        var message = FatalErrorPresenter.CreateMessage(exception, @"C:\Game\logs\fatal.log");

        Assert.Contains("must close", message);
        Assert.Contains("Renderer initialization failed.", message);
        Assert.Contains(@"C:\Game\logs\fatal.log", message);
        Assert.DoesNotContain(nameof(LoggingAndConfigurationTests), message);
    }
}
