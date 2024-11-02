using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Stream Key Assigner")]
    public class StreamLinkAssigner : VizVidBehaviour {
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core"), Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] protected Core core;
        [SerializeField, LocalizedLabel(Key = "VVMW.Handler"), Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] protected FrontendHandler frontendHandler;
        [SerializeField, LocalizedLabel] protected bool currentUserOnly;
        [SerializeField, LocalizedLabel] protected VRCUrl[] streamLinks;
        [SerializeField, LocalizedLabel] protected VRCUrl[] altStreamLinks;
        [SerializeField, LocalizedLabel] protected string[] streamKeys;
        [SerializeField, LocalizedLabel] protected int playerIndex;
        [SerializeField, LocalizedLabel] protected bool autoInterrupt = true;
        [SerializeField, LocalizedLabel] InputField inputFieldToCopy;
        [BindEvent(nameof(Button.onClick), nameof(_Regenerate))]
        [SerializeField, LocalizedLabel] Button regenerateButton;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField, LocalizedLabel] Button playButton;
        [UdonSynced] int syncedStreamIndex = -1;
        protected int streamIndex;

        void Start() {
            if (currentUserOnly || Networking.IsOwner(gameObject)) _Regenerate();
        }

        public override void OnPreSerialization() {
            syncedStreamIndex = currentUserOnly ? -1 : streamIndex;
        }

        public override void OnDeserialization() {
            if (currentUserOnly || syncedStreamIndex < 0) return;
            streamIndex = syncedStreamIndex;
            UpdateText();
        }


        void UpdateText() {
            if (inputFieldToCopy) inputFieldToCopy.text = streamKeys[streamIndex];
        }

        public void _Regenerate() {
            RegenerateCore();
            UpdateText();
            if (!currentUserOnly) {
                if (!Networking.IsOwner(gameObject))
                    Networking.SetOwner(Networking.LocalPlayer, gameObject);
                RequestSerialization();
            }
        }

        protected virtual void RegenerateCore() {
            streamIndex = Random.Range(0, streamLinks.Length);
        }

        public void _Play() {
            if (frontendHandler) {
                bool enableIntrrupt = autoInterrupt && frontendHandler.HasQueueList;
                int currentPendingCount = enableIntrrupt && frontendHandler.PlayListIndex == 0 ? frontendHandler.PendingCount : -1;
                frontendHandler.PlayUrlMP(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
                if (enableIntrrupt && frontendHandler.PlayListIndex == 0) {
                    int pendingCount = frontendHandler.PendingCount;
                    if (pendingCount > currentPendingCount)
                        frontendHandler._PlayAt(0, pendingCount - 1, false);
                }
                return;
            }
            if (core) core.PlayUrlMP(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
        }
    }
}