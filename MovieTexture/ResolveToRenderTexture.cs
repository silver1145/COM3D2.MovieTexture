using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
    /// Renders the video texture to a RenderTexture - either one provided by the user (external) or to an internal one.
    /// The video frames can optionally be "resolved" to unpack packed alpha, display a single stereo eye, generate mip maps, and apply colorspace conversions

    public class ResolveToRenderTexture : MonoBehaviour
    {
        [SerializeField] MediaPlayer _mediaPlayer = null;
        [SerializeField] VideoResolveOptions _options = VideoResolveOptions.Create();
        [SerializeField] VideoRender.ResolveFlags _resolveFlags = (VideoRender.ResolveFlags.ColorspaceSRGB | VideoRender.ResolveFlags.Mipmaps | VideoRender.ResolveFlags.PackedAlpha | VideoRender.ResolveFlags.StereoLeft);
        [SerializeField] RenderTexture _externalTexture = null;

        private Material _materialResolve;
        private bool _isMaterialSetup;
        private bool _isMaterialDirty;
        private bool _lastSeeking;
        private RenderTexture _internalTexture;
        private int _textureFrameCount = -1;
        public event OnDestroyDelegate OnDestroyEvnt;
        public delegate void OnDestroyDelegate();

        public MediaPlayer MediaPlayer
        {
            get
            {
                return _mediaPlayer;
            }
            set
            {
                ChangeMediaPlayer(value);
            }
        }

        public RenderTexture ExternalTexture
        {
            get
            {
                return _externalTexture;
            }
            set
            {
                _externalTexture = value;
            }
        }

        public void SetMaterialDirty()
        {
            _isMaterialDirty = true;
        }

        private void ChangeMediaPlayer(MediaPlayer mediaPlayer)
        {
            if (_mediaPlayer != mediaPlayer)
            {
                _mediaPlayer = mediaPlayer;
                _textureFrameCount = -1;
                _isMaterialSetup = false;
                _isMaterialDirty = true;
            }
        }

        void Start()
        {
            _materialResolve = VideoRender.CreateResolveMaterial();
            VideoRender.SetupMaterialForMedia(_materialResolve, _mediaPlayer, -1);
        }

        void LateUpdate()
        {
            if (_mediaPlayer && _externalTexture is null)
            {
                Destroy(this);
            }
            else
            {
                Resolve();
            }
        }

        public void Resolve()
        {
            IMediaProducer textureProducer = _mediaPlayer != null ? _mediaPlayer.TextureProducer : null;
            if (textureProducer != null && textureProducer.GetTexture())
            {
                if (!_isMaterialSetup)
                {
                    VideoRender.SetupMaterialForMedia(_materialResolve, _mediaPlayer, -1);
                    _isMaterialSetup = true;
                    _isMaterialDirty = true;
                }
                if (_isMaterialDirty)
                {
                    VideoRender.SetupResolveMaterial(_materialResolve, _options);
                    _isMaterialDirty = false;
                }

                int textureFrameCount = textureProducer.GetTextureFrameCount();
                if (textureFrameCount != _textureFrameCount)
                {
                    _internalTexture = VideoRender.ResolveVideoToRenderTexture(_materialResolve, _internalTexture, textureProducer, _resolveFlags, alphaPacking: _mediaPlayer.m_AlphaPacking, stereoPacking: _mediaPlayer.m_StereoPacking);
                    if (_textureFrameCount < -1)
                    {
                        if (_textureFrameCount++ % 10 != 0)
                        {
                            return;
                        }
                    }
                    else
                    {
                        _textureFrameCount = textureFrameCount;
                    }
                    
                    if (_internalTexture && _externalTexture)
                    {
                        // NOTE: This blit can be removed once we can ResolveVideoToRenderTexture is made not to recreate textures
                        // NOTE: This blit probably doesn't do correct linear/srgb conversion if the colorspace settings differ, may have to use GL.sRGBWrite
                        Graphics.Blit(_internalTexture, _externalTexture);
                    }
                }
            }
        }

        public void NeedRefresh(int next = 1)
        {
            _textureFrameCount = -10 * next;
        }

        void OnDisable()
        {
            if (_internalTexture)
            {
                RenderTexture.ReleaseTemporary(_internalTexture); _internalTexture = null;
            }
        }

        void OnDestroy()
        {
            if (_materialResolve)
            {
                Destroy(_materialResolve);
                _materialResolve = null;
            }
            if (this.OnDestroyEvnt != null)
            {
                OnDestroyEvnt();
            }
        }
    }
}