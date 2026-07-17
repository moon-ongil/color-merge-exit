using ColorMergeExit.Core;
using TMPro;
using UnityEngine;

namespace ColorMergeExit.Game
{
    public enum HudButton { None, Restart, Undo, Home, Hint, AddTime, ForceSplit, Settings, Info }

    /// <summary>
    /// World-space HUD: a top header (stage label + large timer + depleting time bar),
    /// a center result banner, and bottom icon buttons. Elements are anchored to the
    /// camera's vertical extent so the tall empty margins on modern phones are filled.
    ///
    /// All sprites/text are built through the shared <see cref="Ui"/> toolkit; colours come from
    /// <see cref="Palette"/>, sort orders from <see cref="Sorting"/>, and every tappable dialog
    /// button is a <see cref="UiButton"/> so its touch box is derived from how it's drawn.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        // Parent-bound forwarders to the shared toolkit (HUD-owned elements live under this transform).
        private SpriteRenderer Sprite(string name, Vector3 pos, Color color, int order, Sprite sprite = null)
            => Ui.Sprite(transform, name, pos, color, order, sprite);
        private TMP_Text Text(string name, Vector3 pos, float size, int order = Sorting.Text)
            => Ui.Text(transform, name, pos, size, order);

        private TMP_Text _timer;
        private TMP_Text _banner;
        private SpriteRenderer _bannerBg;   // dark toast behind the banner so text reads over any block
        private Transform _restartBtn, _undoBtn, _homeBtn, _settingsBtn;
        private Transform _hintBtn, _addTimeBtn, _forceSplitBtn;
        private SpriteRenderer _hintSr, _addTimeSr, _forceSplitSr;        // button body (tint)
        private SpriteRenderer _hintGlyph, _addTimeGlyph, _forceSplitGlyph;
        private SpriteRenderer _hintLock, _addTimeLock, _forceSplitLock;  // padlock shown when locked
        private TMP_Text _hintCountText, _addTimeCountText, _forceSplitCountText;
        private TMP_Text _itemsLabel;
        private Transform _timerFill;
        private GameSprites _sprites;
        private Vector2 _btnSize = new Vector2(1.4f, 1.4f);

        // Settings popup
        public enum SettingsHit { None, Sound, Language, Close, Backdrop, Restart, Exit }
        private GameObject _settingsRoot;
        private UiButton _soundBtn, _closeBtn, _restartMenuBtn, _exitMenuBtn;
        private Transform _langBtn;
        private TMP_Text _settingsTitle, _langLabel, _soundValue, _langValue, _closeLabel;
        private TMP_Text _soundLabel;
        private Vector3 _popupCenter;

        // Color-mix info popup (a cheat-sheet of the merge/split recipes)
        private GameObject _infoRoot;
        private UiButton _infoCloseBtn;
        private Transform _infoBtn;
        private Vector3 _infoCenter;

        public const int AddTimeSeconds = 15;

        private float _maxTime = 1f, _barW = 6f, _barY;
        private float _bottomBannerWorld;   // screen-bottom space taken by the native AdMob banner (world units)

