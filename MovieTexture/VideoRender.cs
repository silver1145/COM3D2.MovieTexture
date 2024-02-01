#define UNITY_PLATFORM_SUPPORTS_LINEAR

using UnityEngine;

namespace RenderHeads.Media.AVProVideo
{
    public struct LazyShaderProperty
    {
        public LazyShaderProperty(string name)
        {
            _name = name;
            _id = 0;
        }

        public string Name { get { return _name; } }
        public int Id { get { if (_id == 0) { _id = Shader.PropertyToID(_name); } return _id; } }

        private string _name;
        private int _id;
    }

    [System.Serializable]
    public struct VideoResolveOptions
    {
        [SerializeField] public bool applyHSBC;
        [SerializeField, Range(0f, 1f)] public float hue;
        [SerializeField, Range(0f, 1f)] public float saturation;
        [SerializeField, Range(0f, 1f)] public float brightness;
        [SerializeField, Range(0f, 1f)] public float contrast;
        [SerializeField, Range(0.0001f, 10f)] public float gamma;
        [SerializeField] public Color tint;
        [SerializeField] public bool generateMipmaps;

        public bool IsColourAdjust()
        {
            return (applyHSBC && (hue != 0.0f || saturation != 0.5f || brightness != 0.5f || contrast != 0.5f || gamma != 1.0f));
        }

        internal void ResetColourAdjust()
        {
            hue = 0.0f;
            saturation = 0.5f;
            brightness = 0.5f;
            contrast = 0.5f;
            gamma = 1.0f;
        }

        public static VideoResolveOptions Create()
        {
            VideoResolveOptions result = new VideoResolveOptions()
            {
                tint = Color.white,
            };
            result.ResetColourAdjust();

            return result;
        }
    }

    public class VideoRender
    {
        public const string Keyword_AlphaPackTopBottom = "ALPHAPACK_TOP_BOTTOM";
        public const string Keyword_AlphaPackLeftRight = "ALPHAPACK_LEFT_RIGHT";
        public const string Keyword_AlphaPackNone = "ALPHAPACK_NONE";
        public const string Keyword_ApplyGamma = "APPLY_GAMMA";
        public const string Keyword_StereoTopBottom = "STEREO_TOP_BOTTOM";
        public const string Keyword_StereoLeftRight = "STEREO_LEFT_RIGHT";
        public const string Keyword_StereoCustomUV = "STEREO_CUSTOM_UV";
        public const string Keyword_StereoTwoTextures = "STEREO_TWOTEXTURES";
        public const string Keyword_StereoNone = "MONOSCOPIC";
        public const string Keyword_LayoutEquirect180 = "LAYOUT_EQUIRECT180";
        public const string Keyword_LayoutNone = "LAYOUT_NONE";

        public static readonly LazyShaderProperty PropVertScale = new LazyShaderProperty("_VertScale");
        public static readonly LazyShaderProperty PropApplyGamma = new LazyShaderProperty("_ApplyGamma");
        public static readonly LazyShaderProperty PropAlphaPack = new LazyShaderProperty("AlphaPack");
        public static readonly LazyShaderProperty PropStereo = new LazyShaderProperty("Stereo");
        public static readonly LazyShaderProperty PropLayout = new LazyShaderProperty("Layout");

        public static string Keyword_UseHSBC = "USE_HSBC";
        public static readonly LazyShaderProperty PropHue = new LazyShaderProperty("_Hue");
        public static readonly LazyShaderProperty PropSaturation = new LazyShaderProperty("_Saturation");
        public static readonly LazyShaderProperty PropContrast = new LazyShaderProperty("_Contrast");
        public static readonly LazyShaderProperty PropBrightness = new LazyShaderProperty("_Brightness");
        public static readonly LazyShaderProperty PropInvGamma = new LazyShaderProperty("_InvGamma");

        public static Shader shader; 

