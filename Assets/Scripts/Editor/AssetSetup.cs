using System.Collections.Generic;
using System.IO;
using System.Linq;
using ColorMergeExit.Core;
using ColorMergeExit.Game;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEditorInternal;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// One-shot pipeline that turns the raw art sheets into usable sprites and a
    /// populated <see cref="GameSprites"/> asset — no manual Sprite Editor work.
    ///
    /// The main art is a single GPT sheet (<c>blocks_sheet.png</c>) laid out as a
    /// regular 6-column grid, one category per row:
    ///   Row 1 color blocks · Row 2 exit portals · Row 3 movable obstacles ·
    ///   Row 4 static walls · Row 5 tiles · Row 6 UI/effects.
    /// Because the grid is regular, we slice it on a FIXED 6xN grid and wire each
    /// sprite by its (row, col) — deterministic, no color guessing. Columns follow the
    /// CarColor order (Red,Blue,Yellow,Green,Purple,Orange). HUD icons still come from
    /// the legacy ui_buttons sheet via color classification.
    ///
    /// Run: menu Tools ▸ Color Exit ▸ Set Up Art, or
    ///   Unity -batchmode -quit -executeMethod ColorMergeExit.Editor.AssetSetup.Run
    /// </summary>
    public static class AssetSetup
    {
        private const string Dir = "Assets/Art/Textures/";
        private const string Sheet = Dir + "blocks_sheet.png";
        private const string Buttons = Dir + "ui_buttons.png";
        private const string GameSpritesPath = "Assets/Resources/GameSprites.asset";

        private const int Cols = 6;
        private const int Rows = 6;

        // Reference colors mirror VisualAssets.ToUnity (indexed by CarColor).
        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.24f, 0.24f), // Red
            new Color(0.20f, 0.48f, 0.92f), // Blue
            new Color(0.98f, 0.80f, 0.18f), // Yellow
            new Color(0.28f, 0.74f, 0.35f), // Green
            new Color(0.62f, 0.35f, 0.83f), // Purple
            new Color(0.95f, 0.55f, 0.16f), // Orange
        };

        [MenuItem("Tools/Color Exit/Set Up Art")]
        public static void Run()
        {
            SliceGrid(Sheet, "cell", Cols, Rows);
            SliceAuto(Buttons, "btn", 20);
            AssetDatabase.Refresh();

            BuildGameSprites();
            AssetDatabase.SaveAssets();
            Debug.Log("[Color Exit] Art setup complete.");
        }

        // ---------- import ----------
        private static TextureImporter ImportAsSprite(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Color Exit] No texture at {path}");
                return null;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.spritePixelsPerUnit = 256;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return importer;
        }

        // ---------- fixed-grid slicing (single sheet) ----------
        private static void SliceGrid(string path, string prefix, int cols, int rows)
        {
            var importer = ImportAsSprite(path);
            if (importer == null) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            float cw = (float)tex.width / cols, ch = (float)tex.height / rows;
            var rects = new List<Rect>(cols * rows);
            var names = new List<string>(cols * rows);
            // Row 0 is the TOP visual row; texture origin is bottom-left. Each cell is
            // trimmed to its opaque content so the sprite's bounds == the visible art,
            // letting the view fill a board cell exactly (no baked-in padding).
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    var cell = new Rect(c * cw, tex.height - (r + 1) * ch, cw, ch);
                    if (TrimToContent(tex, cell, out var tight))
                    {
                        rects.Add(tight);
                        names.Add($"{prefix}_{r}_{c}");
                    }
                }

            ApplyRects(importer, rects.ToArray(), names.ToArray());
            Debug.Log($"[Color Exit] Grid-sliced {path} -> {rects.Count} non-empty cells.");
        }

        /// <summary>Shrink a cell rect to the tight bounding box of its opaque pixels
        /// (with a 1px margin). Returns false if the cell is essentially empty.</summary>
        private static bool TrimToContent(Texture2D tex, Rect cell, out Rect tight)
        {
            int cx = Mathf.RoundToInt(cell.x), cy = Mathf.RoundToInt(cell.y);
            int cw = Mathf.RoundToInt(cell.width), chh = Mathf.RoundToInt(cell.height);
            cw = Mathf.Min(cw, tex.width - cx);
            chh = Mathf.Min(chh, tex.height - cy);
            var px = tex.GetPixels(cx, cy, cw, chh);

            int minX = cw, minY = chh, maxX = -1, maxY = -1, count = 0;
            for (int j = 0; j < chh; j++)
                for (int i = 0; i < cw; i++)
                    if (px[j * cw + i].a > 0.25f)
                    {
                        count++;
                        if (i < minX) minX = i; if (i > maxX) maxX = i;
                        if (j < minY) minY = j; if (j > maxY) maxY = j;
                    }

            if (maxX < 0 || count < 16) { tight = default; return false; }
            minX = Mathf.Max(0, minX - 1); minY = Mathf.Max(0, minY - 1);
            maxX = Mathf.Min(cw - 1, maxX + 1); maxY = Mathf.Min(chh - 1, maxY + 1);
            tight = new Rect(cx + minX, cy + minY, maxX - minX + 1, maxY - minY + 1);
            return true;
        }

        // ---------- auto slicing (legacy button sheet) ----------
        private static void SliceAuto(string path, string prefix, int minSize)
        {
            var importer = ImportAsSprite(path);
            if (importer == null) return;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            Rect[] rects = InternalSpriteUtility.GenerateAutomaticSpriteRectangles(tex, minSize, 0);
            rects = SortRowMajor(rects);
            var names = new string[rects.Length];
            for (int i = 0; i < rects.Length; i++) names[i] = $"{prefix}_{i:00}";
            ApplyRects(importer, rects, names);
            Debug.Log($"[Color Exit] Auto-sliced {path} -> {rects.Length} sprites.");
        }

        private static void ApplyRects(TextureImporter importer, Rect[] rects, string[] names)
        {
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(importer);
            dp.InitSpriteEditorDataProvider();

            var spriteRects = new SpriteRect[rects.Length];
            var pairs = new List<SpriteNameFileIdPair>(rects.Length);
            for (int i = 0; i < rects.Length; i++)
            {
                var sr = new SpriteRect
                {
                    name = names[i],
                    rect = rects[i],
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = SpriteAlignment.Center,
                    spriteID = GUID.Generate(),
                };
                spriteRects[i] = sr;
                pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
            }

            dp.SetSpriteRects(spriteRects);
            var nameProvider = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameProvider.SetNameFileIdPairs(pairs);
            dp.Apply();
            importer.SaveAndReimport();
        }

        private static Rect[] SortRowMajor(Rect[] rects)
        {
            var sorted = rects.OrderByDescending(r => r.y).ToList();
            var rows = new List<List<Rect>>();
            foreach (var r in sorted)
            {
                var row = rows.FirstOrDefault(g => Mathf.Abs(g[0].y - r.y) < r.height * 0.5f);
                if (row == null) { row = new List<Rect>(); rows.Add(row); }
                row.Add(r);
            }
            return rows.SelectMany(g => g.OrderBy(r => r.x)).ToArray();
        }

        // ---------- wiring ----------
        private static void BuildGameSprites()
        {
            if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");

            var gs = AssetDatabase.LoadAssetAtPath<GameSprites>(GameSpritesPath);
            if (gs == null)
            {
                gs = ScriptableObject.CreateInstance<GameSprites>();
                AssetDatabase.CreateAsset(gs, GameSpritesPath);
            }
            gs.block = new Sprite[6];
            gs.exitByColor = new Sprite[6];
            gs.movableObstacles = new Sprite[6];
            gs.staticWalls = new Sprite[6];
            gs.tiles = new Sprite[6];

            var cells = LoadSprites(Sheet).ToDictionary(s => s.name, s => s);
            Sprite Cell(int r, int c) => cells.TryGetValue($"cell_{r}_{c}", out var s) ? s : null;

            for (int c = 0; c < 6; c++)
            {
                gs.block[c] = Cell(0, c);         // Row 1: color blocks
                gs.exitByColor[c] = Cell(1, c);   // Row 2: exit portals
                gs.movableObstacles[c] = Cell(2, c); // Row 3: movable obstacles
                gs.staticWalls[c] = Cell(3, c);   // Row 4: static walls
                gs.tiles[c] = Cell(4, c);         // Row 5: tiles
            }
            gs.obstacle = gs.movableObstacles[0];

            // Row 6: UI / effects (pause, next, close, sound, star, sparkle)
            gs.btnPause = Cell(5, 0);
            gs.btnNext = Cell(5, 1);
            gs.btnClose = Cell(5, 2);
            gs.btnSound = Cell(5, 3);
            gs.sparkle = Cell(5, 5);

            int wired = gs.block.Count(s => s != null);
            Debug.Log($"[Color Exit] sheet wired: blocks={wired}/6, exits={gs.exitByColor.Count(s => s != null)}/6, " +
                      $"movable={gs.movableObstacles.Count(s => s != null)}/6, walls={gs.staticWalls.Count(s => s != null)}/6");

            WireHudButtons(gs);

            EditorUtility.SetDirty(gs);
        }

        // HUD icons from the legacy ui_buttons sheet (restart is the only red icon,
        // undo the only purple one → color classification picks them uniquely).
        private static void WireHudButtons(GameSprites gs)
        {
            var btns = LoadSprites(Buttons);
            if (btns.Length == 0) return; // legacy sheet optional

            gs.btnRestart = null; gs.btnUndo = null;
            float bestRed = -1f, bestPurple = -1f;
            foreach (var s in btns)
            {
                var (color, colorful) = Classify(s);
                if (colorful < 0.05f) continue;
                if (color == CarColor.Red && colorful > bestRed) { bestRed = colorful; gs.btnRestart = s; }
                if (color == CarColor.Purple && colorful > bestPurple) { bestPurple = colorful; gs.btnUndo = s; }
            }
            gs.btnHome = btns.FirstOrDefault(s => s.name == "btn_09");
            gs.btnSettings = btns.FirstOrDefault(s => s.name == "btn_06");
            gs.star = btns.FirstOrDefault(s => s.name == "btn_07");
            Debug.Log($"[Color Exit] buttons: restart={gs.btnRestart?.name ?? "none"}, undo={gs.btnUndo?.name ?? "none"}, " +
                      $"home={gs.btnHome?.name ?? "none"}, star={gs.star?.name ?? "none"}");
        }

        private static Sprite[] LoadSprites(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        }

        /// <summary>Classify a sprite to the nearest palette color by HUE; also return how
        /// colorful it is (fraction of opaque, saturated pixels).</summary>
        private static (CarColor color, float colorful) Classify(Sprite s)
        {
            var tex = s.texture;
            var r = s.rect;
            var px = tex.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);

            float sumSin = 0, sumCos = 0;
            int colorfulCount = 0, opaque = 0;
            foreach (var c in px)
            {
                if (c.a < 0.5f) continue;
                opaque++;
                Color.RGBToHSV(c, out float hue, out float sat, out float val);
                if (sat > 0.35f && val > 0.2f)
                {
                    float ang = hue * 2f * Mathf.PI;
                    sumSin += Mathf.Sin(ang) * sat;
                    sumCos += Mathf.Cos(ang) * sat;
                    colorfulCount++;
                }
            }
            float colorful = opaque > 0 ? (float)colorfulCount / opaque : 0f;
            if (colorfulCount == 0) return (CarColor.Red, 0f);

            float avgHue = Mathf.Repeat(Mathf.Atan2(sumSin, sumCos) / (2f * Mathf.PI), 1f);
            int best = 0; float bestDist = float.MaxValue;
            for (int i = 0; i < Palette.Length; i++)
            {
                Color.RGBToHSV(Palette[i], out float refHue, out _, out _);
                float d = Mathf.Abs(avgHue - refHue);
                d = Mathf.Min(d, 1f - d);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return ((CarColor)best, colorful);
        }
    }
}
