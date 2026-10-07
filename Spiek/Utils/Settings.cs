using System.Text.Json;

namespace Spiek.Utils
{
    public sealed class Settings
    {

        private static readonly Lazy<Settings> _instance =
            new(() => Load());

        private static readonly string SettingsPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "settings.json");

        public static Settings Instance => _instance.Value;

        public string DropboxPath { get; set; } = "none";

        public string SrcpyLaunchArguments { get; set; } =
            "--stay-awake --keep-active --no-audio -b 6M " +
            "--crop %width%:%height%:%x%:%y% " +
            "--video-codec=h264 --max-size=400 --max-fps=20 " +
            "--window-title \"Quest\" -s %addr%";

        public List<DeviceInfo> Devices { get; set; } = new()
        {
            new DeviceInfo()
        };

        public Settings()
        {
        }

        private static Settings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    var settings = new Settings();
                    settings.Save();
                    return settings;
                }

                var json = File.ReadAllText(SettingsPath);

                return JsonSerializer.Deserialize<Settings>(json)
                       ?? new Settings();
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "Failed to load settings.json",
                    ex);
            }
        }

        public void Save()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
    }

    public class DeviceInfo
    {
        public string DeviceId { get; set; } = "example";
        public string Name { get; set; } = "The Exampler";
        public string HexColor { get; set; } = "#FF00FF";
    }
}
