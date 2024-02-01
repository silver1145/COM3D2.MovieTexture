using HarmonyLib;
using System.Collections.Generic;

namespace COM3D2.MovieTexture.Plugin
{
    internal abstract class TryPatch
    {
        public static List<TryPatch> tryPatches = new List<TryPatch>();
        private bool patched = false;
        private int failCount = 0;
        private int failLimit;
        public Harmony harmony;

        public TryPatch(Harmony harmony, int failLimit = 3)
        {
            this.harmony = harmony;
            this.failLimit = failLimit;
            tryPatches.Add(this);
            DoPatch();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
        }

        void DoPatch()
        {
            try
            {
                patched = Patch();
            }
            catch (System.Exception e)
            {
                MovieTexture.Logger.LogDebug(e);
            }
            finally
            {
                if (patched || (failLimit > 0 && ++failCount >= failLimit))
                {
                    RemovePatch();
                }
            }
        }

        void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
        {
            if (!patched)
            {
                DoPatch();
            }
        }

        void RemovePatch()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= SceneLoaded;
            tryPatches.Remove(this);
        }

        public abstract bool Patch();
    }
}
