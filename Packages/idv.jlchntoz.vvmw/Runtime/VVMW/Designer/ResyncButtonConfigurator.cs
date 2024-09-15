using UnityEngine;
using UnityEngine.UI;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
#if UNITY_EDITOR
using System;
using UnityEditor.Events;
using VRC.Udon;
using UdonSharp;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [RequireComponent(typeof(Button))]
    [AddComponentMenu("VizVid/Color Configurator/Resync Button")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-add-a-resync-button")]
    public partial class ResyncButtonConfigurator : MonoBehaviour {
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.NextSibling
        ), SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] Core core;
        [SerializeField, LocalizedLabel] bool globalSync;
    }

    #if UNITY_EDITOR
    public partial class ResyncButtonConfigurator : ISelfPreProcess {
        internal static Func<UdonSharpBehaviour, UdonBehaviour> getBackingUdonBehaviour;

        int IPrioritizedPreProcessor.Priority => -10;

        void ISelfPreProcess.PreProcess() {
            if (!TryGetComponent(out Button button)) {
                Debug.LogWarning($"[ResyncButton] No button component in {name}. This should not happen.", this);
                return;
            }
            if (core == null) {
                Debug.LogWarning($"[ResyncButton] Core component in {name} is not assigned.", this);
                return;
            }
            var udon = getBackingUdonBehaviour(core);
            if (udon == null) {
                Debug.LogWarning($"[ResyncButton] Misconfigurated Core component in {name}.", this);
                return;
            }
            UnityEventTools.AddStringPersistentListener(
                button.onClick,
                udon.SendCustomEvent,
                globalSync ? nameof(Core.GlobalSync) : nameof(Core.LocalSync)
            );
        }
    }
    #endif
}