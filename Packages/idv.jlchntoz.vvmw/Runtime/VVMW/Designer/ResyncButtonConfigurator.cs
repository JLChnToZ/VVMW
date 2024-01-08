using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("VizVid/Color Configurator/Resync Button")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-add-a-resync-button")]
    public class ResyncButtonConfigurator : MonoBehaviour {
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.NextSibling
        )] public Core core;
        public bool globalSync;
    }
}