using Newtonsoft.Json;

namespace Schreadt_Engine.Core;

internal static class Config
{
    internal static ConfigData Data { get; private set; }

    internal static void Load()
    {
        var configString = FileHandler.ReadFileAsString(FileType.GameConfig);
        ConfigData? data;
        try
        {
            data = JsonConvert.DeserializeObject<ConfigData?>(configString);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Game config contains invalid JSON.", exception);
        }

        if (data is null) throw new InvalidDataException("Game config is empty.");

        Data = data.Value;
    }
}

internal struct ConfigData
{
    [JsonProperty("window")]
    public WindowConfigData Window { get; set; }

    [JsonProperty("assetLibraries")]
    public string[]? AssetLibraries { get; set; }
}

internal struct WindowConfigData
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("default-size")]
    public WindowSizeConfigData DefaultSize { get; set; }
}

internal struct WindowSizeConfigData
{
    [JsonProperty("width")]
    public int Width { get; set; }

    [JsonProperty("height")]
    public int Height { get; set; }
}
