using UnityEngine;

namespace ColorMergeExit.Game
{
    /// <summary>
    /// SFX one-shots + looping BGM. Accessed via a static Instance so gameplay code
    /// can fire sounds without wiring; when no AudioManager exists (editor screenshot),
    /// calls are no-ops. Master mute is persisted in PlayerPrefs and applied globally.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private AudioSource _sfx, _music;
        private AudioClip _slide, _exit, _win, _lose, _tap, _merge, _bump, _bgm;

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _CME_SetAudioSessionPlayback();
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int _CME_IsOtherAudioPlaying();
#endif
        // Play sound even when the iOS Ring/Silent switch is muted (see AudioSession.mm). Re-applied on
        // focus regain because Unity re-initialises its own audio session when the app returns to front.
        private static void ApplyPlaybackAudioSession()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _CME_SetAudioSessionPlayback();
#endif
        }

        // True when the player already has their own music going (Genie, Spotify, Apple Music…). We DEFER
        // our BGM to it rather than playing over the top — SFX still mix in (session is MixWithOthers).
        private static bool OtherAudioPlaying()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return _CME_IsOtherAudioPlaying() != 0;
#else
            return false;
#endif
        }

        // On focus regain the player may have started or stopped their own music while away — re-decide
        // whether our BGM should play, so we never talk over their music.
        private void OnApplicationFocus(bool focus)
        {
            if (!focus) return;
            ApplyPlaybackAudioSession();
            if (OtherAudioPlaying()) { if (_music != null && _music.isPlaying) _music.Stop(); }
            else PlayMusic();
        }

        private void Awake()
        {
            Instance = this;
            ApplyPlaybackAudioSession();

            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.32f;

            _slide = Load("slide");
            _exit = Load("exit");
            _win = Load("win");
            _lose = Load("lose");
            _tap = Load("tap");
            _merge = Load("merge");
            _bump = Load("bump");
            _bgm = Load("bgm");

            AudioListener.volume = Muted ? 0f : 1f;
            PlayMusic();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool Muted => PlayerPrefs.GetInt("muted", 0) == 1;

        public void ToggleMute()
        {
            bool m = !Muted;
            PlayerPrefs.SetInt("muted", m ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = m ? 0f : 1f;
        }

        public void PlayMusic()
        {
            if (_bgm == null || _music == null) return;
            // Defer to the player's own music if they're already listening to something.
            if (OtherAudioPlaying()) { if (_music.isPlaying) _music.Stop(); return; }
            _music.clip = _bgm;
            if (!_music.isPlaying) _music.Play();
        }

        private static AudioClip Load(string name) => Resources.Load<AudioClip>("Audio/" + name);

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null && _sfx != null) { _sfx.pitch = 1f; _sfx.PlayOneShot(clip, volume); }
        }

        public void Slide() => Play(_slide, 0.6f);
        public void Exit() => Play(_exit, 0.85f);
        public void Win() => Play(_win, 0.9f);
        public void Lose() => Play(_lose, 0.8f);
        public void Tap() => Play(_tap, 0.5f);

        /// <summary>Dull thunk when two blocks that can't merge knock against each other.</summary>
        public void Bump() => Play(_bump, 0.7f);

        /// <summary>Merge blend/ding; pitch>1 for the brighter tertiary (chain) merge.</summary>
        public void Merge(float pitch = 1f)
        {
            if (_merge != null && _sfx != null) { _sfx.pitch = pitch; _sfx.PlayOneShot(_merge, 0.85f); }
        }
    }
}
