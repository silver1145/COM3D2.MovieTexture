using HarmonyLib;

namespace COM3D2.MovieTexture.Plugin
{
    internal class PlayerSettingPatcher
    {
        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "VideoAPI", MethodType.Getter)]
        [HarmonyPrefix]
        internal static bool VideoAPIPrefix(ref string __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.videoApi.ToString();
            }
            return !MovieTexture.overrideSetting;
        }

        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "DShowFilter", MethodType.Getter)]
        [HarmonyPrefix]
        internal static bool DShowFilterPrefix(ref string __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.dShowFilter;
            }
            return !MovieTexture.overrideSetting;
        }

        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "VideoUseHardwareDecoding", MethodType.Getter)]
        [HarmonyPrefix]
        internal static bool VideoUseHardwareDecodingPrefix(ref bool __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.hardwareDecoding;
            }
            return !MovieTexture.overrideSetting;
        }
    }
}
