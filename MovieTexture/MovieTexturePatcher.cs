using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    internal static class MovieTexturePatcher
    {
        public static Dictionary<string, string> textureNames;
        public static Dictionary<string, RenderTexture> renderTextures;
        public static HashSet<string> tempPropNames;

        public static void Reload()
        {
            
            if (renderTextures == null)
            {
                renderTextures = new Dictionary<string, RenderTexture>();
            }
            if (tempPropNames == null)
            {
                tempPropNames = new HashSet<string>();
            }
            tempPropNames.Clear();
            ReloadTextureFiles();
        }

        public static void ReloadTextureFiles()
        {
            if (textureNames == null)
            {
                textureNames = new Dictionary<string, string>();
            }
            else
            {
                textureNames.Clear();
            }
            List<string> alphaPackFiles = new List<string>();
            var Files = Directory.GetFiles(BepInEx.Paths.GameRootPath + "\\Mod", "*.mp4", SearchOption.AllDirectories);
            for (int i = 0; i < Files.Count(); i++)
            {
                string fileName = Files[i].ToLower();
                if (Path.GetFileNameWithoutExtension(fileName).EndsWith(".alphapack"))
                {
                    alphaPackFiles.Add(fileName);
                }
                else
                {
                    textureNames[Path.GetFileNameWithoutExtension(fileName)] = fileName;
                }
            }
            foreach (string afile in alphaPackFiles)
            {
                string name = Path.GetFileNameWithoutExtension(afile);
                name = name.Substring(0, name.LastIndexOf(".alphapack"));
                textureNames[name] = afile;
            }
        }

        public static void SetTexture(Material m, string name, Texture value)
        {
            if (renderTextures == null)
            {
                Reload();
            }
            if (value != null)
            {
                string texName = value.name?.ToLower();
                if (textureNames.TryGetValue(texName, out var path))
                {
                    if (renderTextures.TryGetValue(texName, out var tex))
                    {
                        value = tex;
                    }
                    else
                    {
                        RenderTexture tex2 = new RenderTexture(value.width, value.height, 24);
                        tex2.name = texName;
                        tex2.wrapMode = TextureWrapMode.Repeat;
                        // tex2.antiAliasing = 8;
                        renderTextures[texName] = tex2;
                        value = tex2;
                    }
                    tempPropNames.Add(name);
                }
            }
            m.SetTexture(name, value);
        }

        public static void ProcessRenderer(Renderer renderer)
        {
            if (renderTextures == null)
            {
                Reload();
            }
            if (renderer != null)
            {
                foreach (var m in renderer.sharedMaterials)
                {
                    if (m != null)
                    {
                        foreach(var prop in tempPropNames)
                        {
                            if (m.GetTexture(prop) is RenderTexture rt)
                            {
                                if (rt != null && renderTextures.TryGetValue(rt.name, out var tex) && tex == rt && textureNames.TryGetValue(rt.name, out var path))
                                {
                                    bool flag = true;
                                    foreach (var component in renderer.GetComponents<ResolveToRenderTexture>())
                                    {
                                        if (component.ExternalTexture == rt)
                                        {
                                            flag = false;
                                            break;
                                        }
                                    }
                                    if (flag)
                                    {
                                        MovieTextureManager.AddResolver(path, tex, renderer.gameObject);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            tempPropNames.Clear();
        }

        public static void DoTryPatch(Harmony harmony)
        {
            new TryPatchNPRShader(harmony);
            new TryPatchSceneCapture(harmony);
            new TryPatchDanceCameraMotion(harmony);
            new TryPatchMaidLoader(harmony);
        }

        public static void RefreshCoPrefix()
        {
            ReloadTextureFiles();
        }

        public static IEnumerable<CodeInstruction> FixLeaveTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.Start();
            while (codeMatcher.IsValid)
            {
                codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Leave))
                    .Advance(1)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Nop));
            }
            return codeMatcher.InstructionEnumeration();
        }

        public static IEnumerable<CodeInstruction> ProcessReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Material), "SetTexture", [typeof(string), typeof(Texture)])))
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(SetTexture))));
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(ImportCM), "ReadMaterial")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessReadMaterialTranspiler(instructions);
        }

        public static IEnumerable<CodeInstruction> ProcessMeshTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Renderer), "materials")))
                .Advance(1)
                .InsertAndAdvance(codeMatcher.InstructionAt(-3))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(ProcessRenderer))));
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(TBody), "ChangeMaterial")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> TBodyChangeMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, new CodeMatch(OpCodes.Ldc_I4_1))
                .Advance(-1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 6))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(ProcessRenderer))));
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(ImportCM), "LoadSkinMesh_R")]
        [HarmonyTranspiler]
        internal static IEnumerable<CodeInstruction> ImportCMLoadSkinMeshRTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessMeshTranspiler(instructions);
        }
    }

    internal class TryPatchNPRShader : TryPatch
    {
        public TryPatchNPRShader(Harmony harmony, int failLimit = 1) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            if (AccessTools.TypeByName("COM3D2.NPRShader.Plugin.NPRShader") == null)
            {
                return false;
            }
            var readMaterial = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.NPRShader"), "ReadMaterial");
            var readMaterialTranspiler = AccessTools.Method(typeof(TryPatchNPRShader), nameof(ReadMaterialTranspiler));
            harmony.Patch(readMaterial, transpiler: new HarmonyMethod(readMaterialTranspiler));
            var readMaterialWithSetShader = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.AssetLoader"), "ReadMaterialWithSetShader");
            var readMaterialWithSetShaderTranspiler = AccessTools.Method(typeof(TryPatchNPRShader), nameof(ReadMaterialWithSetShaderTranspiler));
            harmony.Patch(readMaterialWithSetShader, transpiler: new HarmonyMethod(readMaterialWithSetShaderTranspiler));
            var changeNPRSMaterial = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Managed.NPRShaderManaged"), "ChangeNPRSMaterial");
            var changeNPRSMaterialTranspiler = AccessTools.Method(typeof(TryPatchNPRShader), nameof(ChangeNPRSMaterialTranspiler));
            harmony.Patch(changeNPRSMaterial, transpiler: new HarmonyMethod(changeNPRSMaterialTranspiler));
            var updaateMaterial = AccessTools.Method(AccessTools.TypeByName("COM3D2.NPRShader.Plugin.ObjectWindow"), "UpdaateMaterial");
            var updaateMaterialTranspiler = AccessTools.Method(typeof(TryPatchNPRShader), nameof(UpdaateMaterialTranspiler));
            harmony.Patch(updaateMaterial, transpiler: new HarmonyMethod(updaateMaterialTranspiler));
            return true;
        }

        internal static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessReadMaterialTranspiler(instructions);
        }

        internal static IEnumerable<CodeInstruction> ReadMaterialWithSetShaderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessReadMaterialTranspiler(instructions);
        }

        internal static IEnumerable<CodeInstruction> ChangeNPRSMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessMeshTranspiler(instructions);
        }

        // Note: Leave Label
        internal static IEnumerable<CodeInstruction> UpdaateMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.FixLeaveTranspiler(MovieTexturePatcher.ProcessMeshTranspiler(instructions));
        }
    }

    internal class TryPatchSceneCapture : TryPatch
    {
        public TryPatchSceneCapture(Harmony harmony, int failLimit = 1) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            if (AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.SceneCapture") == null)
            {
                return false;
            }
            var readMaterial = AccessTools.Method(AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.AssetLoader"), "ReadMaterial");
            var readMaterialTranspiler = AccessTools.Method(typeof(TryPatchSceneCapture), nameof(ReadMaterialTranspiler));
            harmony.Patch(readMaterial, transpiler: new HarmonyMethod(readMaterialTranspiler));
            var loadModel = AccessTools.Method(AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.ModelWindow"), "LoadModel");
            var loadModelTranspiler = AccessTools.Method(typeof(TryPatchSceneCapture), nameof(LoadModelTranspiler));
            harmony.Patch(loadModel, transpiler: new HarmonyMethod(loadModelTranspiler));
            var loadMesh = AccessTools.Method(AccessTools.TypeByName("CM3D2.SceneCapture.Plugin.AssetLoader"), "LoadMesh");
            var loadMeshTranspiler = AccessTools.Method(typeof(TryPatchSceneCapture), nameof(LoadMeshTranspiler));
            harmony.Patch(loadMesh, transpiler: new HarmonyMethod(loadMeshTranspiler));
            return true;
        }

        internal static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessReadMaterialTranspiler(instructions);
        }

        internal static IEnumerable<CodeInstruction> LoadModelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessMeshTranspiler(instructions);
        }

        internal static IEnumerable<CodeInstruction> LoadMeshTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return MovieTexturePatcher.ProcessMeshTranspiler(instructions);
        }
    }

    internal class TryPatchDanceCameraMotion : TryPatch
    {
        public static Dictionary<ScreenOverlay, ResolveToRenderTexture> screenOverlayResolvers;
        public static Dictionary<ScreenOverlay, string> screenOverlayFileNames;
        public static Dictionary<string, RenderTexture> dcmRenderTextures;
        private static FieldInfo overlayMaterial;
        public static AssetBundle asset;
        public static Shader shader;

        public TryPatchDanceCameraMotion(Harmony harmony, int failLimit = 1) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            if (AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.DanceCameraMotion") == null)
            {
                return false;
            }
            var setScreenOverlayTexture = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.ImageEffectsManager"), "SetScreenOverlayTexture");
            var setScreenOverlayTextureTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(SetScreenOverlayTextureTranspiler));
            harmony.Patch(setScreenOverlayTexture, transpiler: new HarmonyMethod(setScreenOverlayTextureTranspiler));
            var setLookAtCameraComponent = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.ImageEffectsManager"), "SetLookAtCameraComponent");
            var setLookAtCameraComponentTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(SetLookAtCameraComponentTranspiler));
            harmony.Patch(setLookAtCameraComponent, transpiler: new HarmonyMethod(setLookAtCameraComponentTranspiler));
            var resetEffectValue = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.ImageEffectsManager"), "ResetEffectValue");
            var resetEffectValueTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(ResetEffectValueTranspiler));
            harmony.Patch(resetEffectValue, transpiler: new HarmonyMethod(resetEffectValueTranspiler));
            var unenableEffect = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.ImageEffectsManager"), "UnenableEffect");
            var unenableEffectTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(UnenableEffectTranspiler));
            harmony.Patch(unenableEffect, transpiler: new HarmonyMethod(unenableEffectTranspiler));
            var onRenderImage = AccessTools.Method(typeof(ScreenOverlay), "OnRenderImage");
            var onRenderImageTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(OnRenderImageTranspiler));
            harmony.Patch(onRenderImage, transpiler: new HarmonyMethod(onRenderImageTranspiler));
            var getPngFile = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.FileReader"), "GetPngFile");
            var getPngFileTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(GetPngFileTranspiler));
            harmony.Patch(getPngFile, transpiler: new HarmonyMethod(getPngFileTranspiler));
            var destroyObject = AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.DanceObjectManager"), "DestroyObject");
            var destroyObjectTranspiler = AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(DestroyObjectTranspiler));
            harmony.Patch(destroyObject, transpiler: new HarmonyMethod(destroyObjectTranspiler));
            return true;
        }

        public static void InitResources()
        {
            screenOverlayResolvers = new Dictionary<ScreenOverlay, ResolveToRenderTexture>();
            screenOverlayFileNames = new Dictionary<ScreenOverlay, string>();
            dcmRenderTextures = new Dictionary<string, RenderTexture>();
            asset = AssetBundle.LoadFromFile("BepinEx/config/MovieTexture/spriteshader");
            shader = asset.LoadAsset("assets/sprite.shader", typeof(Shader)) as Shader;
            asset.Unload(false);
            overlayMaterial = AccessTools.Field(typeof(ScreenOverlay), "overlayMaterial");
        }

        public static RenderTexture GetRenderTexture(string fileName, Texture tex)
        {
            if (dcmRenderTextures.TryGetValue(fileName, out RenderTexture renderTexture))
            {
                return renderTexture;
            }
            RenderTexture tex2 = new RenderTexture(tex.width, tex.height, 24);
            tex2.name = tex.name;
            tex2.wrapMode = TextureWrapMode.Repeat;
            // tex2.antiAliasing = 8;
            dcmRenderTextures[fileName] = tex2;
            return tex2;
        }

        public static void LoadOverLayTexture(ScreenOverlay screenOverlay, string fileName)
        {
            if (screenOverlayResolvers == null)
            {
                InitResources();
            }
            DestroyResolver(screenOverlay);
            Texture2D texture = screenOverlay.texture;
            if (texture == null)
            {
                return;
            }
            string videoFile = Path.ChangeExtension(fileName, ".mp4");
            string alphaPackFile = Path.ChangeExtension(fileName, ".alphapack.mp4");
            if (File.Exists(alphaPackFile))
            {
                videoFile = alphaPackFile;
            }
            else if (File.Exists(videoFile)) { }
            else
            {
                return;
            }
            RenderTexture renderTexture = GetRenderTexture(videoFile, texture);
            var resolver = MovieTextureManager.AddResolver(videoFile, renderTexture);
            screenOverlayResolvers[screenOverlay] = resolver;
            screenOverlayFileNames[screenOverlay] = videoFile;
            ((Material)overlayMaterial?.GetValue(screenOverlay))?.SetTexture("_Overlay", renderTexture);
        }

        public static void CopyOverLayTexture(ScreenOverlay from, ScreenOverlay to)
        {
            if (screenOverlayResolvers == null)
            {
                InitResources();
            }
            if (screenOverlayFileNames.TryGetValue(from, out var fileName) && dcmRenderTextures.TryGetValue(fileName, out var renderTexture))
            {
                var resolver = MovieTextureManager.AddResolver(fileName, renderTexture);
                screenOverlayResolvers[to] = resolver;
                screenOverlayFileNames[to] = fileName;
                ((Material)overlayMaterial?.GetValue(to))?.SetTexture("_Overlay", renderTexture);
            }
        }

        public static void DestroyResolver(ScreenOverlay screenOverlay)
        {
            if (screenOverlayResolvers.TryGetValue(screenOverlay, out var resolver))
            {
                Object.Destroy(resolver);
                screenOverlayResolvers.Remove(screenOverlay);
            }
            screenOverlayFileNames.Remove(screenOverlay);
        }

        public static bool HasResolver(ScreenOverlay screenOverlay)
        {
            return screenOverlayResolvers != null && screenOverlayResolvers.ContainsKey(screenOverlay);
        }

        public static void ProcessRenderer2(SpriteRenderer renderer, Texture texture, string fileName, bool isBilinear)
        {
            if (screenOverlayResolvers == null)
            {
                InitResources();
            }
            string videoFile = Path.ChangeExtension(fileName, ".mp4");
            string alphaPackFile = Path.ChangeExtension(fileName, ".alphapack.mp4");
            if (File.Exists(alphaPackFile))
            {
                videoFile = alphaPackFile;
            }
            else if (File.Exists(videoFile)) { }
            else
            {
                return;
            }
            RenderTexture renderTexture = GetRenderTexture(videoFile, texture);
            renderTexture.filterMode = isBilinear ? FilterMode.Bilinear : FilterMode.Point;
            MovieTextureManager.AddResolver(videoFile, renderTexture, renderer.gameObject);
            Material mat = new Material(shader);
            mat.SetTexture("_SubTex", renderTexture);
            renderer.material = mat;
        }

        internal static IEnumerable<CodeInstruction> SetScreenOverlayTextureTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_0))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_2))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(LoadOverLayTexture))));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> SetLookAtCameraComponentTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.ImageEffectsManager"), "CopyValueScreenOverlay")))
                .Advance(-3)
                .InsertAndAdvance(codeMatcher.InstructionAt(1))
                .InsertAndAdvance(codeMatcher.InstructionAt(2))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(CopyOverLayTexture))));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> ResetEffectValueTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ScreenOverlay), nameof(ScreenOverlay.texture))))
                .MatchBack(false, new CodeMatch(OpCodes.Callvirt))
                .Advance(2)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_2))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(DestroyResolver))));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> UnenableEffectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(ScreenOverlay), nameof(ScreenOverlay.texture))))
                .Advance(-1)
                .InsertAndAdvance(codeMatcher.Instruction)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(DestroyResolver))));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> OnRenderImageTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {

            CodeMatcher codeMatcher = new CodeMatcher(instructions, generator);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Material), nameof(Material.SetTexture), [typeof(string), typeof(Texture)])))
                .Advance(1).CreateLabel(out Label label)
                .Advance(-2).MatchBack(false, new CodeMatch(OpCodes.Callvirt)).Advance(1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(HasResolver))))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Brtrue_S, label));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> GetPngFileTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(SpriteRenderer), nameof(SpriteRenderer.sprite))))
                .Advance(1)
                .InsertAndAdvance(codeMatcher.Instruction)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 4))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 3))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarga_S, 2))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(AccessTools.TypeByName("COM3D2.DanceCameraMotion.Plugin.SpriteSet"), "isBilinear")))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(TryPatchDanceCameraMotion), nameof(ProcessRenderer2))));
            return codeMatcher.InstructionEnumeration();
        }

        internal static IEnumerable<CodeInstruction> DestroyObjectTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchForward(false, new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Dictionary<string, SpriteRenderer>), "Keys")))
                .MatchForward(false, new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Object), nameof(Object.Destroy), [typeof(Object)])))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Component), nameof(Component.gameObject))));
            return codeMatcher.InstructionEnumeration();
        }
    }

    internal class TryPatchMaidLoader : TryPatch
    {
        public TryPatchMaidLoader(Harmony harmony, int failLimit = 3) : base(harmony, failLimit) { }

        public override bool Patch()
        {
            var mOriginal = AccessTools.Method(AccessTools.TypeByName("COM3D2.MaidLoader.RefreshMod"), "RefreshCo");
            var mPrefix = SymbolExtensions.GetMethodInfo(() => MovieTexturePatcher.RefreshCoPrefix());
            harmony.Patch(mOriginal, prefix: new HarmonyMethod(mPrefix));
            return true;
        }
    }
}