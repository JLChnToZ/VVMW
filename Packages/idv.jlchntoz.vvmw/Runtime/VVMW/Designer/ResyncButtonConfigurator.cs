using UnityEngine;
using UnityEngine.UI;

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("VizVid/Color Configurator/Resync Button")]
    public class ResyncButtonConfigurator : MonoBehaviour {
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.NextSibling
        )] public Core core;
        public bool globalSync;
    }
}