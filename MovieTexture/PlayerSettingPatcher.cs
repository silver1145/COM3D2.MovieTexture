using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    internal class PlayerSettingPatcher
    {
        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "VideoAPI", MethodType.Getter)]
        [HarmonyPrefix]
        public static bool VideoAPIPrefix(ref string __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.videoApi.ToString();
            }
            return !MovieTexture.overrideSetting;
        }

        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "DShowFilter", MethodType.Getter)]
        [HarmonyPrefix]
        public static bool DShowFilterPrefix(ref string __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.dShowFilter;
            }
            return !MovieTexture.overrideSetting;
        }

        [HarmonyPatch(typeof(CMSystem.SerializeConfig), "VideoUseHardwareDecoding", MethodType.Getter)]
        [HarmonyPrefix]
        public static bool VideoUseHardwareDecodingPrefix(ref bool __result)
        {
            if (MovieTexture.overrideSetting)
            {
                __result = MovieTexture.hardwareDecoding;
            }
            return !MovieTexture.overrideSetting;
        }
    }
}
