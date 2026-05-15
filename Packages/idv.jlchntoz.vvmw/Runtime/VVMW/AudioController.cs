using UdonSharp;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// This component proxies the volume control to the audio source.
    /// You can individually control attached audio sources' volume here while connected to the video player.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/Audio Controller")]
    public class AudioController : AbstractAudioController {
    }
}