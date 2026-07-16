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