        public static Material CreateResolveMaterial()
        {
            if (shader is null)
            {
                AssetBundle asset = AssetBundle.LoadFromFile("BepinEx/config/MovieTexture/resolveshader");
                shader = asset.LoadAsset("assets/shaders/resources/avprovideo-internal-resolve.shader", typeof(Shader)) as Shader;
                asset.Unload(false);
            }
            return new Material(shader);
        }

        public static void SetupLayoutMaterial(Material material, VideoMapping mapping)
        {
            switch (mapping)
            {
                default:
                    material.DisableKeyword(Keyword_LayoutEquirect180);
                    material.EnableKeyword(Keyword_LayoutNone);
                    break;
                // Only EquiRectangular180 currently does anything in the shader
                case VideoMapping.EquiRectangular180:
                    material.DisableKeyword(Keyword_LayoutNone);
                    material.EnableKeyword(Keyword_LayoutEquirect180);
                    break;
            }
        }

        public static void SetupAlphaPackedMaterial(Material material, AlphaPacking packing)
        {
            switch (packing)
            {
                case AlphaPacking.None:
                    material.DisableKeyword(Keyword_AlphaPackTopBottom);
                    material.DisableKeyword(Keyword_AlphaPackLeftRight);
                    material.EnableKeyword(Keyword_AlphaPackNone);
                    break;
                case AlphaPacking.TopBottom:
                    material.DisableKeyword(Keyword_AlphaPackNone);
                    material.DisableKeyword(Keyword_AlphaPackLeftRight);
                    material.EnableKeyword(Keyword_AlphaPackTopBottom);
                    break;
                case AlphaPacking.LeftRight:
                    material.DisableKeyword(Keyword_AlphaPackNone);
                    material.DisableKeyword(Keyword_AlphaPackTopBottom);
                    material.EnableKeyword(Keyword_AlphaPackLeftRight);
                    break;
            }
        }

        public static void SetupStereoMaterial(Material material, StereoPacking packing)
        {
            switch (packing)
            {
                case StereoPacking.None:
                    material.DisableKeyword(Keyword_StereoTopBottom);
                    material.DisableKeyword(Keyword_StereoLeftRight);
                    material.DisableKeyword(Keyword_StereoCustomUV);
                    material.DisableKeyword(Keyword_StereoTwoTextures);
                    material.EnableKeyword(Keyword_StereoNone);
                    break;
                case StereoPacking.TopBottom:
                    material.DisableKeyword(Keyword_StereoNone);
                    material.DisableKeyword(Keyword_StereoLeftRight);
                    material.DisableKeyword(Keyword_StereoCustomUV);
                    material.DisableKeyword(Keyword_StereoTwoTextures);
                    material.EnableKeyword(Keyword_StereoTopBottom);
                    break;
                case StereoPacking.LeftRight:
                    material.DisableKeyword(Keyword_StereoNone);
                    material.DisableKeyword(Keyword_StereoTopBottom);
                    material.DisableKeyword(Keyword_StereoTwoTextures);
                    material.DisableKeyword(Keyword_StereoCustomUV);
                    material.EnableKeyword(Keyword_StereoLeftRight);
                    break;
                case StereoPacking.CustomUV:
                    material.DisableKeyword(Keyword_StereoNone);
                    material.DisableKeyword(Keyword_StereoTopBottom);
                    material.DisableKeyword(Keyword_StereoLeftRight);
                    material.DisableKeyword(Keyword_StereoTwoTextures);
                    material.EnableKeyword(Keyword_StereoCustomUV);
                    break;
            }
        }


        public static void SetupGammaMaterial(Material material, bool playerSupportsLinear)
        {
#if UNITY_PLATFORM_SUPPORTS_LINEAR
            if (QualitySettings.activeColorSpace == ColorSpace.Linear && !playerSupportsLinear)
            {
                material.EnableKeyword(Keyword_ApplyGamma);
            }
            else
            {
                material.DisableKeyword(Keyword_ApplyGamma);
            }
#endif
        }

