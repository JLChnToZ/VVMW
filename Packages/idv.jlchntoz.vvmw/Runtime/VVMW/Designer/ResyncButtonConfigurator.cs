using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [RequireComponent(typeof(Button))]
    public class ResyncButtonConfigurator : MonoBehaviour {
        [Locatable] public Core core;
        public bool globalSync;
    }
}