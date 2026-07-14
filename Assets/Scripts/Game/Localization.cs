using System.Collections.Generic;
using UnityEngine;

namespace ColorMergeExit.Game
{
    public enum Locale { En, Ko, Ja, ZhHans, Es, PtBr, De, Fr, It, Ru, Ar }

    /// <summary>
    /// Lightweight key-based localization. Intentionally tiny and engine-only so it
    /// works today with zero editor setup; the game is near-textless, so a handful
    /// of keys covers the whole UI.
    ///
    /// Migration path: when TextMeshPro + fonts are added (Pretendard for
    /// Latin/Cyrillic/Hangul, Noto Sans JP/SC/Arabic as TMP font-fallbacks), swap
    /// the lookup for Unity's Localization package. Call sites use <see cref="Get"/>
    /// with <see cref="LocKeys"/>, so only this file changes.
    ///
    /// NOTE: default locale is <see cref="Locale.En"/> because the current HUD uses
    /// the builtin Latin font (TextMesh) which cannot render CJK/Arabic glyphs.
    /// Enable <see cref="AutoDetect"/> only after switching text to TMP + fonts that
    /// cover the target scripts. Arabic additionally needs RTL shaping.
    /// </summary>
    public static class Localization
    {
        public static Locale Current { get; private set; } = Locale.En;

        public static void SetLocale(Locale locale)
        {
            if (Tables.ContainsKey(locale)) Current = locale;
        }

        /// <summary>Pick the device language if supported, else fall back to English.</summary>
        public static void AutoDetect()
        {
            Current = Map(Application.systemLanguage);
        }

        public static string Get(string key)
        {
            string raw = Raw(key);
            // Arabic needs contextual shaping (letter joining) + RTL reordering before
            // it can render in a shaping-unaware renderer like TMP.
            return Current == Locale.Ar ? Shape(raw) : raw;
        }

        /// <summary>Reshape an already-localized Arabic string for display. Safe for
        /// mixed Arabic/Latin/number content; a no-op-ish passthrough for plain ASCII.</summary>
        public static string Shape(string s) => ArabicSupport.ArabicFixer.Fix(s);

        /// <summary>Every string as it will actually render (Arabic shaped to
        /// presentation forms). Used by the editor font-subsetting step.</summary>
        public static IEnumerable<string> AllDisplayStrings()
        {
            foreach (var kv in Tables)
            {
                bool ar = kv.Key == Locale.Ar;
                foreach (var v in kv.Value.Values)
                    yield return ar ? Shape(v) : v;
            }
        }

        private static string Raw(string key)
        {
            if (Tables.TryGetValue(Current, out var table) && table.TryGetValue(key, out var value))
                return value;
            if (Tables[Locale.En].TryGetValue(key, out var en))
                return en; // fall back to English
            return key;     // last resort: show the key so a missing entry is obvious
        }

        private static Locale Map(SystemLanguage lang)
        {
            switch (lang)
            {
                case SystemLanguage.Korean: return Locale.Ko;
                case SystemLanguage.Japanese: return Locale.Ja;
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional: return Locale.ZhHans; // TODO: add ZhHant
                case SystemLanguage.Spanish: return Locale.Es;
                case SystemLanguage.Portuguese: return Locale.PtBr;
                case SystemLanguage.German: return Locale.De;
                case SystemLanguage.French: return Locale.Fr;
                case SystemLanguage.Italian: return Locale.It;
                case SystemLanguage.Russian: return Locale.Ru;
                case SystemLanguage.Arabic: return Locale.Ar;
                default: return Locale.En;
            }
        }

