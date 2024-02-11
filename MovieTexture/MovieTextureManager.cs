using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    internal class MovieTextureManager : MonoBehaviour
    {
        public static MovieTextureManager Instance { get; private set; }
        public static Dictionary<string, MediaPlayer> mediaPlayers = new Dictionary<string, MediaPlayer>();
        public static Dictionary<string, List<ResolveToRenderTexture>> resolvers = new Dictionary<string, List<ResolveToRenderTexture>>();
        public static GameObject mediaPlayerManager;
        public static GameObject resolverManager;
        public static MethodInfo OpenVideoFromFile = AccessTools.Method(typeof(MediaPlayer), "OpenVideoFromFile", [] );
        public static bool checkFlag = false;
        public static bool tempCheckFlag = false;

        public static void InitmediaPlayerManager(GameObject obj)
        {
            mediaPlayerManager = new GameObject("COM3D2.MovieTexture.Plugin.MediaPlayerManager");
            mediaPlayerManager.transform.parent = obj.transform;
            resolverManager = new GameObject("COM3D2.MovieTexture.Plugin.ResolverManager");
            resolverManager.transform.parent = obj.transform;
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

        public static void TryLoadMediaConfig(MediaPlayer mediaPlayer, string configFileName)
        {

            bool loop = true;
            bool muted = false;
            float volume = 1.0f;
            float playbackRate = 1.0f;
            TextureWrapMode wrapMode = TextureWrapMode.Repeat;
            FilterMode filterMode = FilterMode.Bilinear;
            int anisoLevel = 1;
            if (File.Exists(configFileName))
            {
                try
                {
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.Load(configFileName);
                    XmlNode node = xmlDoc.SelectSingleNode("MediaConfig");
                    bool.TryParse(node.SelectSingleNode("Loop")?.InnerText, out loop);
                    bool.TryParse(node.SelectSingleNode("Muted")?.InnerText, out muted);
                    float.TryParse(node.SelectSingleNode("Volume")?.InnerText, out volume);
                    float.TryParse(node.SelectSingleNode("PlaybackRate")?.InnerText, out playbackRate);
                    try
                    {
                        wrapMode = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode), node.SelectSingleNode("WrapMode")?.InnerText);
                    }
                    catch { }
                    try
                    {
                        filterMode = (FilterMode)Enum.Parse(typeof(FilterMode), node.SelectSingleNode("FilterMode")?.InnerText);
                    }
                    catch { }
                    int.TryParse(node.SelectSingleNode("AnisoLevel")?.InnerText, out anisoLevel);
                }
                catch (Exception e)
                {
                    MovieTexture.Logger.LogError(e);
                }
            }
            mediaPlayer.m_Loop = loop;
            mediaPlayer.m_Muted = muted;
            mediaPlayer.m_Volume = volume;
            mediaPlayer.m_PlaybackRate = playbackRate;
            mediaPlayer.m_WrapMode = wrapMode;
            mediaPlayer.m_FilterMode = filterMode;
            mediaPlayer.m_AnisoLevel = anisoLevel;
        }

        public static MediaPlayer GetMediaPlayer(string filename)
        {
            if (mediaPlayers.TryGetValue(filename, out var player))
            {
                return player;
            }
            var mPlayer = mediaPlayerManager.AddComponent<MediaPlayer>();
            TryLoadMediaConfig(mPlayer, Path.ChangeExtension(filename, ".xml"));
            MediaPlayer.OptionsWindows platformOptionsWindows = mPlayer.PlatformOptionsWindows;
            SetPlatformOptions(platformOptionsWindows);
            if (filename.ToLower().EndsWith(".alphapack.mp4"))
            {
                mPlayer.m_AlphaPacking = AlphaPacking.TopBottom;
            }
            mPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, filename, true);
            mPlayer.Events.AddListener((MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode code) => NeedRefreshAfterSeek(filename, mp, eventType, code));
            mediaPlayers[filename] = mPlayer;
            return mPlayer;
        }

        public static ResolveToRenderTexture AddResolver(string filename, RenderTexture renderTexture, GameObject gameObject = null)
        {
            var player = GetMediaPlayer(filename);
            if (gameObject == null)
            {
                gameObject = resolverManager;
            }
            var resolveToTexture = gameObject.AddComponent<ResolveToRenderTexture>();
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
            return resolveToTexture;
        }

        public static void NeedRefreshAfterSeek(string filename, MediaPlayer mp, MediaPlayerEvent.EventType eventType, ErrorCode code)
        {
            if (mp.Control.IsPaused() && eventType == MediaPlayerEvent.EventType.FinishedSeeking)
            {
                if (resolvers.TryGetValue(filename, out var resolverList))
                {
                    foreach (var resolver in resolverList)
                    {
                        resolver.NeedRefresh(5);
                    }
                }
            }
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
                if (((BaseMediaPlayer)mplayer.Player).GetVideoDisplayRate() == 0)
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
                mplayer.Control.Seek(mplayer.m_PlaybackRate >= 0 ? 0 : ((mplayer.Control as BaseMediaPlayer)?.GetDurationMs() ?? 0));
                mplayer.Play();
            }
        }

        public static void PlayMovie()
        {
            foreach (var mplayer in mediaPlayers.Values)
            {
                mplayer.Play();
            }
        }

        public static void PauseMovie()
        {
            foreach (var mplayer in mediaPlayers.Values)
            {
                mplayer.Pause();
            }
        }

        public static void SeekMovie(float time)
        {
            float timeMs = time * 1000;
            foreach (var mplayer in mediaPlayers.Values)
            {
                float playTimeMs = timeMs * mplayer.m_PlaybackRate;
                BaseMediaPlayer player = mplayer.Control as BaseMediaPlayer;
                if (player != null)
                {
                    var durationMs = player.GetDurationMs();
                    float targetMs;
                    if (Math.Abs(playTimeMs) < durationMs || mplayer.m_Loop)
                    {
                        targetMs = playTimeMs >= 0 ? playTimeMs % durationMs : durationMs + playTimeMs % durationMs;
                    }
                    else
                    {
                        targetMs = durationMs;
                    }
                    player.Seek(targetMs);
                }
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
