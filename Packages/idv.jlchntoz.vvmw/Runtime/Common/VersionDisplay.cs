using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    [EditorOnly]
    [AddComponentMenu("VizVid/Common/Version Display")]
    [TMProMigratable]
    public class VersionDisplay : MonoBehaviour {
        public string format = "V{0}";
    }
}