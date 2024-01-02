using HarmonyLib;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace COM3D2.MovieTexture.Plugin
{
    internal static class MovieTexturePatcher
    {
        public static Dictionary<string, string> TextureNames;
        public static Dictionary<string, RenderTexture> RenderTextures;
        public static HashSet<string> tempPropNames;

        public static void Reload()
        {
            
            if (RenderTextures == null)
            {
                RenderTextures = new Dictionary<string, RenderTexture>();
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
            if (TextureNames == null)
            {
                TextureNames = new Dictionary<string, string>();
            }
            else
            {
                TextureNames.Clear();
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
                    TextureNames[Path.GetFileNameWithoutExtension(fileName)] = fileName;
                }
            }
            foreach (string afile in alphaPackFiles)
            {
                string name = Path.GetFileNameWithoutExtension(afile);
                name = name.Substring(0, name.LastIndexOf(".alphapack"));
                TextureNames[name] = afile;
            }
        }

        public static void SetTexture(Material m, string name, Texture value)
        {
            if (RenderTextures == null)
            {
                Reload();
            }
            if (value != null)
            {
                string texName = value.name;
                if (TextureNames.TryGetValue(texName, out var path))
                {
                    if (RenderTextures.TryGetValue(texName, out var tex))
                    {
                        value = tex;
                    }
                    else
                    {
                        RenderTexture tex2 = new RenderTexture(value.width, value.height, 24);
                        tex2.name = texName;
                        tex2.wrapMode = TextureWrapMode.Repeat;
                        // tex2.antiAliasing = 8;
                        RenderTextures[texName] = tex2;
                        value = tex2;
                    }
                    tempPropNames.Add(name);
                }
            }
            m.SetTexture(name, value);
        }

        public static void ProcessRenderer(Renderer renderer)
        {
            if (RenderTextures == null)
            {
                Reload();
            }
            if (renderer != null)
            {
                foreach (var m in renderer.materials)
                {
                    if (m != null)
                    {
                        foreach(var prop in tempPropNames)
                        {
                            if (m.GetTexture(prop) is RenderTexture rt)
                            {
                                if (rt != null && RenderTextures.TryGetValue(rt.name, out var tex) && tex == rt && TextureNames.TryGetValue(rt.name, out var path))
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
                                        MovieTextureManager.AddResolver(renderer, path, tex);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            tempPropNames.Clear();
        }

        public static IEnumerable<CodeInstruction> ReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, [new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(Material), "SetTexture", [typeof(string), typeof(Texture)]))])
                .SetInstruction(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(SetTexture))));
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(ImportCM), "ReadMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ImportCMReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReadMaterialTranspiler(instructions);
        }

        [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.NPRShader), "ReadMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NPRShaderReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReadMaterialTranspiler(instructions);
        }

        [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.AssetLoader), "ReadMaterialWithSetShader")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NPRShaderReadMaterialWithSetShaderTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReadMaterialTranspiler(instructions);
        }

        [HarmonyPatch(typeof(CM3D2.SceneCapture.Plugin.AssetLoader), "ReadMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SceneCaptureReadMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ReadMaterialTranspiler(instructions);
        }

        // Process Renderer
        public static IEnumerable<CodeInstruction> ProcessTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, [new CodeMatch(OpCodes.Callvirt, AccessTools.PropertySetter(typeof(Renderer), "materials"))])
                .Advance(1)
                .InsertAndAdvance(codeMatcher.InstructionAt(-3))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(ProcessRenderer))));
            return codeMatcher.InstructionEnumeration();
        }

        public static IEnumerable<CodeInstruction> FixLeaveTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.Start();
            while (codeMatcher.IsValid)
            {
                codeMatcher.MatchForward(false, [new CodeMatch(OpCodes.Leave)])
                    .Advance(1)
                    .InsertAndAdvance(new CodeInstruction(OpCodes.Nop));
            }
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(TBody), "ChangeMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> TBodyChangeMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            CodeMatcher codeMatcher = new CodeMatcher(instructions);
            codeMatcher.End()
                .MatchBack(false, [new CodeMatch(OpCodes.Ldc_I4_1)])
                .Advance(-1)
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldloc_S, 6))
                .InsertAndAdvance(new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MovieTexturePatcher), nameof(ProcessRenderer))));
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(ImportCM), "LoadSkinMesh_R")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> ImportCMLoadSkinMeshRTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessTranspiler(instructions);
        }

        [HarmonyPatch(typeof(COM3D2.NPRShader.Managed.NPRShaderManaged), "ChangeNPRSMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NPRShaderManagedChangeNPRSMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessTranspiler(instructions);
        }

        // Note: Leave Label
        [HarmonyPatch(typeof(COM3D2.NPRShader.Plugin.ObjectWindow), "UpdaateMaterial")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> NPRShaderObjectWindowUpdaateMaterialTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return FixLeaveTranspiler(ProcessTranspiler(instructions));
        }

        [HarmonyPatch(typeof(CM3D2.SceneCapture.Plugin.ModelWindow), "LoadModel")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SceneCaptureModelWindowLoadModelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessTranspiler(instructions);
        }

        [HarmonyPatch(typeof(CM3D2.SceneCapture.Plugin.AssetLoader), "LoadMesh")]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> SceneCaptureLoadMeshTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ProcessTranspiler(instructions);
        }
    }
}