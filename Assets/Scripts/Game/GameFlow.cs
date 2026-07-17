using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Top-level app flow: builds the camera/audio and switches between the stage
    /// select screen and a reusable <see cref="GameController"/>. Clearing a level
    /// unlocks and advances to the next; Home returns to select.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        private Camera _cam;
        private GameSprites _sprites;
        private GameObject _gameGo, _hudGo, _selectGo;
        private GameController _game;
        private LevelSelectView _select;
        private int _currentLevel = 1;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            StartCoroutine(GatherConsentThenInitAds());   // ATT + UMP consent BEFORE ads (see method)
            Analytics.Init();   // Firebase (no-op unless the SDK is imported + FIREBASE_ANALYTICS defined)
            ProgressStore.SyncFromCloud();   // pull/merge iCloud progress (no-op off signed iOS)

            // Restore the language chosen in the settings popup (default English). NOTE: the shipped
            // font atlas is subsetted to the baked (mostly English) glyph set, so non-Latin locales
            // tofu until FontSetup.OptimizeFonts is re-run with source TTFs. The tutorial is now fully
            // visual (animated hand demos, no text), so it reads the same in any language.
            Localization.SetLocale((Locale)PlayerPrefs.GetInt("locale", (int)Locale.En));

            _cam = CreateCamera();
            new GameObject("Audio").AddComponent<AudioManager>();
            _sprites = Resources.Load<GameSprites>("GameSprites");

            _hudGo = new GameObject("HUD");
            var hud = _hudGo.AddComponent<HudView>();

            _gameGo = new GameObject("Game");
            _gameGo.AddComponent<BoardView>();
            _game = _gameGo.AddComponent<GameController>();
            _game.Configure(_cam, hud, _sprites, onHome: ShowSelect, onNext: NextLevel);

            _selectGo = new GameObject("LevelSelect");
            _select = _selectGo.AddComponent<LevelSelectView>();
            _select.Build(_cam, _sprites, PlayLevel);

            // Dev/QA: boot straight into a level when COLOREXIT_BOOTLEVEL is set (pass via
            // `SIMCTL_CHILD_COLOREXIT_BOOTLEVEL=N xcrun simctl launch ...`). Unset in production.
            var boot = System.Environment.GetEnvironmentVariable("COLOREXIT_BOOTLEVEL");
            if (!string.IsNullOrEmpty(boot) && int.TryParse(boot, out int bl) && bl >= 1)
                PlayLevel(bl);
            else
                ShowSelect();
        }

        // Show the iOS ATT prompt immediately, gather UMP (GDPR) consent, THEN initialize the ads SDK so
        // the first ad request already carries the right consent. The UMP step has a short timeout so a
        // no-op UMP (e.g. the simulator stubs) can never block ad initialization.
        private System.Collections.IEnumerator GatherConsentThenInitAds()
        {
            Att.Request(null);   // iOS ATT prompt right away (no-op off iOS)

            bool umpDone = false;
            Consent.RequestUmp(() => umpDone = true);
            float t = 0f;
            while (!umpDone && t < 3f) { t += Time.unscaledDeltaTime; yield return null; }

            AdManager.Initialize();
        }

        public void ShowSelect()
        {
            AdManager.HideBanner();
            _gameGo.SetActive(false);
            _hudGo.SetActive(false);
            _selectGo.SetActive(true);
            _select.Show();
        }

        public void PlayLevel(int levelId)
        {
            _currentLevel = levelId;
            AdManager.ShowBanner();
            _selectGo.SetActive(false);
            _gameGo.SetActive(true);
            _hudGo.SetActive(true);
            _game.Play(levelId);
        }

        private void NextLevel()
        {
            AdManager.NotifyLevelComplete(_currentLevel); // interstitial every few cleared levels (skips early ones)
            int next = _currentLevel + 1;
            if (next <= ProgressStore.TotalLevels && ProgressStore.IsUnlocked(next))
                PlayLevel(next);
            else
                ShowSelect();
        }

        private Camera CreateCamera()
        {
            var existing = Camera.main;
            if (existing != null) { EnsureAudioListener(existing.gameObject); return existing; }

            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            // Unity outputs NO audio at all if the scene has no AudioListener. The game builds its
            // scene entirely in code (no listener in Main.unity), so without this the app is silent
            // on device AND simulator — regardless of the AudioSession/mute-switch fix.
            EnsureAudioListener(go);
            return cam;
        }

        private static void EnsureAudioListener(GameObject go)
        {
            if (go.GetComponent<AudioListener>() == null) go.AddComponent<AudioListener>();
        }
    }
}