        public static void SetupVerticalFlipMaterial(Material material, bool flip)
        {
            material.SetFloat(VideoRender.PropVertScale.Id, flip ? -1f : 1f);
        }

        public static Texture GetTexture(MediaPlayer mediaPlayer, int textureIndex)
        {
            Texture result = null;
            if (mediaPlayer != null)
            {
                if (mediaPlayer.FrameResampler != null && mediaPlayer.FrameResampler.OutputTexture != null)
                {
                    if (mediaPlayer.FrameResampler.OutputTexture.Length > textureIndex)
                    {
                        result = mediaPlayer.FrameResampler.OutputTexture[textureIndex];
                    }
                }
                else if (mediaPlayer.TextureProducer != null)
                {
                    if (mediaPlayer.TextureProducer.GetTextureCount() > textureIndex)
                    {
                        result = mediaPlayer.TextureProducer.GetTexture(textureIndex);
                    }
                }
            }
            return result;
        }

        public static void SetupMaterialForMedia(Material material, MediaPlayer mediaPlayer, int texturePropId = -1, Texture fallbackTexture = null, bool forceFallbackTexture = false)
        {
            Debug.Assert(material != null);
            if (mediaPlayer != null)
            {
                Texture mainTexture = GetTexture(mediaPlayer, 0);
                Texture yCbCrTexture = GetTexture(mediaPlayer, 1);

                if (texturePropId != -1)
                {
                    if (mainTexture == null || forceFallbackTexture)
                    {
                        mainTexture = fallbackTexture;
                    }
                    material.SetTexture(texturePropId, mainTexture);
                }

                SetupMaterial(material,
                            (mediaPlayer.TextureProducer != null) ? mediaPlayer.TextureProducer.RequiresVerticalFlip() : false,
                            (mediaPlayer.Info != null) ? mediaPlayer.Info.PlayerSupportsLinearColorSpace() : true,
                            (mediaPlayer.TextureProducer != null) ? mediaPlayer.TextureProducer.GetYpCbCrTransform() : Matrix4x4.identity,
                            yCbCrTexture,
                            (mediaPlayer.Info != null && mediaPlayer.PlatformOptionsAndroid.useFastOesPath) ? mediaPlayer.Info.GetTextureTransform() : null,
                            mediaPlayer.VideoLayoutMapping,
                            mediaPlayer.m_StereoPacking,
                            mediaPlayer.m_AlphaPacking);
            }
            else
            {
                if (texturePropId != -1)
                {
                    material.SetTexture(texturePropId, fallbackTexture);
                }
                SetupMaterial(material, false, true, Matrix4x4.identity, null);
            }
        }

        internal static void SetupMaterial(Material material, bool flipVertically, bool playerSupportsLinear, Matrix4x4 ycbcrTransform, Texture ycbcrTexture = null, float[] textureTransform = null,
            VideoMapping mapping = VideoMapping.Normal, StereoPacking stereoPacking = StereoPacking.None, AlphaPacking alphaPacking = AlphaPacking.None)
        {
            SetupVerticalFlipMaterial(material, flipVertically);

            // Apply changes for layout
            if (material.HasProperty(VideoRender.PropLayout.Id))
            {
                VideoRender.SetupLayoutMaterial(material, mapping);
            }

            // Apply changes for stereo videos
            if (material.HasProperty(VideoRender.PropStereo.Id))
            {
                VideoRender.SetupStereoMaterial(material, stereoPacking);
            }

            // Apply changes for alpha videos
            if (material.HasProperty(VideoRender.PropAlphaPack.Id))
            {
                VideoRender.SetupAlphaPackedMaterial(material, alphaPacking);
            }

            // Apply gamma correction
#if UNITY_PLATFORM_SUPPORTS_LINEAR
            if (material.HasProperty(VideoRender.PropApplyGamma.Id))
            {
                VideoRender.SetupGammaMaterial(material, playerSupportsLinear);
            }
#endif
        }

