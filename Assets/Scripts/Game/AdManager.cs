using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// AdMob monetization via the Google Mobile Ads SDK. Uses Google's TEST ad unit IDs (safe to run;
    /// swap for your real IDs + real App ID in Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings
    /// before release).
    ///
    /// Placements: bottom BANNER during play; INTERSTITIAL every few cleared levels; REWARDED to
    /// refill hearts and to top up depleted items.
    /// </summary>
    public static class AdManager
    {
        // Ad unit IDs. Development builds ALWAYS use Google's TEST units (safe: real ads never serve,
        // and clicking your own live ads gets the AdMob account banned for invalid traffic). Release
        // (non-development) builds use the REAL units below — but only once they're filled in; an empty
        // real id falls back to the test id, so a release built before the ids are set still can't ship
        // live ads by accident. Fill RealBannerId/RealInterstitialId/RealRewardedId from the AdMob
        // console (apps.admob.com) and update the App ID in
        // Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset (adMobIOSAppId / adMobAndroidAppId).
#if UNITY_ANDROID
        private const string TestBannerId = "ca-app-pub-3940256099942544/6300978111";
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/5224354917";

        // Real Android ad unit ids (AdMob app ca-app-pub-6223833456987726~2975209740).
        private const string RealBannerId = "ca-app-pub-6223833456987726/1856166824";
        private const string RealInterstitialId = "ca-app-pub-6223833456987726/2091970547";
        private const string RealRewardedId = "ca-app-pub-6223833456987726/5603840144";
#else // iOS
        private const string TestBannerId = "ca-app-pub-3940256099942544/2934735716";
        private const string TestInterstitialId = "ca-app-pub-3940256099942544/4411468910";
        private const string TestRewardedId = "ca-app-pub-3940256099942544/1712485313";

        // Real iOS ad unit ids (AdMob app ca-app-pub-6223833456987726~3213480526).
        private const string RealBannerId = "ca-app-pub-6223833456987726/8898574552";
        private const string RealInterstitialId = "ca-app-pub-6223833456987726/5731832659";
        private const string RealRewardedId = "ca-app-pub-6223833456987726/8431670695";
#endif

        // Use TEST ad units for every non–App-Store build (Editor, simulator, TestFlight) so ads always
        // render while testing — a real device is NOT auto-registered as an AdMob test device the way a
        // simulator is, so real units on a brand-new app just fail to fill. The real units are used ONLY
        // for a production App Store build, which must define PRODUCTION_ADS (BuildScript sets it when the
        // PRODUCTION_ADS env var is passed). Also falls back to test whenever a real id is still blank.
        private static string Pick(string real, string test) =>
#if PRODUCTION_ADS
            string.IsNullOrEmpty(real) ? test : real;
#else
            test;
#endif

        private static string BannerId => Pick(RealBannerId, TestBannerId);
        private static string InterstitialId => Pick(RealInterstitialId, TestInterstitialId);
        private static string RewardedId => Pick(RealRewardedId, TestRewardedId);

        // Interstitials: less frequent than before (was every 4) and never during the early/tutorial
        // levels, so the onboarding stays ad-free and clears don't feel spammed by full-screen ads.
        public const int InterstitialEvery = 6;
        public const int NoInterstitialBeforeLevel = 6;

        private static bool _initialized;
        private static BannerView _banner;
        private static InterstitialAd _interstitial;
        private static RewardedAd _rewarded;
        private static int _levelsSinceInterstitial;

        /// <summary>Height in device pixels of the on-screen bottom banner, or 0 when none is loaded.
        /// The native banner is an overlay Unity's camera can't see, so the HUD reserves this much space
        /// at the screen bottom to keep the ITEMS row from hiding behind it. Known only AFTER the banner
        /// loads (adaptive height depends on the device), so <see cref="OnBannerChanged"/> fires then.</summary>
        public static float BannerHeightPixels { get; private set; }

        /// <summary>Raised when the banner loads/relayouts and <see cref="BannerHeightPixels"/> changes,
        /// so the HUD can re-reserve the correct bottom inset.</summary>
        public static event Action OnBannerChanged;

        /// <summary>True while a full-screen ad (interstitial or rewarded) is on screen. The game
        /// clock is paused while this is set so watching an ad never burns the player's level timer.</summary>
        public static bool IsShowing { get; private set; }

        /// <summary>Toggle the full-screen-ad state in one place: also pauses the BGM while an ad is on
        /// screen and resumes it when the ad closes, so the music never plays over the ad's audio.</summary>
        private static void SetShowing(bool showing)
        {
            IsShowing = showing;
            if (showing) AudioManager.Instance?.PauseMusic();
            else AudioManager.Instance?.ResumeMusic();
        }

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            MobileAds.Initialize(_ =>
            {
                LoadInterstitial();
                LoadRewarded();
            });
        }

        // ---- Banner ----
        public static void ShowBanner()
        {
            if (!_initialized) return;
            if (_banner == null)
            {
                // Full-width anchored adaptive banner: spans the whole screen width (flush to the
                // left/right edges) with an SDK-chosen height, instead of the fixed 320px banner.
                AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(AdSize.FullWidth);
                _banner = new BannerView(BannerId, size, AdPosition.Bottom);
                // Publish the real (device-dependent) banner height once it loads so the HUD can reserve
                // exactly that much space at the screen bottom. GetHeightInPixels is only valid post-load.
                _banner.OnBannerAdLoaded += () =>
                {
                    BannerHeightPixels = _banner.GetHeightInPixels();
                    OnBannerChanged?.Invoke();
                };
                _banner.LoadAd(new AdRequest());
            }
            _banner.Show();
        }

        public static void HideBanner() => _banner?.Hide();

        // ---- Interstitial ----
        private static void LoadInterstitial()
        {
            InterstitialAd.Load(InterstitialId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) return;
                _interstitial = ad;
                _interstitial.OnAdFullScreenContentOpened += () => SetShowing(true);
                _interstitial.OnAdFullScreenContentClosed += () =>
                {
                    SetShowing(false);
                    _interstitial.Destroy();
                    _interstitial = null;
                    LoadInterstitial();
                };
            });
        }

        /// <summary>Call when a level is CLEARED (pass the cleared level id) — shows an interstitial
        /// every InterstitialEvery clears, but never during the early tutorial levels.</summary>
        public static void NotifyLevelComplete(int clearedLevel)
        {
            if (clearedLevel < NoInterstitialBeforeLevel) return;   // onboarding stays ad-free
            if (++_levelsSinceInterstitial >= InterstitialEvery)
            {
                _levelsSinceInterstitial = 0;
                ShowInterstitial();
            }
        }

        public static void ShowInterstitial()
        {
            if (_interstitial != null && _interstitial.CanShowAd()) _interstitial.Show();
            else LoadInterstitial();
        }

        // ---- Rewarded ----
        private static void LoadRewarded()
        {
            RewardedAd.Load(RewardedId, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null) return;
                _rewarded = ad;
                _rewarded.OnAdFullScreenContentOpened += () => SetShowing(true);
                _rewarded.OnAdFullScreenContentClosed += () =>
                {
                    SetShowing(false);
                    _rewarded.Destroy();
                    _rewarded = null;
                    LoadRewarded();
                };
            });
        }

        /// <summary>Show a rewarded ad; <paramref name="onReward"/> fires when the reward is earned.
        /// If no ad is loaded yet, grants immediately (dev-friendly) and preloads one for next time.</summary>
        public static void ShowRewarded(Action onReward)
        {
            if (_rewarded != null && _rewarded.CanShowAd())
            {
                _rewarded.Show(_ => onReward?.Invoke());
            }
            else
            {
                LoadRewarded();
                onReward?.Invoke(); // fallback so the player isn't blocked if the ad hasn't loaded
            }
        }
    }
}
