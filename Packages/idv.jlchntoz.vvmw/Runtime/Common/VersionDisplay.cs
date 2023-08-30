using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW {

    [RequireComponent(typeof(Text))]
    [EditorOnly]
    public class VersionDisplay : MonoBehaviour {
        public string format = "V{0}";
    }
}