        // Short UI strings. Add locales/keys here; call sites never change.
        private static readonly Dictionary<Locale, Dictionary<string, string>> Tables =
            new Dictionary<Locale, Dictionary<string, string>>
            {
                [Locale.En] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "RESTART",
                    [LocKeys.Undo] = "UNDO",
                    [LocKeys.Memorize] = "MEMORIZE",
                    [LocKeys.Clear] = "CLEAR!",
                    [LocKeys.TimeUp] = "TIME UP",
                    [LocKeys.Detonated] = "BOOM!",
                    [LocKeys.Retry] = "RETRY?",
                    [LocKeys.DeadEnd] = "NO WAY OUT!",
                    [LocKeys.SplitPick] = "TAP A BLOCK TO SPLIT",
                    [LocKeys.Items] = "ITEMS",
                    [LocKeys.Menu] = "MENU",
                    [LocKeys.Settings] = "SETTINGS",
                    [LocKeys.Sound] = "SOUND",
                    [LocKeys.Language] = "LANGUAGE",
                    [LocKeys.Close] = "CLOSE",
                    [LocKeys.HowToPlay] = "COLOR MIX",
                    [LocKeys.MergeHint] = "Push blocks together to mix colors",
                    [LocKeys.SplitHint] = "Splitter breaks a mixed color apart",
                    [LocKeys.Tagline] = "Bring color back to the world",
                    [LocKeys.NoHearts] = "OUT OF HEARTS",
                    [LocKeys.WatchAd] = "WATCH AD",
                    [LocKeys.TapToContinue] = "Tap to continue",
                    [LocKeys.TutDrag] = "Drag a block to the exit of the same color!",
                    [LocKeys.TutExit] = "When it reaches a matching exit, the block leaves the board.",
                    [LocKeys.TutMerge] = "Overlap two blocks to mix their colors. e.g. Blue + Yellow = Green",
                    [LocKeys.TutMergeExit] = "Send the mixed color to its matching exit!",
                    [LocKeys.TutHint] = "Stuck? Tap HINT to see the next move. (unlocked!)",
                    [LocKeys.TutAddTime] = "Low on time? Tap +TIME to add seconds. (unlocked!)",
                    [LocKeys.TutSplitCell] = "Pass a SPLIT tile and a mixed block breaks back apart!",
                    [LocKeys.TutSplitItem] = "The SPLIT item breaks every mixed block at once. (unlocked!)",
                    [LocKeys.ExitLevel] = "EXIT",
                    [LocKeys.RefillHearts] = "REFILL",
                },
                [Locale.Ko] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "다시하기",
                    [LocKeys.Undo] = "되돌리기",
                    [LocKeys.Memorize] = "기억하세요",
                    [LocKeys.Clear] = "클리어!",
                    [LocKeys.TimeUp] = "시간 초과",
                    [LocKeys.Detonated] = "펑! 시간 초과",
                    [LocKeys.Retry] = "다시?",
                    [LocKeys.DeadEnd] = "막다른 길!",
                    [LocKeys.SplitPick] = "분리할 블록을 탭!",
                    [LocKeys.Items] = "아이템",
                    [LocKeys.Menu] = "메뉴",
                    [LocKeys.Settings] = "설정",
                    [LocKeys.Sound] = "사운드",
                    [LocKeys.Language] = "언어",
                    [LocKeys.Close] = "닫기",
                    [LocKeys.HowToPlay] = "색 조합",
                    [LocKeys.MergeHint] = "블록을 밀어 붙이면 색이 섞여요",
                    [LocKeys.SplitHint] = "분리기는 섞인 색을 다시 나눠요",
                    [LocKeys.Tagline] = "세상에 색을 되돌리는 여정",
                    [LocKeys.NoHearts] = "하트가 없어요",
                    [LocKeys.WatchAd] = "광고 보기",
                    [LocKeys.TapToContinue] = "탭하여 계속",
                    [LocKeys.TutDrag] = "블록을 같은 색 출구로 밀어보세요!",
                    [LocKeys.TutExit] = "같은 색 출구에 닿으면 블록이 빠져나가요.",
                    [LocKeys.TutMerge] = "두 블록을 겹치면 색이 섞여요. 예) 파랑 + 노랑 = 초록",
                    [LocKeys.TutMergeExit] = "섞인 색을 맞는 출구로 내보내세요!",
                    [LocKeys.TutHint] = "막히면 '힌트'로 다음 수를 확인! (해제됨)",
                    [LocKeys.TutAddTime] = "시간이 부족하면 '+시간'! (해제됨)",
                    [LocKeys.TutSplitCell] = "분리 칸을 지나면 섞인 블록이 다시 나뉘어요!",
                    [LocKeys.TutSplitItem] = "'분리' 아이템은 섞인 블록을 한 번에 나눠요. (해제됨)",
                    [LocKeys.ExitLevel] = "나가기",
                    [LocKeys.RefillHearts] = "충전",
                },
                [Locale.Ja] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "リスタート",
                    [LocKeys.Undo] = "もどす",
                    [LocKeys.Memorize] = "おぼえて",
                    [LocKeys.Clear] = "クリア！",
                    [LocKeys.TimeUp] = "タイムアップ",
                    [LocKeys.Retry] = "もう一度？",
                },
                [Locale.ZhHans] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "重新开始",
                    [LocKeys.Undo] = "撤销",
                    [LocKeys.Memorize] = "记住顺序",
                    [LocKeys.Clear] = "通关！",
                    [LocKeys.TimeUp] = "时间到",
                    [LocKeys.Retry] = "再来？",
                },
                [Locale.Es] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "REINICIAR",
                    [LocKeys.Undo] = "DESHACER",
                    [LocKeys.Memorize] = "MEMORIZA",
                    [LocKeys.Clear] = "¡COMPLETADO!",
                    [LocKeys.TimeUp] = "TIEMPO AGOTADO",
                    [LocKeys.Retry] = "¿REINTENTAR?",
                },
                [Locale.PtBr] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "REINICIAR",
                    [LocKeys.Undo] = "DESFAZER",
                    [LocKeys.Memorize] = "MEMORIZE",
                    [LocKeys.Clear] = "CONCLUÍDO!",
                    [LocKeys.TimeUp] = "TEMPO ESGOTADO",
                    [LocKeys.Retry] = "TENTAR DE NOVO?",
                },
                [Locale.De] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "NEU STARTEN",
                    [LocKeys.Undo] = "RÜCKGÄNGIG",
                    [LocKeys.Memorize] = "MERKEN",
                    [LocKeys.Clear] = "GESCHAFFT!",
                    [LocKeys.TimeUp] = "ZEIT ABGELAUFEN",
                    [LocKeys.Retry] = "NOCHMAL?",
                },
                [Locale.Fr] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "RECOMMENCER",
                    [LocKeys.Undo] = "ANNULER",
                    [LocKeys.Memorize] = "MÉMORISE",
                    [LocKeys.Clear] = "RÉUSSI !",
                    [LocKeys.TimeUp] = "TEMPS ÉCOULÉ",
                    [LocKeys.Retry] = "RÉESSAYER ?",
                },
                [Locale.It] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "RICOMINCIA",
                    [LocKeys.Undo] = "ANNULLA",
                    [LocKeys.Memorize] = "MEMORIZZA",
                    [LocKeys.Clear] = "COMPLETATO!",
                    [LocKeys.TimeUp] = "TEMPO SCADUTO",
                    [LocKeys.Retry] = "RIPROVA?",
                },
                [Locale.Ru] = new Dictionary<string, string>
                {
                    [LocKeys.Restart] = "ЗАНОВО",
                    [LocKeys.Undo] = "ОТМЕНА",
                    [LocKeys.Memorize] = "ЗАПОМНИ",
                    [LocKeys.Clear] = "ПРОЙДЕНО!",
                    [LocKeys.TimeUp] = "ВРЕМЯ ВЫШЛО",
                    [LocKeys.Retry] = "ЕЩЁ РАЗ?",
                },
                [Locale.Ar] = new Dictionary<string, string>
                {
                    // Arabic renders right-to-left and needs glyph shaping (handle at
                    // the TMP/rendering layer). Strings themselves are stored normally.
                    [LocKeys.Restart] = "إعادة",
                    [LocKeys.Undo] = "تراجع",
                    [LocKeys.Memorize] = "احفظ",
                    [LocKeys.Clear] = "اكتمل!",
                    [LocKeys.TimeUp] = "انتهى الوقت",
                    [LocKeys.Retry] = "إعادة المحاولة؟",
                },
            };
    }

    /// <summary>String table keys. Use these constants instead of literal keys.</summary>
    public static class LocKeys
    {
        public const string Restart = "hud.restart";
        public const string Undo = "hud.undo";
        public const string Memorize = "game.memorize";
        public const string Clear = "game.clear";
        public const string TimeUp = "game.timeup";
        public const string Detonated = "game.detonated";
        public const string Retry = "game.retry";
        public const string DeadEnd = "game.deadend";
        public const string SplitPick = "game.splitpick";
        public const string Items = "hud.items";
        public const string Menu = "hud.menu";
        public const string Settings = "settings.title";
        public const string Sound = "settings.sound";
        public const string Language = "settings.language";
        public const string Close = "settings.close";
        public const string HowToPlay = "info.title";
        public const string MergeHint = "info.merge";
        public const string SplitHint = "info.split";
        public const string Tagline = "home.tagline";
        public const string NoHearts = "hearts.none";
        public const string WatchAd = "hearts.watchad";
        // Tutorial coach-marks
        public const string TapToContinue = "tut.tap";
        public const string TutDrag = "tut.drag";
        public const string TutExit = "tut.exit";
        public const string TutMerge = "tut.merge";
        public const string TutMergeExit = "tut.mergeexit";
        public const string TutHint = "tut.hint";
        public const string TutAddTime = "tut.addtime";
        public const string TutSplitCell = "tut.splitcell";
        public const string TutSplitItem = "tut.splititem";
        // Game-over overlay buttons
        public const string ExitLevel = "gameover.exit";
        public const string RefillHearts = "gameover.refill";
    }
}