        [System.Flags]
        public enum ResolveFlags : int
        {
            Mipmaps = 1 << 0,
            PackedAlpha = 1 << 1,
            StereoLeft = 1 << 2,
            StereoRight = 1 << 3,
            ColorspaceSRGB = 1 << 4,
        }

        public static void SetupResolveMaterial(Material material, VideoResolveOptions options)
        {
            if (options.IsColourAdjust())
            {
                material.EnableKeyword(VideoRender.Keyword_UseHSBC);
                material.SetFloat(VideoRender.PropHue.Id, options.hue);
                material.SetFloat(VideoRender.PropSaturation.Id, options.saturation);
                material.SetFloat(VideoRender.PropBrightness.Id, options.brightness);
                material.SetFloat(VideoRender.PropContrast.Id, options.contrast);
                material.SetFloat(VideoRender.PropInvGamma.Id, 1f / options.gamma);
            }
            else
            {
                material.DisableKeyword(VideoRender.Keyword_UseHSBC);
            }

            material.color = options.tint;
        }

        public static RenderTexture ResolveVideoToRenderTexture(Material resolveMaterial, RenderTexture targetTexture, IMediaProducer texture, ResolveFlags flags, ScaleMode scaleMode = ScaleMode.StretchToFill, AlphaPacking alphaPacking = AlphaPacking.None, StereoPacking stereoPacking = StereoPacking.None)
        {
            int targetWidth = texture.GetTexture(0).width;
            int targetHeight = texture.GetTexture(0).height;
            StereoEye eyeMode = StereoEye.Both;
            if (((flags & ResolveFlags.StereoLeft) == ResolveFlags.StereoLeft) &&
                ((flags & ResolveFlags.StereoRight) != ResolveFlags.StereoRight))
            {
                eyeMode = StereoEye.Left;
            }
            else if (((flags & ResolveFlags.StereoLeft) != ResolveFlags.StereoLeft) &&
                    ((flags & ResolveFlags.StereoRight) == ResolveFlags.StereoRight))
            {
                eyeMode = StereoEye.Right;
            }
            // RJT NOTE: No longer passing in PAR as combined with larger videos (e.g. 8K+) it can lead to textures >16K which most platforms don't support
            // - Instead, the PAR is accounted for during drawing (which is more efficient too)
            // - https://github.com/RenderHeads/UnityPlugin-AVProVideo/issues/1297
            GetResolveTextureSize(alphaPacking, stereoPacking, eyeMode, ref targetWidth, ref targetHeight);

            if (targetTexture)
            {
                bool sizeChanged = (targetTexture.width != targetWidth || targetTexture.height != targetHeight);
                if (sizeChanged)
                {
                    RenderTexture.ReleaseTemporary(targetTexture); targetTexture = null;
                }
            }

            if (!targetTexture)
            {
                RenderTextureReadWrite readWrite = ((flags & ResolveFlags.ColorspaceSRGB) == ResolveFlags.ColorspaceSRGB) ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
                targetTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32, readWrite);
            }

            // Set target mipmap generation support
            {
                bool requiresMipmap = (flags & ResolveFlags.Mipmaps) == ResolveFlags.Mipmaps;
                bool requiresRecreate = (targetTexture.IsCreated() && targetTexture.useMipMap != requiresMipmap);
                if (requiresRecreate)
                {
                    targetTexture.Release();
                }
                if (!targetTexture.IsCreated())
                {
                    targetTexture.useMipMap = targetTexture.autoGenerateMips = requiresMipmap;
                    targetTexture.Create();
                }
            }

