using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    class MovieTextureManager : MonoBehaviour
    {
        public static Dictionary<string, MediaPlayer> mediaPlayers = new Dictionary<string, MediaPlayer>();
        public static Dictionary<string, List<ResolveToRenderTexture>> resolvers = new Dictionary<string, List<ResolveToRenderTexture>>();
        public static GameObject mediaPlayerManager;
        public static bool checkFlag = false;
        public static bool tempCheckFlag = false;

        public static void InitmediaPlayerManager(GameObject obj)
        {
            mediaPlayerManager = new GameObject("COM3D2.MovieTexture.MediaPlayerManager");
            mediaPlayerManager.transform.parent = obj.transform;
        }

        public static MediaPlayer GetMediaPlayer(string filename)
        {
            if (mediaPlayers.TryGetValue(filename, out var player))
            {
                return player;
            }
            var avPlayer = mediaPlayerManager.AddComponent<MediaPlayer>();
            avPlayer.name = Path.GetFileNameWithoutExtension(filename).ToLower();
            avPlayer.m_WrapMode = TextureWrapMode.Repeat;
            if (filename.ToLower().EndsWith(".alphapack.mp4"))
            {
                avPlayer.m_AlphaPacking = AlphaPacking.TopBottom;
            }
            avPlayer.m_Loop = true;
            avPlayer.OpenVideoFromFile(MediaPlayer.FileLocation.AbsolutePathOrURL, filename, true);
            avPlayer.m_Muted = true;
            // avPlayer.Start();
            mediaPlayers[filename] = avPlayer;
            return avPlayer;
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
