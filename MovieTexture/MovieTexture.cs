using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
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

        private void Awake()
        {
            Instance = this;
            harmony = Harmony.CreateAndPatchAll(typeof(MovieTexturePatcher));
            MovieTexturePatcher.DoTryPatch();
            CreatePluginObject();
        }

        void CreatePluginObject()
        {
            GameObject obj = new GameObject("COM3D2.MovieTexture.Plugin");
            MovieTextureManager.InitmediaPlayerManager(obj);
            DontDestroyOnLoad(obj);
        }
    }
}
