using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SkillManager;
using ServerSync;
using UnityEngine;

namespace ThirdEye
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class ThirdEyePlugin : BaseUnityPlugin
    {
        internal const string ModName = "ThirdEye";
        internal const string ModVersion = "2.1.4";
        internal const string Author = "Azumatt";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        public static readonly ManualLogSource ThirdEyeLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("General", "Force Server Config", Toggle.On, "Force Server Config");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            BaseRange = config("Adjustments", "Base Detection Range", 20.0F,
                "How far you can see an enemy's health bar by default. Keep in mind that a very large value will not allow you to see enemies unloaded from the game due to distance.");
            SkillMultiplier = config("Adjustments", "Skill Multiplier", 1.0F,
                "How much to multiply the increase in detection range granted by Third Eye skill level. A multiplier of 1 grants 30 additional meters of range at Third Eye 100, for a total of 60 meters. A multiplier of 5 would grant 150 additional meters, and so on. ");
            StaminaDrain = config("Adjustments", "Stamina Drain", 70.0F,
                "How much stamina to drain when using the skill. By default this is 70, rather large to make the game balanced. Setting this to 0 would cause no drain to occur.");
            ShowMessage = config("Features", "Show Message", Toggle.On,
                "When using the ability, show a message confirming how many creatures are nearby.", false);
            ShowVisual = config("Features", "Visual Effect", Toggle.On,
                "Show an expanding circle upon using the ability, representing the detection range.", false);
            PlayAudio = config("Features", "Audio Effect", Toggle.On,
                "Play a shwimsical sound when using the ability. Can be heard by other players.", false);
            ShowMinimapIcons = config("Features", "Minimap Icons", Toggle.On,
                "Show detected enemies on the minimap.", false);
            AllowAdditionalZoom = config("Features", "Additional Zoom", Toggle.On,
                "Lets you zoom in two additional steps on the minimap.", false);
            KeyBind = config("Features", "Custom Keybind", KeyboardShortcut.Empty, 
                new ConfigDescription(
                    "If you really don't like pressing C while crouching. Change the bind to something else. If you want to use a mouse key, include a space: mouse 3, for example. Valid inputs: https://docs.unity3d.com/ScriptReference/KeyCode.html",
                    new AcceptableShortcuts()), false);
            CustomMessage = config("Features", "Custom Message", "",
                "Set a custom message if you'd like. Example: Detected # enemies nearby. (# will be replaced with the number of enemies detected.", false);
            MessageColor = config("Adjustments", "Message Color", "#006448",
                "Customize the color of the message with a hex code.", false);
            VisualEffectColor = config("Adjustments", "Visual Effect Color", "#006448",
                "Customize the color of the visual effect with a hex code. Must reboot game for this to take effect.",
                false);
            MiniMapZoomLevel = config("Adjustments", "MiniMap Zoom Level", 0.00375F,
                "Zoom level of the minimap, default value allows 2 additional zoom levels. Play around with this setting to get your desired effects.", false);
            AllowPlayerDetection = config("Features", "Player detection", Toggle.Off,
                "Should the ping be able to detect other players?");
            ShowTames = config("Features", "Tames detection", Toggle.Off,
                "Should the ping be able to detect tamed animals?");


            Skill thirdeye = new("ThirdEye", "thirdeye.png"); // Skill name along with the skill icon. By default the icon is found in the icons folder. Put it there if you wish to load one.
            thirdeye.Name.English("Third Eye"); // Vanilla can't find it if it has a space, so, re-localize here with one.
            thirdeye.Description.English("Sense enemies around you");
            thirdeye.Configurable = true;


            Assembly assembly = Assembly.GetExecutingAssembly();
            Harmony harmony = new(ModGUID);
            harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                ThirdEyeLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                ThirdEyeLogger.LogError($"There was an issue loading your {ConfigFileName}");
                ThirdEyeLogger.LogError("Please check your config entries for spelling and format!");
            }
        }


        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<float> BaseRange = null!;
        public static ConfigEntry<float> SkillMultiplier = null!;
        public static ConfigEntry<float> StaminaDrain = null!;
        public static ConfigEntry<Toggle> ShowMessage = null!;
        public static ConfigEntry<Toggle> ShowVisual = null!;
        public static ConfigEntry<Toggle> PlayAudio = null!;
        public static ConfigEntry<KeyboardShortcut> KeyBind = null!;
        public static bool InvalidKey = false;
        public static ConfigEntry<string> CustomMessage = null!;
        public static ConfigEntry<string> MessageColor = null!;
        public static ConfigEntry<Toggle> ShowMinimapIcons = null!;
        public static ConfigEntry<Toggle> AllowAdditionalZoom = null!;
        public static ConfigEntry<float> MiniMapZoomLevel = null!;
        public static ConfigEntry<string> VisualEffectColor = null!;
        public static ConfigEntry<Toggle> AllowPlayerDetection = null!;
        public static ConfigEntry<Toggle> ShowTames = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            public bool? Browsable = false;
        }


        class AcceptableShortcuts : AcceptableValueBase
        {
            public AcceptableShortcuts() : base(typeof(KeyboardShortcut))
            {
            }

            public override object Clamp(object value) => value;
            public override bool IsValid(object value) => true;

            public override string ToDescriptionString() =>
                "# Acceptable values: " + string.Join(", ", UnityInput.Current.SupportedKeyCodes);
        }

        #endregion
    }
}