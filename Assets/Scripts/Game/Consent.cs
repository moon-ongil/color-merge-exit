using System;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// Google User Messaging Platform (UMP) consent. Gathers the GDPR/EEA consent the AdMob SDK needs
    /// before requesting personalized ads: it updates consent info and shows the consent form only when
    /// required (i.e. mostly EEA users; a no-op elsewhere). <paramref name="onComplete"/> always fires so
    /// the caller can proceed to initialize ads.
    ///
    /// NOTE: on the iOS Simulator the native UMP is stubbed to a no-op (see dev/fix_admob_sim.sh), so the
    /// callbacks never fire — the caller MUST also have a timeout fallback (see GameFlow) so ad init is
    /// never blocked. On device this resolves in well under a second.
    /// </summary>
    public static class Consent
    {
        public static void RequestUmp(Action onComplete)
        {
            try
            {
                var request = new ConsentRequestParameters();
                ConsentInformation.Update(request, updateError =>
                {
                    if (updateError != null)
                    {
                        Debug.LogWarning($"[Consent] UMP update error: {updateError.Message}");
                        onComplete?.Invoke();
                        return;
                    }
                    // Loads + shows the form only if the user's region/status requires it; otherwise no-op.
                    ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                    {
                        if (formError != null)
                            Debug.LogWarning($"[Consent] UMP form error: {formError.Message}");
                        onComplete?.Invoke();
                    });
                });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Consent] UMP unavailable: {e.Message}");
                onComplete?.Invoke();
            }
        }
    }
}
