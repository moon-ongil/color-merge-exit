using System.IO;
using ColorMergeExit.Core;
using ColorMergeExit.Game;
using UnityEditor;
using UnityEngine;

namespace ColorMergeExit.Editor
{
    /// <summary>
    /// Renders the default level to a PNG without entering Play mode, so the sprite
    /// wiring can be verified visually from the command line. Path is passed via the
    /// COLOREXIT_SHOT env var (falls back to Assets/../shot.png).
    /// </summary>
    public static class Screenshot
    {
        public static void CaptureMain()
        {
            int W = 1080, H = 1920;
            string path = System.Environment.GetEnvironmentVariable("COLOREXIT_SHOT");
            if (string.IsNullOrEmpty(path)) path = "shot.png";

            var locEnv = System.Environment.GetEnvironmentVariable("COLOREXIT_LOCALE");
            if (!string.IsNullOrEmpty(locEnv) && System.Enum.TryParse<Locale>(locEnv, true, out var loc))
                Localization.SetLocale(loc);

            var sprites = Resources.Load<GameSprites>("GameSprites");
            int levelId = 1;
            var idEnv = System.Environment.GetEnvironmentVariable("COLOREXIT_LEVEL");
            if (!string.IsNullOrEmpty(idEnv)) int.TryParse(idEnv, out levelId);
            var level = LevelRepository.Load(levelId);
            var session = new GameSession(level);

            var root = new GameObject("ShotRoot");
            try
            {
                var camGo = new GameObject("ShotCam");
                camGo.transform.SetParent(root.transform);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
                cam.transform.position = new Vector3(0f, 0f, -10f);

                var boardGo = new GameObject("Board");
                boardGo.transform.SetParent(root.transform);
                var bv = boardGo.AddComponent<BoardView>();
                bv.Build(session.Board, sprites);

                // Optional: show a vehicle mid-drag (continuous offset) for verification.
                var dragEnv = System.Environment.GetEnvironmentVariable("COLOREXIT_DRAGOFF");
                if (!string.IsNullOrEmpty(dragEnv) && float.TryParse(dragEnv, out float doff))
                    bv.SetDragOffset(1, doff, 0f);

                // Optional: emit + simulate a particle burst for verification.
                if (System.Environment.GetEnvironmentVariable("COLOREXIT_PARTICLES") == "1")
                {
                    var ps = ParticleBurst.Emit(bv.CellCenterWorld(5, 2) + new Vector3(0.6f, 0f, 0f),
                        VisualAssets.ToUnity(CarColor.Red), 45, 8f, 0.3f, 1f);
                    ps.Simulate(0.3f, true, true);
                }

                // frame camera (mirrors GameController.FrameCamera)
                float aspect = (float)W / H;
                float sizeH = session.Board.Height * 0.5f + 1.9f;
                float sizeW = (session.Board.Width * 0.5f + 1.0f) / aspect;
                cam.orthographicSize = Mathf.Max(sizeH, sizeW);
                cam.aspect = aspect;

                var hudGo = new GameObject("HUD");
                hudGo.transform.SetParent(root.transform);
                var hud = hudGo.AddComponent<HudView>();
                hud.Build(session.Board.Width, session.Board.Height, sprites, levelId, cam.orthographicSize, level.timeLimitSeconds);
                hud.SetTime(level.timeLimitSeconds);

                // Optional: show localized banner text (verifies subsetted CJK/Arabic glyphs).
                if (System.Environment.GetEnvironmentVariable("COLOREXIT_BANNER") == "1")
                    hud.ShowBanner(Localization.Get(LocKeys.Memorize) + "\n" + Localization.Get(LocKeys.Clear), Color.white);

                // Optional: open the color-mix info popup for verification.
                if (System.Environment.GetEnvironmentVariable("COLOREXIT_INFO") == "1")
                    hud.ToggleInfo();
                // Optional: open the settings popup for verification.
                if (System.Environment.GetEnvironmentVariable("COLOREXIT_SETTINGS") == "1")
                    hud.ToggleSettings();

                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;

                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[Color Exit] Screenshot saved: {Path.GetFullPath(path)} " +
                          $"(sprites asset {(sprites != null ? "FOUND" : "MISSING")})");

                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(tex);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Capture several levels in one editor session (COLOREXIT_LEVELS="150,300,500",
        /// COLOREXIT_SHOTDIR=output dir). Avoids paying Unity's cold-start per level.</summary>
        public static void CaptureBatch()
        {
            var list = System.Environment.GetEnvironmentVariable("COLOREXIT_LEVELS");
            var dir = System.Environment.GetEnvironmentVariable("COLOREXIT_SHOTDIR");
            if (string.IsNullOrEmpty(dir)) dir = ".";
            if (string.IsNullOrEmpty(list)) list = "1";
            foreach (var tok in list.Split(','))
            {
                if (!int.TryParse(tok.Trim(), out int lvl)) continue;
                System.Environment.SetEnvironmentVariable("COLOREXIT_LEVEL", lvl.ToString());
                System.Environment.SetEnvironmentVariable("COLOREXIT_SHOT",
                    Path.Combine(dir, $"shot_{lvl:D3}.png"));
                CaptureMain();
            }
        }

        /// <summary>Render the stage-select screen (simulates progress for the shot).</summary>
        public static void CaptureSelect()
        {
            int W = 1080, H = 1920;
            string path = System.Environment.GetEnvironmentVariable("COLOREXIT_SHOT");
            if (string.IsNullOrEmpty(path)) path = "shot.png";
            var sprites = Resources.Load<GameSprites>("GameSprites");

            PlayerPrefs.SetInt("unlocked", 12);
            for (int i = 1; i <= 11; i++)
            {
                PlayerPrefs.SetInt($"lvl.{i}.stars", (i % 3) + 1);
                PlayerPrefs.SetFloat($"lvl.{i}.best", 12f + (i * 7 % 44));
            }

            var root = new GameObject("ShotRoot");
            try
            {
                var camGo = new GameObject("ShotCam");
                camGo.transform.SetParent(root.transform);
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
                cam.aspect = (float)W / H;

                var selGo = new GameObject("LevelSelect");
                selGo.transform.SetParent(root.transform);
                var sel = selGo.AddComponent<LevelSelectView>();
                sel.Build(cam, sprites, _ => { });
                sel.Show();

                RenderToPng(cam, W, H, path);
                Debug.Log($"[Color Exit] Select screenshot saved: {Path.GetFullPath(path)}");
            }
            finally
            {
                Object.DestroyImmediate(root);
                ProgressStore.ResetAll(); // don't leave simulated progress behind
            }
        }

        private static void RenderToPng(Camera cam, int W, int H, string path)
        {
            cam.aspect = (float)W / H;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
