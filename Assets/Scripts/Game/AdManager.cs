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

        // Use test units in Development builds, or whenever a real id is still blank.
        private static string Pick(string real, string test) =>
            (Debug.isDebugBuild || string.IsNullOrEmpty(real)) ? test : real;

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

        /// <summary>True while a full-screen ad (interstitial or rewarded) is on screen. The game
        /// clock is paused while this is set so watching an ad never burns the player's level timer.</summary>
        public static bool IsShowing { get; private set; }

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
                _interstitial.OnAdFullScreenContentOpened += () => IsShowing = true;
                _interstitial.OnAdFullScreenContentClosed += () =>
                {
                    IsShowing = false;
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
                _rewarded.OnAdFullScreenContentOpened += () => IsShowing = true;
                _rewarded.OnAdFullScreenContentClosed += () =>
                {
                    IsShowing = false;
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
