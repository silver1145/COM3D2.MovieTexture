using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using UnityEngine;

[assembly: AssemblyVersion(COM3D2.MovieTexture.Plugin.PluginInfo.PLUGIN_VERSION + ".*")]
[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]


namespace COM3D2.MovieTexture.Plugin
{
    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "COM3D2.MovieTexture";
        public const string PLUGIN_NAME = "MovieTexture";
        public const string PLUGIN_VERSION = "1.0";
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public sealed class MovieTexture : BaseUnityPlugin
    {
        public static MovieTexture Instance { get; private set; }
        public static Harmony harmony { get; private set; }
        internal static new ManualLogSource Logger => Instance?._Logger;
        private ManualLogSource _Logger => base.Logger;
        private static ConfigEntry<bool> _overrideSetting;
        private static ConfigEntry<Windows.VideoApi> _videoApi;
        private static ConfigEntry<string> _dShowFilter;
        private static ConfigEntry<bool> _hardwareDecoding;
        public static bool overrideSetting;
        public static Windows.VideoApi videoApi;
        public static string dShowFilter;
        public static bool hardwareDecoding;

        private void Awake()
        {
            InitSetting();
            Instance = this;
            harmony = Harmony.CreateAndPatchAll(typeof(MovieTexturePatcher));
            harmony.PatchAll(typeof(PlayerSettingPatcher));
            MovieTexturePatcher.DoTryPatch(harmony);
            CreatePluginObject();
        }

        private void InitSetting()
        {
            _overrideSetting = Config.Bind("Override Setting", "Override", false, "Override Decode Setting");
            _videoApi = Config.Bind("Video Setting", "VideoAPI", Windows.VideoApi.MediaFoundation, "Video API");
            _dShowFilter = Config.Bind("Video Setting", "DirectShowFilter", "Microsoft DTV-DVD Video Decoder", "DirectShow Filter");
            _hardwareDecoding = Config.Bind("Video Setting", "HardwareDecoding", true, "Use Hardware Decoding");
            overrideSetting = _overrideSetting.Value;
            videoApi = _videoApi.Value;
            dShowFilter = _dShowFilter.Value;
            hardwareDecoding = _hardwareDecoding.Value;
            _overrideSetting.SettingChanged += (s, e) => overrideSetting = _overrideSetting.Value;
            _videoApi.SettingChanged += (s, e) => videoApi = _videoApi.Value;
            _dShowFilter.SettingChanged += (s, e) => dShowFilter = _dShowFilter.Value;
            _hardwareDecoding.SettingChanged += (s, e) => hardwareDecoding = _hardwareDecoding.Value;
        }

        void CreatePluginObject()
        {
            GameObject obj = new GameObject("COM3D2.MovieTexture.Plugin");
            MovieTextureManager.InitmediaPlayerManager(obj);
            DontDestroyOnLoad(obj);
        }
    }
}
