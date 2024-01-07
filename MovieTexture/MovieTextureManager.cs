using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    internal class MovieTextureManager : MonoBehaviour
    {
        public static MovieTextureManager Instance { get; private set; }
        public static Dictionary<string, MediaPlayer> mediaPlayers = new Dictionary<string, MediaPlayer>();
        public static Dictionary<string, List<ResolveToRenderTexture>> resolvers = new Dictionary<string, List<ResolveToRenderTexture>>();
        public static GameObject mediaPlayerManager;
        public static MethodInfo OpenVideoFromFile = AccessTools.Method(typeof(MediaPlayer), "OpenVideoFromFile", [] );
        public static bool checkFlag = false;
        public static bool tempCheckFlag = false;

        public static void InitmediaPlayerManager(GameObject obj)
        {
            mediaPlayerManager = new GameObject("COM3D2.MovieTexture.Plugin.MediaPlayerManager");
            mediaPlayerManager.transform.parent = obj.transform;
            Instance = obj.AddComponent<MovieTextureManager>();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += SceneLoaded;
        }

        public static void SetPlatformOptions(MediaPlayer.OptionsWindows platformOptionsWindows)
        {
            string videoAPI = GameMain.Instance.CMSystem.SConfig.VideoAPI;
            if (!string.IsNullOrEmpty(videoAPI))
            {
                try
                {
                    platformOptionsWindows.videoApi = (Windows.VideoApi)Enum.Parse(typeof(Windows.VideoApi), videoAPI, true);
                }
                finally { }
            }
            string dshowFilter = GameMain.Instance.CMSystem.SConfig.DShowFilter;
            if (!string.IsNullOrEmpty(dshowFilter))
            {
                platformOptionsWindows.preferredFilters.Add(dshowFilter);
            }
            platformOptionsWindows.useHardwareDecoding = GameMain.Instance.CMSystem.SConfig.VideoUseHardwareDecoding;
        }

        public static MediaPlayer GetMediaPlayer(string filename)
        {
            if (mediaPlayers.TryGetValue(filename, out var player))
            {
                return player;
            }
            var mPlayer = mediaPlayerManager.AddComponent<MediaPlayer>();
            mPlayer.m_WrapMode = TextureWrapMode.Repeat;
            mPlayer.m_Loop = true;
            MediaPlayer.OptionsWindows platformOptionsWindows = mPlayer.PlatformOptionsWindows;
            SetPlatformOptions(platformOptionsWindows);
            if (filename.ToLower().EndsWith(".alphapack.mp4"))
            {
                mPlayer.m_AlphaPacking = AlphaPacking.TopBottom;
            }
            mPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, filename, true);
            mediaPlayers[filename] = mPlayer;
            return mPlayer;
        }

        public static void AddResolver(Renderer renderer, string filename, RenderTexture renderTexture)
        {
            var player = GetMediaPlayer(filename);
            var resolveToTexture = renderer.gameObject.AddComponent<ResolveToRenderTexture>();
            resolveToTexture.MediaPlayer = player;
            resolveToTexture.ExternalTexture = renderTexture;
            resolveToTexture.OnDestroyEvnt += SetCheckFlag;
            if (!resolvers.ContainsKey(filename))
            {
                resolvers[filename] = new List<ResolveToRenderTexture>();
            }
            else
            {
                resolveToTexture.enabled = false;
            }
            resolvers[filename].Add(resolveToTexture);
        }

        public static void CheckResolvers()
        {
            List<string> keysNeedRemove = new List<string>();
            foreach (var key in resolvers.Keys)
            {
                bool flag = true;
                for (int i = resolvers[key].Count - 1; i >= 0; i--)
                {
                    if (!resolvers[key][i])
                    {
                        resolvers[key].RemoveAt(i);
                    }
                    else if (resolvers[key][i].enabled)
                    {
                        if (!flag)
                        {
                            resolvers[key][i].enabled = false;
                        }
                        flag = false;
                    }
                }
                if (resolvers[key].Count == 0)
                {
                    GameObject.Destroy(mediaPlayers[key]);
                    keysNeedRemove.Add(key);
                }
                else if (flag)
                {
                    resolvers[key].First().enabled = true;
                }
            }
            foreach (var key in keysNeedRemove)
            {
                resolvers.Remove(key);
                mediaPlayers.Remove(key);
            }
            foreach (var mplayer in mediaPlayers.Values)
            {
                if (((BaseMediaPlayer)mplayer.m_Player).GetVideoDisplayRate() == 0)
                {
                    MovieTexture.Logger.LogInfo("Incorrect MediaPlayer, Reopen.");
                    OpenVideoFromFile?.Invoke(mplayer, null);
                }
            }
        }

        public static void SetCheckFlag()
        {
            tempCheckFlag = true;
        }

        public static void ResetMovie()
        {
            foreach (var mplayer in mediaPlayers.Values)
            {
                mplayer.Control.Seek(0);
            }
        }

        private static void SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode)
        {
            Instance.StartCoroutine(SceneLoadedCheck());
        }

        private static System.Collections.IEnumerator SceneLoadedCheck()
        {
            yield return new WaitForSeconds(1f);
            SetCheckFlag();
        }

        void LateUpdate()
        {
            if (checkFlag)
            {
                CheckResolvers();
                checkFlag = false;
            }
            if (tempCheckFlag)
            {
                checkFlag = true;
                tempCheckFlag = false;
            }
        }
    }
}
