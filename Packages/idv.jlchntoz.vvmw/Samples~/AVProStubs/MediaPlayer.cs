using UnityEngine;

namespace RenderHeads.Media.AVProVideo {
    // Minimal stub of AVPro Media Player, which ensures driven properties in animation clip got properly referenced.
    [AddComponentMenu("/AVPro Media Player (Stub)")]
    internal class MediaPlayer : MonoBehaviour {
        [SerializeField] float _playbackRate = 1;
    }
}