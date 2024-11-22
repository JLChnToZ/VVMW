using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Automatically assigns unique stream links for each event, performer, or instance.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(2)]
    [AddComponentMenu("VizVid/Stream Key Assigner")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-automatically-assigns-unique-stream-links-for-each-event-performer-or-instance")]
    public class StreamLinkAssigner : VizVidBehaviour {
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core"), Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] protected Core core;
        [SerializeField, LocalizedLabel(Key = "VVMW.Handler"), Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] protected FrontendHandler frontendHandler;
        [SerializeField, LocalizedLabel] protected string streamKeyTemplate, streamUrlTemplate = "rtspt://example.com/live/{0}", altStreamUrlTemplate = "rtsp://example.com/live/{0}";
        [SerializeField, LocalizedLabel] protected bool currentUserOnly;
        [SerializeField, LocalizedLabel] protected VRCUrl[] streamLinks, altStreamLinks;
        [SerializeField, LocalizedLabel] protected string[] streamKeys;
        [SerializeField, LocalizedLabel] protected int playerIndex = 1;
        [SerializeField, LocalizedLabel] protected bool autoInterrupt = true;
        [SerializeField, LocalizedLabel] protected bool autoPlay = true;
        [SerializeField, LocalizedLabel] InputField inputFieldToCopy;
        [BindEvent(nameof(Button.onClick), nameof(_Regenerate))]
        [SerializeField, LocalizedLabel] Button regenerateButton;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField, LocalizedLabel] Button playButton;
        [UdonSynced] int syncedStreamIndex = -1;
        protected int streamIndex = -1;

        protected virtual void Start() {
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
            if (autoPlay) _Play();
        }

        protected virtual void RegenerateCore() {
            if (!Utilities.IsValid(streamLinks) || streamLinks.Length == 0) {
                Debug.LogError("[Stream Key Assigner] No stream links are generated. Please report to the world creator to fix this.");
                return;
            }
            streamIndex = Random.Range(0, streamLinks.Length);
        }

        public void _Play() {
            if (streamIndex < 0) {
                Debug.LogError("[Stream Key Assigner] No stream key is assigned. Unable to play.");
                return;
            }
            if (frontendHandler) {
                bool enableIntrrupt = autoInterrupt && frontendHandler.HasQueueList;
                int currentPendingCount = enableIntrrupt && frontendHandler.PlayListIndex == 0 ? frontendHandler.PendingCount : -1;
                frontendHandler.PlayUrl(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
                if (enableIntrrupt && frontendHandler.PlayListIndex == 0) {
                    int pendingCount = frontendHandler.PendingCount;
                    if (pendingCount > currentPendingCount)
                        frontendHandler.PlayAt(0, pendingCount - 1, false);
                }
                return;
            }
            if (core) core.PlayUrl(streamLinks[streamIndex], altStreamLinks[streamIndex], (byte)playerIndex);
        }
    }
}