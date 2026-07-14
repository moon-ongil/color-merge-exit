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
        // Google TEST ad unit IDs (do NOT ship these).
#if UNITY_ANDROID
        private const string BannerId = "ca-app-pub-3940256099942544/6300978111";
        private const string InterstitialId = "ca-app-pub-3940256099942544/1033173712";
        private const string RewardedId = "ca-app-pub-3940256099942544/5224354917";
#else // iOS
        private const string BannerId = "ca-app-pub-3940256099942544/2934735716";
        private const string InterstitialId = "ca-app-pub-3940256099942544/4411468910";
        private const string RewardedId = "ca-app-pub-3940256099942544/1712485313";
#endif

        public const int InterstitialEvery = 4;

        private static bool _initialized;
        private static BannerView _banner;
        private static InterstitialAd _interstitial;
        private static RewardedAd _rewarded;
        private static int _levelsSinceInterstitial;

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
                _interstitial.OnAdFullScreenContentClosed += () =>
                {
                    _interstitial.Destroy();
                    _interstitial = null;
                    LoadInterstitial();
                };
            });
        }

        /// <summary>Call when a level is CLEARED — shows an interstitial every InterstitialEvery levels.</summary>
        public static void NotifyLevelComplete()
        {
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
                _rewarded.OnAdFullScreenContentClosed += () =>
                {
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
