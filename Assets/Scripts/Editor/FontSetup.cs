using System.Collections.Generic;
using System.IO;
using ColorMergeExit.Game;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Sets up TextMeshPro for global text: imports TMP essentials, builds dynamic
    /// TMP font assets from the downloaded TTFs, and wires Pretendard (Latin/Cyrillic/
    /// Hangul) as primary with Noto SC/JP/Arabic as fallbacks so CJK + Arabic glyphs
    /// resolve automatically. Font assets go to Resources/Fonts for runtime loading.
    ///
    /// Run in two batch steps (essentials import needs its own domain reload):
    ///   -executeMethod ColorMergeExit.Editor.FontSetup.ImportEssentials
    ///   -executeMethod ColorMergeExit.Editor.FontSetup.CreateFonts
    /// </summary>
    public static class FontSetup
    {
        private const string FontDir = "Assets/Art/Fonts/";
        private const string OutDir = "Assets/Resources/Fonts/";

        public static void ImportEssentials()
        {
            if (Directory.Exists("Assets/TextMesh Pro"))
            {
                Debug.Log("[Color Merge Exit] TMP essentials already present.");
                return;
            }

            string pkg = FindEssentialsPackage();
            if (pkg == null) { Debug.LogError("[Color Merge Exit] TMP essentials unitypackage not found."); return; }

            Debug.Log("[Color Merge Exit] Importing TMP essentials from: " + pkg);
            AssetDatabase.ImportPackage(pkg, false);
            AssetDatabase.Refresh();
            Debug.Log("[Color Merge Exit] TMP essentials import requested.");
        }

        private static string FindEssentialsPackage()
        {
            if (Directory.Exists("Library/PackageCache"))
            {
                var hits = Directory.GetFiles("Library/PackageCache",
                    "TMP Essential Resources.unitypackage", SearchOption.AllDirectories);
                if (hits.Length > 0) return hits[0];
            }
            string builtIn = EditorApplication.applicationContentsPath +
                "/Resources/PackageManager/BuiltInPackages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
            return File.Exists(builtIn) ? builtIn : null;
        }

        public static void CreateFonts()
        {
            Directory.CreateDirectory(OutDir);

            var pretendard = CreateFontAsset("Pretendard-Regular.ttf", "Pretendard SDF");
            var sc = CreateFontAsset("NotoSansSC.ttf", "NotoSansSC SDF");
            var jp = CreateFontAsset("NotoSansJP.ttf", "NotoSansJP SDF");
            var ar = CreateFontAsset("NotoSansArabic.ttf", "NotoSansArabic SDF");

            if (pretendard != null)
            {
                pretendard.fallbackFontAssetTable = new List<TMP_FontAsset>();
                foreach (var fb in new[] { sc, jp, ar })
                    if (fb != null) pretendard.fallbackFontAssetTable.Add(fb);
                EditorUtility.SetDirty(pretendard);
                Debug.Log($"[Color Merge Exit] Pretendard fallbacks: {pretendard.fallbackFontAssetTable.Count}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Color Merge Exit] Font assets created.");
        }

        /// <summary>Build the playful display font (Titan One) used for the HUD/timer/stage
        /// numbers, with Pretendard as a fallback for any glyph it lacks.</summary>
        public static void CreateGameFont()
        {
            Directory.CreateDirectory(OutDir);
            var game = CreateFontAsset("TitanOne-Regular.ttf", "Game SDF");
            if (game != null)
            {
                var pretendard = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutDir + "Pretendard SDF.asset");
                if (pretendard != null)
                    game.fallbackFontAssetTable = new List<TMP_FontAsset> { pretendard };
                EditorUtility.SetDirty(game);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Color Merge Exit] Game display font created.");
        }

        /// <summary>
        /// Subset the font assets for shipping: bake ONLY the glyphs the game actually
        /// uses (all localized strings incl. Arabic presentation forms, ASCII, digits)
        /// into static atlases, then drop the source-TTF reference so the multi-MB CJK
        /// TTFs are excluded from the build. Re-run CreateFonts to re-link for editing.
        /// </summary>
        public static void OptimizeFonts()
        {
            var set = new System.Collections.Generic.HashSet<char>();
            for (int c = 0x20; c <= 0x7E; c++) set.Add((char)c); // ASCII (English UI, digits, punct)
            set.Add('★'); set.Add('☆');
            foreach (var s in Localization.AllDisplayStrings())
                foreach (var ch in s) set.Add(ch);
            var arr = new char[set.Count];
            set.CopyTo(arr);
            string chars = new string(arr);
            Debug.Log($"[Color Merge Exit] subsetting to {chars.Length} unique glyphs.");

            foreach (var name in new[] { "Pretendard SDF", "Game SDF", "NotoSansSC SDF", "NotoSansJP SDF", "NotoSansArabic SDF" })
            {
                var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutDir + name + ".asset");
                if (fa == null) { Debug.LogWarning("[Color Merge Exit] missing font asset: " + name); continue; }

                fa.atlasPopulationMode = AtlasPopulationMode.Dynamic; // need source to rasterize
                fa.ClearFontAssetData(true);
                fa.TryAddCharacters(chars, out string _);
                fa.atlasPopulationMode = AtlasPopulationMode.Static;  // freeze; runtime uses baked atlas only

                // Drop the source TTF reference so the big font file is not built.
                var so = new SerializedObject(fa);
                var src = so.FindProperty("m_SourceFontFile");
                if (src != null) src.objectReferenceValue = null;
                var guid = so.FindProperty("m_SourceFontFileGUID");
                if (guid != null) guid.stringValue = "";
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(fa);

                int baked = fa.characterTable != null ? fa.characterTable.Count : 0;
                int atlases = fa.atlasTextures != null ? fa.atlasTextures.Length : 0;
                Debug.Log($"[Color Merge Exit] optimized {name}: baked {baked} glyphs across {atlases} atlas(es).");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Color Merge Exit] Font subsetting done — TTF source refs cleared (excluded from build).");
        }

        private static TMP_FontAsset CreateFontAsset(string ttf, string assetName)
        {
            string src = FontDir + ttf;
            var font = AssetDatabase.LoadAssetAtPath<Font>(src);
            if (font == null) { Debug.LogWarning("[Color Merge Exit] Missing font: " + src); return null; }

            string outPath = OutDir + assetName + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (existing != null) return existing;

            // Dynamic atlas so glyphs (incl. large CJK sets) rasterize on demand.
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA,
                1024, 1024, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            fa.name = assetName;

            AssetDatabase.CreateAsset(fa, outPath);

            // persist generated sub-assets
            if (fa.material != null)
            {
                fa.material.name = assetName + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, fa);
            }
            if (fa.atlasTextures != null)
            {
                for (int i = 0; i < fa.atlasTextures.Length; i++)
                {
                    fa.atlasTextures[i].name = $"{assetName} Atlas {i}";
                    AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
                }
            }
            EditorUtility.SetDirty(fa);
            Debug.Log("[Color Merge Exit] Created font asset: " + outPath);
            return fa;
        }
    }
}
