// Force the iOS audio session to the Playback category so game sound plays even when the
// device's Ring/Silent switch is muted (Unity's default Ambient/SoloAmbient categories are
// silenced by the mute switch). Called once at startup from AudioManager, and re-applied on
// focus regain because Unity re-initialises its own audio session when the app returns.
#import <AVFoundation/AVFoundation.h>

extern "C" void _CME_SetAudioSessionPlayback(void)
{
    NSError *err = nil;
    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryPlayback
             withOptions:AVAudioSessionCategoryOptionMixWithOthers
                   error:&err];
    [session setActive:YES error:&err];
}

// Mixing category used when the player already has their own music going. Ambient NEVER interrupts
// other apps' audio, so their music keeps playing — but we still setActive:YES so OUR session is
// live and game SFX actually come out (an inactive session = no game sound). Ambient is silenced by
// the Ring/Silent switch, which is fine: we only use it when the player is already listening to audio.
extern "C" void _CME_SetAudioSessionMixAmbient(void)
{
    NSError *err = nil;
    AVAudioSession *session = [AVAudioSession sharedInstance];
    [session setCategory:AVAudioSessionCategoryAmbient error:&err];
    [session setActive:YES error:&err];
}

// True when another app (Music/Spotify/Genie/…) is already playing audio. Used so the game can DEFER
// its own BGM to the player's music and pick the non-interrupting Ambient session instead of Playback.
extern "C" int _CME_IsOtherAudioPlaying(void)
{
    return [AVAudioSession sharedInstance].isOtherAudioPlaying ? 1 : 0;
}
