using Newtonsoft.Json;

namespace Schreadt_Engine.Core;

internal static class Config
{
    internal static ConfigData Data { get; private set; }

    internal static void Load()
    {
        var configString = FileHandler.ReadFileAsString(FileType.GameConfig);
        var data = JsonConvert.DeserializeObject<ConfigData?>(configString);

        if (data is null) throw new Exception("Game config file could not be loaded!");

        Data = data.Value;
    }
}

internal struct ConfigData
{
    [JsonProperty("window")]
    public WindowConfigData Window { get; set; }

    [JsonProperty("assetLibraries")]
    public string[] AssetLibraries { get; set; }
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