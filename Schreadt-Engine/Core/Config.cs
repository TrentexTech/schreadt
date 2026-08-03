using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Schreadt_Engine.Core;

public sealed class GameConfigurationException : Exception
{
    public string SourcePath { get; }

    public IReadOnlyList<string> Problems { get; }

    internal GameConfigurationException(string sourcePath, IEnumerable<string> problems, Exception? innerException = null)
        : base(CreateMessage(sourcePath, problems), innerException)
    {
        SourcePath = sourcePath;
        Problems = problems.ToArray();
    }

    private static string CreateMessage(string sourcePath, IEnumerable<string> problems)
    {
        var problemList = problems.ToArray();
        var builder = new StringBuilder($"Game configuration '{sourcePath}' is invalid:");
        foreach (var problem in problemList) builder.Append("\n - ").Append(problem);
        return builder.ToString();
    }
}

internal static class Config
{
    internal static ConfigData Data { get; private set; } = null!;

    internal static void Load()
    {
        var path = FileHandler.GetPath(FileType.GameConfig);
        var configString = FileHandler.ReadFileAsString(FileType.GameConfig);
        Data = Parse(configString, path);
        EngineLog.Information(
            $"Configuration loaded: {Data.Window.DefaultSize.Width}x{Data.Window.DefaultSize.Height}, " +
            $"{Data.AssetLibraries.Count} asset library/libraries.",
            "Configuration");
    }

    internal static ConfigData Parse(string configString, string sourcePath = "config/config.json")
    {
        ArgumentNullException.ThrowIfNull(configString);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        JToken rootToken;
        try
        {
            rootToken = JToken.Parse(configString);
        }
        catch (JsonReaderException exception)
        {
            var location = exception.LineNumber > 0
                ? $" at line {exception.LineNumber}, position {exception.LinePosition}"
                : string.Empty;
            throw new GameConfigurationException(
                sourcePath,
                [$"JSON syntax error{location}: {exception.Message}"],
                exception);
        }

        if (rootToken is not JObject root)
            throw new GameConfigurationException(sourcePath, ["The root value must be a JSON object."]);

        var problems = new List<string>();
        ValidateKnownProperties(root, string.Empty, ["window", "assetLibraries"], problems);

        var windowObject = ReadRequiredObject(root, "window", "window", problems);
        var title = "Schreadt Game";
        var width = 0;
        var height = 0;
        if (windowObject is not null)
        {
            ValidateKnownProperties(windowObject, "window", ["title", "default-size"], problems);
            title = ReadRequiredString(windowObject, "title", "window.title", problems) ?? title;
            if (title.Length > 256) problems.Add("window.title must contain at most 256 characters.");

            var sizeObject = ReadRequiredObject(windowObject, "default-size", "window.default-size", problems);
            if (sizeObject is not null)
            {
                ValidateKnownProperties(sizeObject, "window.default-size", ["width", "height"], problems);
                var configuredWidth = ReadRequiredInteger(
                    sizeObject, "width", "window.default-size.width", problems);
                var configuredHeight = ReadRequiredInteger(
                    sizeObject, "height", "window.default-size.height", problems);
                if (configuredWidth.HasValue)
                {
                    width = configuredWidth.Value;
                    ValidateWindowDimension(width, "window.default-size.width", problems);
                }

                if (configuredHeight.HasValue)
                {
                    height = configuredHeight.Value;
                    ValidateWindowDimension(height, "window.default-size.height", problems);
                }
            }
        }

        var assetLibraries = ReadAssetLibraries(root, problems);
        if (problems.Count > 0) throw new GameConfigurationException(sourcePath, problems);

        return new ConfigData(
            new WindowConfigData(title, new WindowSizeConfigData(width, height)),
            assetLibraries.AsReadOnly());
    }

    private static List<string> ReadAssetLibraries(JObject root, List<string> problems)
    {
        if (!root.TryGetValue("assetLibraries", StringComparison.Ordinal, out var token)) return [];
        if (token is not JArray array)
        {
            problems.Add("assetLibraries must be an array of manifest names.");
            return [];
        }

        var libraries = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < array.Count; index++)
        {
            var path = $"assetLibraries[{index}]";
            if (array[index]?.Type != JTokenType.String)
            {
                problems.Add($"{path} must be a string.");
                continue;
            }

            var name = array[index]!.Value<string>()!.Trim();
            if (name.Length == 0)
            {
                problems.Add($"{path} must not be empty.");
                continue;
            }

            if (Path.GetFileName(name) != name)
                problems.Add($"{path} must be a manifest name without directory separators.");
            if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                problems.Add($"{path} must omit the '.json' extension.");
            if (!names.Add(name)) problems.Add($"{path} duplicates asset library '{name}'.");
            libraries.Add(name);
        }

        return libraries;
    }

    private static JObject? ReadRequiredObject(
        JObject parent,
        string propertyName,
        string path,
        List<string> problems)
    {
        if (!parent.TryGetValue(propertyName, StringComparison.Ordinal, out var token))
        {
            problems.Add($"{path} is required.");
            return null;
        }

        if (token is JObject value) return value;
        problems.Add($"{path} must be an object.");
        return null;
    }

    private static string? ReadRequiredString(
        JObject parent,
        string propertyName,
        string path,
        List<string> problems)
    {
        if (!parent.TryGetValue(propertyName, StringComparison.Ordinal, out var token))
        {
            problems.Add($"{path} is required.");
            return null;
        }

        if (token.Type != JTokenType.String)
        {
            problems.Add($"{path} must be a string.");
            return null;
        }

        var value = token.Value<string>()!.Trim();
        if (value.Length > 0) return value;
        problems.Add($"{path} must not be empty.");
        return null;
    }

    private static int? ReadRequiredInteger(
        JObject parent,
        string propertyName,
        string path,
        List<string> problems)
    {
        if (!parent.TryGetValue(propertyName, StringComparison.Ordinal, out var token))
        {
            problems.Add($"{path} is required.");
            return null;
        }

        if (token.Type == JTokenType.Integer && token.Value<long>() is >= int.MinValue and <= int.MaxValue)
            return token.Value<int>();

        problems.Add($"{path} must be an integer.");
        return null;
    }

    private static void ValidateWindowDimension(int value, string path, List<string> problems)
    {
        if (value <= 0) problems.Add($"{path} must be greater than zero.");
        else if (value > 16384) problems.Add($"{path} must not exceed 16384.");
    }

    private static void ValidateKnownProperties(
        JObject value,
        string path,
        IReadOnlyList<string> knownProperties,
        List<string> problems)
    {
        foreach (var property in value.Properties())
        {
            if (knownProperties.Contains(property.Name)) continue;
            var propertyPath = path.Length == 0 ? property.Name : $"{path}.{property.Name}";
            problems.Add($"{propertyPath} is not recognized. Check for a spelling mistake.");
        }
    }
}

internal sealed record ConfigData(
    WindowConfigData Window,
    IReadOnlyList<string> AssetLibraries);

internal sealed record WindowConfigData(
    string Title,
    WindowSizeConfigData DefaultSize);

internal readonly record struct WindowSizeConfigData(int Width, int Height);