        public void Build(int boardWidth, int boardHeight, GameSprites sprites,
            int levelId, float camHalfHeight, float maxTime, float bottomBannerWorld = 0f)
        {
            _sprites = sprites;
            _maxTime = Mathf.Max(1f, maxTime);
            _camHalf = camHalfHeight;
            _bottomBannerWorld = Mathf.Max(0f, bottomBannerWorld);
            _resultRoot = null; _confirmRoot = null;   // cleared with the children below

            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            // Anchor the header (label + timer + bar) JUST ABOVE the board rather than the screen top,
            // so on tall phones the extra space becomes a balanced top margin and the board doesn't
            // feel stuck to the top. Capped so it never runs off the top of very short screens.
            _barW = boardWidth;
            float boardTop = boardHeight * 0.5f;
            _barY = boardTop + 1.5f;   // extra gap between the timer bar and the top-row exit doors
            float topY = Mathf.Min(_barY + 2.35f, camHalfHeight - 1.7f);
            // On wide framings (tablets) camHalfHeight is width-driven and large, so the top cap above
            // would drag the whole header stack DOWN until the timer bar lands on the board's top row.
            // Floor topY so the bar always keeps a real gap above the board (it only lifts the header
            // when the cap would otherwise overlap; on tall phones this is a no-op).
            const float MinBarGapAboveBoard = 0.9f;
            topY = Mathf.Max(topY, boardTop + MinBarGapAboveBoard + 2.35f);
            _barY = topY - 2.35f;

            // Header: prominent stage label
            var label = Text("StageLabel", new Vector3(0f, topY + 0.2f, 0f), Typography.Title);
            label.text = $"STAGE {levelId}";
            label.color = Palette.TextDark; // dark for the bright background
            // Auto-size down only if a wide number (STAGE 1000) would reach the corner nav buttons.
            label.enableAutoSizing = true; label.fontSizeMax = Typography.Title; label.fontSizeMin = 4f;
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, _barW - 0.4f), 1.8f);

            // Timer (big and legible — the main pressure readout). Nudged up now that the hearts row
            // is gone from the in-play HUD (lives still gate play from the level map).
            _timer = Text("Timer", new Vector3(0f, topY - 1.35f, 0f), Typography.Readout);
            _timer.text = "";

            // Depleting time bar (chunky rounded CAPSULE via 9-slice so the ends stay round)
            var bg = Sprite("TimerBarBg", new Vector3(0f, _barY, 0f), new Color(0.74f, 0.78f, 0.88f), Sorting.HudBar, VisualAssets.RoundedSquare());
            Ui.Sliced(bg, _barW, 0.4f);
            var fillSr = Sprite("TimerBarFill", new Vector3(0f, _barY, -0.05f), new Color(0.35f, 0.75f, 0.5f), Sorting.HudBarFill, VisualAssets.RoundedSquare());
            _timerFill = fillSr.transform;
            Ui.Sliced(fillSr, _barW, 0.4f);

            // (Hearts removed from the in-play HUD per design — lives still gate play from the level map.)

            // Center banner — a big, bold state message (TIME UP / CLEAR! / NO WAY OUT!). Auto-sizes
            // so short messages render huge while long ones (e.g. "TAP A BLOCK TO SPLIT") shrink to fit.
            _banner = Text("Banner", new Vector3(0f, 0.2f, 0f), Typography.Display);
            _banner.fontStyle = FontStyles.Bold;
            // Single line: no wrapping — autosize shrinks the font to fit the toast width instead of
            // wrapping to two lines (e.g. "NO WAY OUT! 3" stays on one line).
            _banner.enableWordWrapping = false;
            _banner.rectTransform.sizeDelta = new Vector2(Mathf.Max(6f, _barW - 0.2f), 6f);   // narrower than the toast bg → side padding
            _banner.enableAutoSizing = true;
            _banner.fontSizeMin = Typography.Body;
            _banner.fontSizeMax = Typography.Display;
            // Bold dark outline so the message stays legible over the busy, light-coloured board
            // (the plain coral/gold fills washed out against the grid + blocks).
            _banner.fontMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
            _banner.fontMaterial.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.12f, 0.10f, 0.20f, 1f));
            _banner.text = "";
            // Dark translucent toast BEHIND the banner (just below its text order) so the message +
            // countdown read clearly even when blocks sit behind them.
            _bannerBg = Sprite("BannerBg", new Vector3(0f, 0.2f, 0.05f), new Color(0.12f, 0.10f, 0.20f, 0.60f), Sorting.Text - 1, VisualAssets.RoundedSquare());
            Ui.Sliced(_bannerBg, _barW + 0.8f, 2.8f);
            _bannerBg.gameObject.SetActive(false);
            _banner.gameObject.SetActive(false);

            // ---- Top corners: game-independent nav. Left column = Info (top) then Home (below);
            // right column = Settings (top). Restart lives INSIDE the settings popup now, so the
            // bottom-right corner stays clear. ----
            _btnSize = new Vector2(1.0f, 1.0f);    // smaller nav buttons so the stage label reads bigger
            _undoBtn = null; _restartBtn = null; _homeBtn = null;  // restart + home moved into the settings menu
            float topBtnX = _barW * 0.5f + 0.05f;  // sit just above the board's top corners
            float topBtnY = topY + 0.15f;          // level with the stage label
            Vector3 infoPos = new Vector3(-topBtnX, topBtnY, 0f);
            Vector3 settingsPos = new Vector3(topBtnX, topBtnY, 0f);

            // INFO (top-left): opens the color-mix cheat-sheet. Uppercase "i" reads cleaner/centred.
            _infoBtn = MakeButton("InfoBtn", infoPos, "", VisualAssets.GlossyCircle());
            _infoBtn.GetComponent<SpriteRenderer>().color = Palette.BlueBright;
            var iGlyph = Text("InfoGlyph", infoPos + new Vector3(0f, 0f, -0.1f), 4.2f);
            iGlyph.text = "i";
            iGlyph.rectTransform.sizeDelta = new Vector2(1.2f, 1.2f);
            iGlyph.color = Color.white;

            // SETTINGS (top-right): sound toggle + restart + exit, inside the popup.
            _settingsBtn = MakeButton("SettingsBtn", settingsPos, "SET", VisualAssets.GlossyCircle());
            _settingsBtn.GetComponent<SpriteRenderer>().color = Palette.Slate; // soft slate-indigo
            var gearGlyph = Sprite("GearGlyph", settingsPos + new Vector3(0f, 0f, -0.1f), Color.white, Sorting.HudGlyph, VisualAssets.GearIcon());
            var gb = gearGlyph.sprite.bounds.size;
            gearGlyph.transform.localScale = new Vector3(0.68f / gb.x, 0.68f / gb.y, 1f);

            // ---- Bottom: the single ITEMS row (consumable power-ups) ----
            // Items hug just under the board (not pinned to the screen bottom) so the board+items
            // read as one centred cluster instead of leaving a big empty gap between them.
            float itemY = Mathf.Max(-(boardHeight * 0.5f) - 2.3f, -(camHalfHeight - 1.6f));
            // Keep the whole item (button + its bottom-right count/padlock badge, which hangs ~0.94
            // below the centre) clear of the native AdMob banner at the screen bottom. Without this the
            // padlock badges render behind the banner on devices with a tall (tablet) banner.
            if (_bottomBannerWorld > 0f)
            {
                const float ItemReachBelow = 0.98f;   // badge/padlock extent below the button centre
                const float BannerMargin = 0.45f;
                float itemFloorY = -camHalfHeight + _bottomBannerWorld + ItemReachBelow + BannerMargin;
                itemY = Mathf.Max(itemY, itemFloorY);
            }
            // All three items share ONE rendering path (procedural glossy button + white glyph) so
            // they're pixel-identical in size — hint (bulb), +time (clock), split (4-way arrows).
            // Counts come from the persistent ItemStore; locked items grey out with a padlock badge.
            (_hintBtn, _hintSr, _hintGlyph, _hintLock, _hintCountText) =
                MakeItemButton("HintBtn", new Vector3(-2.0f, itemY, 0f), VisualAssets.BulbIcon(), new Color(0.98f, 0.74f, 0.24f));
            (_addTimeBtn, _addTimeSr, _addTimeGlyph, _addTimeLock, _addTimeCountText) =
                MakeItemButton("AddTimeBtn", new Vector3(0f, itemY, 0f), VisualAssets.ClockIcon(), new Color(0.30f, 0.74f, 0.52f));
            (_forceSplitBtn, _forceSplitSr, _forceSplitGlyph, _forceSplitLock, _forceSplitCountText) =
                MakeItemButton("SplitBtn", new Vector3(2.0f, itemY, 0f), VisualAssets.SplitIcon(), new Color(0.15f, 0.72f, 0.68f));
            RefreshItems();
            _itemsLabel = MakeCaption("ItemsLabel", new Vector3(0f, itemY + 1.2f, 0f), Localization.Get(LocKeys.Items));

            // (No placeholder band here: the real native AdMob banner renders at the screen bottom.)

            BuildSettingsPopup(camHalfHeight);
            BuildInfoPopup(camHalfHeight);
        }

        public int HintCount => ItemStore.Count(ItemType.Hint);
        public int AddTimeCount => ItemStore.Count(ItemType.AddTime);
        public int ForceSplitCount => ItemStore.Count(ItemType.ForceSplit);

        public bool IsUnlocked(ItemType t) => ItemStore.IsUnlocked(t);

        // World positions of the item buttons (for tutorial coach-mark pointers).
        public Vector3 HintButtonWorld => _hintBtn != null ? _hintBtn.position : Vector3.zero;
        public Vector3 AddTimeButtonWorld => _addTimeBtn != null ? _addTimeBtn.position : Vector3.zero;
        public Vector3 TimerWorld => _timer != null ? _timer.transform.position : new Vector3(0f, _barY, 0f);
        public Vector3 ForceSplitButtonWorld => _forceSplitBtn != null ? _forceSplitBtn.position : Vector3.zero;

        // Grant extra charges (e.g. from a rewarded ad).
        public void AddHint(int n) { ItemStore.Add(ItemType.Hint, n); RefreshItemState(ItemType.Hint); }
        public void AddAddTime(int n) { ItemStore.Add(ItemType.AddTime, n); RefreshItemState(ItemType.AddTime); }
        public void AddForceSplit(int n) { ItemStore.Add(ItemType.ForceSplit, n); RefreshItemState(ItemType.ForceSplit); }

        /// <summary>Consume one hint charge; false (no change) if locked or none left.</summary>
        public bool UseHint()
        {
            if (!ItemStore.IsUnlocked(ItemType.Hint) || !ItemStore.Spend(ItemType.Hint)) return false;
            RefreshItemState(ItemType.Hint); return true;
        }

        /// <summary>Consume one +time charge; false if locked or none left.</summary>
        public bool UseAddTime()
        {
            if (!ItemStore.IsUnlocked(ItemType.AddTime) || !ItemStore.Spend(ItemType.AddTime)) return false;
            RefreshItemState(ItemType.AddTime); return true;
        }

        /// <summary>Consume one force-split charge; false if locked or none left.</summary>
        public bool UseForceSplit()
        {
            if (!ItemStore.IsUnlocked(ItemType.ForceSplit) || !ItemStore.Spend(ItemType.ForceSplit)) return false;
            RefreshItemState(ItemType.ForceSplit); return true;
        }

        /// <summary>Give a charge back (e.g. force-split found no valid target).</summary>
        public void RefundForceSplit()
        {
            ItemStore.Add(ItemType.ForceSplit, 1); RefreshItemState(ItemType.ForceSplit);
        }

        /// <summary>Repaint all three item buttons from the ItemStore (count + lock state).</summary>
        public void RefreshItems()
        {
            RefreshItemState(ItemType.Hint);
            RefreshItemState(ItemType.AddTime);
            RefreshItemState(ItemType.ForceSplit);
        }

        private void RefreshItemState(ItemType t)
        {
            SpriteRenderer body, glyph, lockSr; TMP_Text cnt;
            switch (t)
            {
                case ItemType.Hint: body = _hintSr; glyph = _hintGlyph; lockSr = _hintLock; cnt = _hintCountText; break;
                case ItemType.AddTime: body = _addTimeSr; glyph = _addTimeGlyph; lockSr = _addTimeLock; cnt = _addTimeCountText; break;
                default: body = _forceSplitSr; glyph = _forceSplitGlyph; lockSr = _forceSplitLock; cnt = _forceSplitCountText; break;
            }
            bool unlocked = ItemStore.IsUnlocked(t);
            int count = ItemStore.Count(t);
            if (lockSr != null) lockSr.gameObject.SetActive(!unlocked);
            if (cnt != null) { cnt.gameObject.SetActive(unlocked); cnt.text = $"x{count}"; }
            if (body != null) { var c = body.color; c.a = !unlocked ? 0.4f : (count > 0 ? 1f : 0.5f); body.color = c; }
            if (glyph != null) { var c = glyph.color; c.a = unlocked ? 1f : 0.55f; glyph.color = c; }
        }

        public void SetTime(float seconds)
        {
            int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
            _timer.text = $"{s / 60:0}:{s % 60:00}";
            bool low = seconds <= 10f;
            _timer.color = low ? new Color(0.90f, 0.28f, 0.30f) : Palette.TextDark;

            if (_timerFill != null)
            {
                float frac = Mathf.Clamp01(seconds / _maxTime);
                var sr = _timerFill.GetComponent<SpriteRenderer>();
                sr.size = new Vector2(Mathf.Max(0.4f, _barW * frac), 0.4f);   // sliced: keeps round ends
                _timerFill.localPosition = new Vector3(-_barW * 0.5f + _barW * frac * 0.5f, _barY, -0.05f);
                sr.color = Color.Lerp(new Color(0.92f, 0.4f, 0.32f), new Color(0.35f, 0.78f, 0.52f), frac);
            }
        }

        public void ShowBanner(string text, Color color)
        {
            _banner.text = text;
            _banner.color = color;
            _banner.gameObject.SetActive(true);
            if (_bannerBg != null) _bannerBg.gameObject.SetActive(true);
        }

        public void HideBanner()
        {
            _banner.gameObject.SetActive(false);
            if (_bannerBg != null) _bannerBg.gameObject.SetActive(false);
        }

        public HudButton ButtonAtWorld(Vector3 world)
        {
            if (_homeBtn != null && InRect(_homeBtn.position, world)) return HudButton.Home;
            if (_settingsBtn != null && InRect(_settingsBtn.position, world)) return HudButton.Settings;
            if (_infoBtn != null && InRect(_infoBtn.position, world)) return HudButton.Info;
            if (_restartBtn != null && InRect(_restartBtn.position, world)) return HudButton.Restart;
            if (_undoBtn != null && InRect(_undoBtn.position, world)) return HudButton.Undo;
            if (_hintBtn != null && InItemRect(_hintBtn.position, world)) return HudButton.Hint;
            if (_addTimeBtn != null && InItemRect(_addTimeBtn.position, world)) return HudButton.AddTime;
            if (_forceSplitBtn != null && InItemRect(_forceSplitBtn.position, world)) return HudButton.ForceSplit;
            return HudButton.None;
        }

        // ---- Result overlay (win / lose) ----
        public enum ResultButton { None, Next, Retry, Exit, Refill }
        private GameObject _resultRoot;
        private UiButton _resultA, _resultB;
        private ResultButton _btnAId, _btnBId;
        private float _camHalf = 6f;

        public bool ResultOpen => _resultRoot != null;

        /// <summary>Show the end-of-level overlay: a dim scrim, a big title, and two buttons.
        /// Win → NEXT / EXIT. Lose with hearts → RETRY / EXIT. Lose with no hearts → REFILL / EXIT.</summary>
        public void ShowResult(bool won, bool hasHearts, int stars, string title, Color titleColor)
        {
            HideBanner();   // clear any centre banner (e.g. the dead-end countdown) so it can't show through
            if (_resultRoot != null) Destroy(_resultRoot);
            _resultRoot = new GameObject("ResultOverlay");
            _resultRoot.transform.SetParent(transform, false);
            var root = _resultRoot.transform;

            var bsr = Ui.Sprite(root, "R_Back", new Vector3(0f, 0f, -1f), Palette.Scrim(0.66f), Sorting.Scrim);
            bsr.transform.localScale = new Vector3(60f, Mathf.Max(60f, _camHalf * 3f), 1f);

            float titleY = won ? 2.6f : 1.9f;
            var t = Ui.Text(root, "R_Title", new Vector3(0f, titleY, -2f), Typography.Display, Sorting.DialogText);
            t.text = title; t.color = titleColor;
            t.enableAutoSizing = true; t.fontSizeMax = Typography.Display; t.fontSizeMin = 5f;
            t.rectTransform.sizeDelta = new Vector2(8.4f, 2.4f);
            Ui.Lighten(t, -0.12f);

            // On a win, show the earned star rating (same gold/grey coin-star as the level-select).
            if (won)
            {
                var starSprite = (_sprites != null && _sprites.star != null) ? _sprites.star : VisualAssets.Star();
                for (int i = 0; i < 3; i++)
                {
                    var ssr = Ui.Sprite(root, "R_Star" + i, new Vector3((i - 1) * 1.25f, 1.0f, -2f),
                        i < stars ? Palette.StarGold : Palette.StarGrey, Sorting.DialogText, starSprite);
                    Ui.FitWidth(ssr, 1.05f);
                }
            }

            _btnAId = won ? ResultButton.Next : (hasHearts ? ResultButton.Retry : ResultButton.Refill);
            _btnBId = ResultButton.Exit;
            Color aCol = won ? Palette.Green : (hasHearts ? Palette.Blue : Palette.Orange);
            _resultA = BuildDialogButton(root, LabelFor(_btnAId), new Vector3(-1.9f, -1.05f, -2f), aCol, new Vector2(3.5f, 1.4f), Typography.Button);
            _resultB = BuildDialogButton(root, "EXIT", new Vector3(1.9f, -1.05f, -2f), Palette.Grey, new Vector2(3.5f, 1.4f), Typography.Button);
        }

        public void HideResult()
        {
            if (_resultRoot != null) { Destroy(_resultRoot); _resultRoot = null; }
        }

        public ResultButton ResultHit(Vector3 world)
        {
            if (!ResultOpen) return ResultButton.None;
            if (_resultA != null && _resultA.Contains(world)) return _btnAId;
            if (_resultB != null && _resultB.Contains(world)) return _btnBId;
            return ResultButton.None;
        }

        private static string LabelFor(ResultButton b) =>
            b == ResultButton.Next ? "NEXT" : b == ResultButton.Retry ? "RETRY" : b == ResultButton.Refill ? "REFILL" : "EXIT";

        // A dialog button (rounded pill + white label) floating on a scrim. Visual size and hit box
        // are one and the same via UiButton, so they can't drift apart.
        private UiButton BuildDialogButton(Transform root, string label, Vector3 pos, Color color, Vector2 size, float labelSize)
            => UiButton.Pill(root, "Btn_" + label, pos, size, color, label, labelSize, Color.white);

        // ---- Yes/No confirmation dialog (shared by restart + exit; both forfeit a heart) ----
        public enum ConfirmResult { None, Yes, No }
        private GameObject _confirmRoot;
        private UiButton _confYes, _confNo;
        private string _confirmId;
        public bool ConfirmOpen => _confirmRoot != null;
        /// <summary>What a Yes press should do ("restart" / "exit") — the caller owns the action.</summary>
        public string ConfirmId => _confirmId;

        public void ShowConfirm(string id, string title)
        {
            if (_confirmRoot != null) Destroy(_confirmRoot);
            _confirmId = id;
            _confirmRoot = new GameObject("ConfirmDialog");
            _confirmRoot.transform.SetParent(transform, false);
            var root = _confirmRoot.transform;

            var bsr = Ui.Sprite(root, "C_Back", new Vector3(0f, 0f, -1f), Palette.Scrim(0.6f), Sorting.Scrim);
            bsr.transform.localScale = new Vector3(60f, Mathf.Max(60f, _camHalf * 3f), 1f);

            // No white panel — title + buttons float on the dim scrim, matching the CLEAR!/result style.
            var t = Ui.Text(root, "C_Title", new Vector3(0f, 1.15f, -3f), Typography.Display, Sorting.DialogText);
            t.text = title; t.color = Color.white;
            t.enableAutoSizing = true; t.fontSizeMax = Typography.Display; t.fontSizeMin = 5f;
            t.rectTransform.sizeDelta = new Vector2(8.4f, 2.4f);
            Ui.Lighten(t, -0.1f);

            _confYes = BuildDialogButton(root, "YES", new Vector3(-1.6f, -0.55f, -3f), Palette.Green, new Vector2(3.1f, 1.35f), Typography.Button);
            _confNo = BuildDialogButton(root, "NO", new Vector3(1.6f, -0.55f, -3f), Palette.Grey, new Vector2(3.1f, 1.35f), Typography.Button);
        }

        public void HideConfirm()
        {
            if (_confirmRoot != null) { Destroy(_confirmRoot); _confirmRoot = null; }
        }

        public ConfirmResult ConfirmHit(Vector3 world)
        {
            if (!ConfirmOpen) return ConfirmResult.None;
            if (_confYes != null && _confYes.Contains(world)) return ConfirmResult.Yes;
            if (_confNo != null && _confNo.Contains(world)) return ConfirmResult.No;
            return ConfirmResult.None;
        }

        // ---- Settings popup ----
        public bool SettingsOpen => _settingsRoot != null && _settingsRoot.activeSelf;

        public void ToggleSettings()
        {
            if (_settingsRoot == null) return;
            bool open = !_settingsRoot.activeSelf;
            _settingsRoot.SetActive(open);
            if (open) RefreshSettingsValues();
        }

        public void CloseSettings()
        {
            if (_settingsRoot != null) _settingsRoot.SetActive(false);
        }

        public void ToggleSound()
        {
            AudioManager.Instance?.ToggleMute();
            RefreshSettingsValues();
        }

        public void ToggleLanguage()
        {
            var next = Localization.Current == Locale.Ko ? Locale.En : Locale.Ko;
            Localization.SetLocale(next);
            PlayerPrefs.SetInt("locale", (int)Localization.Current);
            PlayerPrefs.Save();
            RelocalizeStatic();
            RefreshSettingsValues();
        }

        // Which popup element (if any) a world point hits. Backdrop = tap outside the panel.
        public SettingsHit PopupHit(Vector3 world)
        {
            if (!SettingsOpen) return SettingsHit.None;
            if (_soundBtn != null && _soundBtn.Contains(world)) return SettingsHit.Sound;
            if (_restartMenuBtn != null && _restartMenuBtn.Contains(world)) return SettingsHit.Restart;
            if (_exitMenuBtn != null && _exitMenuBtn.Contains(world)) return SettingsHit.Exit;
            if (_closeBtn != null && _closeBtn.Contains(world)) return SettingsHit.Close;
            // inside the panel body: absorb the tap (no-op); outside: treat as backdrop -> close
            if (Ui.Hit(_popupCenter, world, 3.5f, 3.5f)) return SettingsHit.None;
            return SettingsHit.Backdrop;
        }

        private void RefreshSettingsValues()
        {
            bool muted = AudioManager.Instance != null && AudioManager.Instance.Muted;
            if (_soundValue != null) _soundValue.text = muted ? "OFF" : "ON";
            if (_langValue != null) _langValue.text = Localization.Current == Locale.Ko ? "KO" : "EN";
        }

        // Re-localize the always-on static labels (captions + popup) after a language switch.
        private void RelocalizeStatic()
        {
            if (_itemsLabel != null) _itemsLabel.text = Localization.Get(LocKeys.Items);
            if (_settingsTitle != null) _settingsTitle.text = Localization.Get(LocKeys.Settings);
            if (_soundLabel != null) _soundLabel.text = Localization.Get(LocKeys.Sound);
            if (_langLabel != null) _langLabel.text = Localization.Get(LocKeys.Language);
            if (_closeLabel != null) _closeLabel.text = Localization.Get(LocKeys.Close);
        }

        // ---- Color-mix info popup ----
        public bool InfoOpen => _infoRoot != null && _infoRoot.activeSelf;

        public void ToggleInfo()
        {
            if (_infoRoot == null) return;
            _infoRoot.SetActive(!_infoRoot.activeSelf);
        }

        public void CloseInfo()
        {
            if (_infoRoot != null) _infoRoot.SetActive(false);
        }

        // Close = tap the button; Backdrop = tap outside the panel; None = inside (absorb).
        public SettingsHit InfoPopupHit(Vector3 world)
        {
            if (!InfoOpen) return SettingsHit.None;
            if (_infoCloseBtn != null && _infoCloseBtn.Contains(world)) return SettingsHit.Close;
            if (Ui.Hit(_infoCenter, world, 3.5f, 4.9f)) return SettingsHit.None;
            return SettingsHit.Backdrop;
        }

        private void BuildInfoPopup(float camHalfHeight)
        {
            _infoCenter = new Vector3(0f, 0.3f, 0f);
            _infoRoot = new GameObject("InfoPopup");
            _infoRoot.transform.SetParent(transform, false);
            var root = _infoRoot.transform;

            var back = Ui.Sprite(root, "InfoBackdrop", new Vector3(0f, 0f, -1f), Palette.Scrim(0.5f), Sorting.Backdrop);
            back.transform.localScale = new Vector3(60f, Mathf.Max(60f, camHalfHeight * 3f), 1f);

            var psr = Ui.Sprite(root, "InfoPanel", _infoCenter + new Vector3(0f, 0f, -2f), Palette.Panel, Sorting.Panel, VisualAssets.DialogPanel());
            Ui.Sliced(psr, 6.9f, 10.3f);   // narrower than the screen → clear left/right margin

            MakeInfoText("I_Title", _infoCenter + new Vector3(0f, 4.35f, -3f), Typography.Heading,
                Localization.Get(LocKeys.HowToPlay), Palette.TextDark);
            MakeInfoText("I_MergeCap", _infoCenter + new Vector3(0f, 3.45f, -3f), 3.3f,
                Localization.Get(LocKeys.MergeHint), Palette.TextMuted);

            // Merge recipes A + B = C, laid out in a 2-column grid (8 total: 3 secondaries + 5
            // tertiaries) so the whole palette fits without a giant panel.
            var recipes = new (CarColor, CarColor, CarColor)[]
            {
                (CarColor.Red, CarColor.Blue, CarColor.Purple),
                (CarColor.Blue, CarColor.Yellow, CarColor.Green),
                (CarColor.Red, CarColor.Yellow, CarColor.Orange),
                (CarColor.Red, CarColor.Purple, CarColor.Pink),
                (CarColor.Blue, CarColor.Purple, CarColor.Indigo),
                (CarColor.Red, CarColor.Orange, CarColor.Coral),
                (CarColor.Yellow, CarColor.Orange, CarColor.Amber),
                (CarColor.Yellow, CarColor.Green, CarColor.Lime),
            };
            float[] rowY = { 2.5f, 1.2f, -0.1f, -1.4f };
            for (int r = 0; r < recipes.Length; r++)
            {
                float cx = (r % 2 == 0) ? -1.68f : 1.68f;   // pulled inward so rows sit inside the narrower panel
                var (a, b, c) = recipes[r];
                MakeRecipeRow(_infoCenter + new Vector3(cx, rowY[r / 2], -3f), a, b, c);
            }

            // Split note: a mixed block on a ◄► splitter breaks back into its two colors
            MakeInfoText("I_SplitCap", _infoCenter + new Vector3(0f, -2.62f, -3f), 3.3f,
                Localization.Get(LocKeys.SplitHint), Palette.TextMuted);

            _infoCloseBtn = UiButton.Pill(root, "I_Close", _infoCenter + new Vector3(0f, -3.95f, -3f),
                new Vector2(3.9f, 1.3f), Palette.Blue, Localization.Get(LocKeys.Close), Typography.Button, Color.white,
                Sorting.PanelButton, Sorting.PanelText);

            _infoRoot.SetActive(false);
        }

        // One compact recipe: [A] + [B] = [C]. Wider gap so the +/= symbols read clearly apart.
        private void MakeRecipeRow(Vector3 center, CarColor a, CarColor b, CarColor c)
        {
            const float dot = 0.88f, gap = 1.08f, sym = 3.0f;   // bigger dots; still fit inside the panel
            MakeColorDot(center + new Vector3(-gap, 0f, 0f), a, dot);
            MakeInfoText($"plus{center.x}{a}{b}", center + new Vector3(-gap * 0.5f, 0.02f, -1f), sym, "+", new Color(0.42f, 0.47f, 0.6f));
            MakeColorDot(center + new Vector3(0f, 0f, 0f), b, dot);
            MakeInfoText($"eq{center.x}{a}{b}", center + new Vector3(gap * 0.5f, 0.02f, -1f), sym, "=", new Color(0.42f, 0.47f, 0.6f));
            MakeColorDot(center + new Vector3(gap, 0f, 0f), c, dot);
        }

        private void MakeColorDot(Vector3 pos, CarColor color, float size = 0.9f)
        {
            var sr = Ui.Sprite(_infoRoot.transform, "Dot" + color, pos + new Vector3(0f, 0f, -1f),
                VisualAssets.ToUnity(color), Sorting.PanelIcon, VisualAssets.GlossyCircle());
            Ui.FitWidth(sr, size);
        }

        private TMP_Text MakeInfoText(string name, Vector3 pos, float size, string text, Color color)
        {
            var t = Ui.Text(_infoRoot.transform, name, pos, size, Sorting.PanelText);
            t.text = text;
            t.color = color;
            return t;
        }

        private void BuildSettingsPopup(float camHalfHeight)
        {
            _popupCenter = new Vector3(0f, 0.3f, 0f);
            _settingsRoot = new GameObject("SettingsPopup");
            _settingsRoot.transform.SetParent(transform, false);
            var root = _settingsRoot.transform;

            // Dim backdrop covering the whole screen
            var back = Ui.Sprite(root, "Backdrop", new Vector3(0f, 0f, -1f), Palette.Scrim(0.5f), Sorting.Backdrop);
            back.transform.localScale = new Vector3(60f, Mathf.Max(60f, camHalfHeight * 3f), 1f);

            // Rounded panel: title (X to close in the corner), SOUND toggle, then uniform action buttons.
            // RoundedSquare (not RoundedPanel) gives the same clearly-rounded corners as the buttons —
            // RoundedPanel's tiny corner radius reads as square at this size.
            // Clearly-rounded DialogPanel. Wide enough for the big title + X and the full-width action
            // buttons; tall enough for a generous margin below EXIT.
            var psr = Ui.Sprite(root, "Panel", _popupCenter + new Vector3(0f, 0f, -2f), Palette.Panel, Sorting.Panel, VisualAssets.DialogPanel());
            Ui.Sliced(psr, 7.0f, 7.0f);

            // Title: ALWAYS centred on the panel (independent of the X). It sits a touch below the X row
            // so the two never collide; auto-size only guards against an unexpectedly long string.
            _settingsTitle = MakePopupText("S_Title", _popupCenter + new Vector3(0f, 2.35f, -3f), Typography.Heading, Localization.Get(LocKeys.Settings), Palette.TextDark);
            _settingsTitle.enableAutoSizing = true; _settingsTitle.fontSizeMax = Typography.Heading; _settingsTitle.fontSizeMin = 3.4f;
            _settingsTitle.rectTransform.sizeDelta = new Vector2(5.6f, 1.3f);

            // CLOSE: a small X up in the top-right corner, above the centred title, with right-edge margin.
            _closeBtn = UiButton.Pill(root, "S_Close", _popupCenter + new Vector3(2.5f, 2.75f, -3f), new Vector2(1.0f, 1.0f),
                new Color(0.90f, 0.91f, 0.96f), "X", Typography.Button, Palette.TextMuted, Sorting.PanelButton, Sorting.PanelText, 0.1f);
            _closeLabel = _closeBtn.Label;

            // English-only build: no LANGUAGE row — SOUND toggle, then the uniform action buttons.
            _soundLabel = MakePopupText("S_SoundL", _popupCenter + new Vector3(-1.85f, 1.2f, -3f), Typography.Label, Localization.Get(LocKeys.Sound), new Color(0.28f, 0.32f, 0.46f));
            _soundBtn = UiButton.Pill(root, "S_SoundBtn", _popupCenter + new Vector3(1.7f, 1.2f, -3f), new Vector2(2.1f, 1.05f),
                new Color(0.88f, 0.90f, 0.97f), "", Typography.Label, new Color(0.22f, 0.26f, 0.42f), Sorting.PanelButton, Sorting.PanelText, 0f);
            _soundValue = MakePopupText("S_SoundBtn_V", _popupCenter + new Vector3(1.7f, 1.2f, -4f), Typography.Label, "", new Color(0.22f, 0.26f, 0.42f));

            // Action buttons: ONE uniform size + even spacing (via MenuButton) so the stack reads as a
            // coherent set. Colours differ by role (orange = caution, slate = exit). Generous bottom margin.
            _restartMenuBtn = MenuButton(root, "S_Restart", -0.3f, Palette.Orange, "RESTART"); // costs a heart → confirms
            _exitMenuBtn    = MenuButton(root, "S_Exit",   -1.9f, Palette.Slate,  "EXIT");     // back to the level map

            RefreshSettingsValues();
            _settingsRoot.SetActive(false);
        }

        // A uniform settings action button (identical size + label style for every row → systematic
        // stack; only the colour differs by role). One definition, so rows can't drift in size again.
        private static readonly Vector2 MenuBtnSize = new Vector2(4.6f, 1.35f);
        private UiButton MenuButton(Transform root, string name, float y, Color color, string label)
            => UiButton.Pill(root, name, _popupCenter + new Vector3(0f, y, -3f), MenuBtnSize,
                color, label, Typography.Button, Color.white, Sorting.PanelButton, Sorting.PanelText, 0.05f);

        private TMP_Text MakePopupText(string name, Vector3 pos, float size, string text, Color color)
        {
            var t = Ui.Text(_settingsRoot.transform, name, pos, size, Sorting.PanelText);
            t.text = text;
            t.color = color;
            return t;
        }

        private TMP_Text MakeCaption(string name, Vector3 pos, string text)
        {
            var t = Text(name, pos, Typography.Label);
            t.text = text;
            t.color = Palette.TextLabel;      // darker so it reads clearly
            t.characterSpacing = 2f;          // tighter = a solid label, not a thin spread
            return t;
        }

        private bool InItemRect(Vector3 center, Vector3 p) => Ui.Hit(center, p, 0.85f, 0.85f);

        // An item button: a UNIFORM procedural glossy button (tinted) + a white glyph, all the same
        // size so the three items read as one set. Always gets a count badge (bottom-right).
        private const float ItemBtnSize = 1.55f, ItemGlyphSize = 0.98f;
        private (Transform, SpriteRenderer, SpriteRenderer, SpriteRenderer, TMP_Text) MakeItemButton(
            string name, Vector3 pos, Sprite glyph, Color tint)
        {
            var sr = Sprite(name, pos, tint, Sorting.HudBar, VisualAssets.GlossyBlock());
            var bb = VisualAssets.GlossyBlock().bounds.size;
            sr.transform.localScale = new Vector3(bb.x > 0f ? ItemBtnSize / bb.x : 1f, bb.y > 0f ? ItemBtnSize / bb.y : 1f, 1f);

            var g = Sprite(name + "_G", pos + new Vector3(0f, 0.02f, -0.1f), Color.white, Sorting.HudGlyphTop, glyph);
            Ui.FitWidth(g, ItemGlyphSize);

            // count badge: a bold pill overlapping the button's bottom-right corner (app-icon style),
            // gold on dark so the "x3" reads clearly against both the button and the pastel bg.
            var badge = Sprite(name + "_B", pos + new Vector3(0.58f, -0.56f, -0.15f), new Color(0.16f, 0.18f, 0.26f), Sorting.HudGlyph, VisualAssets.RoundedSquare());
            Ui.Sliced(badge, 1.06f, 0.76f);
            var cnt = Text(name + "_C", pos + new Vector3(0.58f, -0.56f, -0.2f), Typography.Caption);
            cnt.text = "";
            cnt.color = Palette.StarGold;   // gold, high-contrast on the dark pill
            cnt.fontStyle = FontStyles.Bold;

            // padlock overlay on the badge, shown while the item is locked (before its tutorial).
            var lockSr = Sprite(name + "_L", pos + new Vector3(0.58f, -0.56f, -0.25f), new Color(1f, 0.9f, 0.4f), Sorting.HudGlyphTop, VisualAssets.Padlock());
            Ui.FitWidth(lockSr, 0.62f);
            lockSr.gameObject.SetActive(false);

            return (sr.transform, sr, g, lockSr, cnt);
        }

        private bool InRect(Vector3 center, Vector3 p) =>
            Ui.Hit(center, p, _btnSize.x * 0.5f, _btnSize.y * 0.5f);

        private Transform MakeButton(string name, Vector3 pos, string label, Sprite icon)
        {
            if (icon != null)
            {
                var sr = Sprite(name, pos, Color.white, Sorting.HudBar, icon);
                var b = icon.bounds.size;
                sr.transform.localScale = new Vector3(b.x > 0f ? _btnSize.x / b.x : 1f, b.y > 0f ? _btnSize.y / b.y : 1f, 1f);
                return sr.transform;
            }
            var body = Sprite(name, pos, new Color(0.30f, 0.34f, 0.42f), Sorting.HudBar);
            body.transform.localScale = new Vector3(_btnSize.x, _btnSize.y, 1f);
            var text = Text(name + "_Label", pos + new Vector3(0f, 0f, -0.1f), 2.2f);
            text.text = label;
            return body.transform;
        }
    }
}
