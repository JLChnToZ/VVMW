using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using JLChnToZ.VRC.Foundation;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// A dual-handle slider for selecting a range.
    /// The left handle is the start of the range, and the right handle is the end of the range.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/Range Slider")]
    public class RangeSlider : UdonSharpEventSender {
        [SerializeField, LocalizedLabel, FieldChangeCallback(nameof(Interactable))] bool interactable = true;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(RangeStart))] float rangeStart;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(RangeEnd))] float rangeEnd = 1;
        [SerializeField, LocalizedLabel, BindEvent("#" + nameof(Slider.onValueChanged), nameof(_OnValueChanged))] Slider leftSlider;
        [SerializeField, LocalizedLabel, BindEvent("#" + nameof(Slider.onValueChanged), nameof(_OnValueChanged))] Slider rightSlider;
        [SerializeField, HideInInspector, Resolve(nameof(leftSlider))] RectTransform leftSliderTransform;
        [SerializeField, HideInInspector, Resolve(nameof(rightSlider))] RectTransform rightSliderTransform;
        [SerializeField, LocalizedLabel] Graphic leftSliderBackground, rightSliderBackground;
        [SerializeField, HideInInspector, Resolve(nameof(leftSlider) + "." + nameof(Slider.fillRect))] Graphic leftSliderFill;
        [SerializeField, HideInInspector, Resolve(nameof(rightSlider) + "." + nameof(Slider.fillRect))] Graphic rightSliderFill;
        /// <summary>
        /// The event name to send when the value of the slider changes.
        /// </summary>
        [LocalizedLabel] public string callbackEventName = "_OnValueChanged";

        /// <summary>
        /// Whether the slider is interactable.
        /// If false, the slider will not respond to user input and will not send events when the value changes.
        /// </summary>
        public bool Interactable {
            get => interactable;
            set {
                if (interactable == value) return;
                interactable = value;
                UpdateInteractable();
            }
        }

        /// <summary>
        /// The start of the range, represented by the left handle. Value is between 0 and 1.
        /// </summary>
        public float RangeStart {
            get => rangeStart = leftSlider.value;
            set {
                value = Mathf.Clamp(value, 0, rangeEnd);
                if (Mathf.Approximately(rangeStart, value)) return;
                rangeStart = value;
                leftSlider.SetValueWithoutNotify(value);
                UpdateSliderApperance();
            }
        }

        /// <summary>
        /// The end of the range, represented by the right handle. Value is between 0 and 1.
        /// </summary>
        public float RangeEnd {
            get => rangeEnd = 1 - rightSlider.value;
            set {
                value = Mathf.Clamp(value, rangeStart, 1);
                if (Mathf.Approximately(rangeEnd, value)) return;
                rangeEnd = value;
                rightSlider.SetValueWithoutNotify(1 - value);
                UpdateSliderApperance();
            }
        }

        void OnEnable() {
            UpdateSliderApperance();
            UpdateInteractable();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnValueChanged() {
            UpdateSliderApperance();
            rangeStart = leftSlider.value;
            rangeEnd = 1 - rightSlider.value;
            SendEvent(callbackEventName);
        }

        public void SetRange(float start, float end) {
            start = Mathf.Clamp01(start);
            end = Mathf.Clamp01(end);
            if (Mathf.Approximately(rangeStart, start) && Mathf.Approximately(rangeEnd, end)) return;
            rangeStart = start;
            rangeEnd = end;
            leftSlider.SetValueWithoutNotify(start);
            rightSlider.SetValueWithoutNotify(1 - end);
            UpdateSliderApperance();
        }

        void UpdateInteractable() {
            leftSlider.interactable = interactable;
            rightSlider.interactable = interactable;
            if (Utilities.IsValid(leftSliderFill)) leftSliderFill.raycastTarget = interactable;
            if (Utilities.IsValid(rightSliderFill)) rightSliderFill.raycastTarget = interactable;
            if (Utilities.IsValid(leftSliderBackground)) leftSliderBackground.raycastTarget = interactable;
            if (Utilities.IsValid(rightSliderBackground)) rightSliderBackground.raycastTarget = interactable;
        }

        void UpdateSliderApperance() {
            var middle = (1 + leftSlider.value - rightSlider.value) * 0.5f;
            leftSlider.maxValue = middle;
            var anchor = leftSliderTransform.anchorMax;
            anchor.x = middle;
            leftSliderTransform.anchorMax = anchor;
            rightSlider.maxValue = 1 - middle;
            anchor = rightSliderTransform.anchorMin;
            anchor.x = middle;
            rightSliderTransform.anchorMin = anchor;
        }
    }
}