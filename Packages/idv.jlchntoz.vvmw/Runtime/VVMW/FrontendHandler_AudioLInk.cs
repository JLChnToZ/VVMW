using VRC.SDKBase;
#if AUDIOLINK_V1
using AudioLink;
#endif

namespace JLChnToZ.VRC.VVMW {
    public partial class FrontendHandler {
        void UpdateAudioLink() {
            #if AUDIOLINK_V1
            var audioLink = core.AudioLink;
            if (Utilities.IsValid(audioLink)) {
                if ((localFlags & REPEAT_ALL) != 0) {
                    if ((localFlags & SHUFFLE) != 0)
                        audioLink.SetMediaLoop(MediaLoop.RandomLoop);
                    else
                        audioLink.SetMediaLoop(MediaLoop.Loop);
                } else if ((localFlags & REPEAT_ONE) != 0)
                    audioLink.SetMediaLoop(MediaLoop.LoopOne);
                else if ((localFlags & SHUFFLE) != 0)
                    audioLink.SetMediaLoop(MediaLoop.Random);
                else
                    audioLink.SetMediaLoop(MediaLoop.None);
            }
            #endif
        }
    }
}