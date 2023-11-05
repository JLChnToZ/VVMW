using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW {

    [RequireComponent(typeof(Text))]
    [EditorOnly]
    [AddComponentMenu("VizVid/Common/Version Display")]
    public class VersionDisplay : MonoBehaviour {
        public string format = "V{0}";
    }
}