            // Render resolve blit
            // TODO: combine these two paths into a single material blit
            {
                bool prevSRGB = GL.sRGBWrite;
                GL.sRGBWrite = targetTexture.sRGB;
                RenderTexture prev = RenderTexture.active;
                if (scaleMode == ScaleMode.StretchToFill)
                {
                    Graphics.Blit(texture.GetTexture(0), targetTexture, resolveMaterial);
                }
                else
                {
                    RenderTexture.active = targetTexture;
                    bool partialAreaRender = (scaleMode == ScaleMode.ScaleToFit);
                    if (partialAreaRender)
                    {
                        GL.Clear(false, true, Color.black);
                    }
                    VideoRender.DrawTexture(new Rect(0f, 0f, targetTexture.width, targetTexture.height), texture.GetTexture(0), scaleMode, alphaPacking, resolveMaterial);
                }
                RenderTexture.active = prev;
                GL.sRGBWrite = prevSRGB;
            }

            return targetTexture;
        }

        public static void GetResolveTextureSize(AlphaPacking alphaPacking, StereoPacking stereoPacking, StereoEye eyeMode, ref int width, ref int height)
        {
            switch (alphaPacking)
            {
                case AlphaPacking.LeftRight:
                    width /= 2;
                    break;
                case AlphaPacking.TopBottom:
                    height /= 2;
                    break;
            }
            if (eyeMode != StereoEye.Both)
            {
                switch (stereoPacking)
                {
                    case StereoPacking.LeftRight:
                        width /= 2;
                        break;
                    case StereoPacking.TopBottom:
                        height /= 2;
                        break;
                }
            }
        }

        public static void DrawTexture(Rect destRect, Texture texture, ScaleMode scaleMode, AlphaPacking alphaPacking, Material material)
        {
            if (Event.current == null || Event.current.type == EventType.Repaint)
            {
                int sourceWidth = texture.width;
                int sourceHeight = texture.height;
                GetResolveTextureSize(alphaPacking, StereoPacking.None, StereoEye.Both, ref sourceWidth, ref sourceHeight);

                float sourceRatio = (float)sourceWidth / (float)sourceHeight;
                Rect sourceRect = new Rect(0f, 0f, 1f, 1f);
                switch (scaleMode)
                {
                    case ScaleMode.ScaleAndCrop:
                        {
                            float destRatio = destRect.width / destRect.height;
                            if (destRatio > sourceRatio)
                            {
                                float adjust = sourceRatio / destRatio;
                                sourceRect = new Rect(0f, (1f - adjust) * 0.5f, 1f, adjust);
                            }
                            else
                            {
                                float adjust = destRatio / sourceRatio;
                                sourceRect = new Rect(0.5f - adjust * 0.5f, 0f, adjust, 1f);
                            }
                        }
                        break;
                    case ScaleMode.ScaleToFit:
                        {
                            float destRatio = destRect.width / destRect.height;
                            if (destRatio > sourceRatio)
                            {
                                float adjust = sourceRatio / destRatio;
                                destRect = new Rect(destRect.xMin + destRect.width * (1f - adjust) * 0.5f, destRect.yMin, adjust * destRect.width, destRect.height);
                            }
                            else
                            {
                                float adjust = destRatio / sourceRatio;
                                destRect = new Rect(destRect.xMin, destRect.yMin + destRect.height * (1f - adjust) * 0.5f, destRect.width, adjust * destRect.height);
                            }
                        }
                        break;
                    case ScaleMode.StretchToFill:
                        break;
                }

                GL.PushMatrix();
                if (RenderTexture.active == null)
                {
                    //GL.LoadPixelMatrix();
                    GL.LoadPixelMatrix(0f, Screen.width, Screen.height, 0f);
                }
                else
                {
                    GL.LoadPixelMatrix(0f, RenderTexture.active.width, RenderTexture.active.height, 0f);
                }
                Graphics.DrawTexture(destRect, texture, sourceRect, 0, 0, 0, 0, GUI.color, material);
                GL.PopMatrix();
            }
        }
    }
}