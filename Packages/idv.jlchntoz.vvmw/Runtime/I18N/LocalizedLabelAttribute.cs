using UnityEngine;

namespace JLChnToZ.VRC.VVMW.I18N {
    public class LocalizedLabelAttribute : PropertyAttribute {
        public string Key { get; set; }
        public string TooltipKey { get; set; }

        public LocalizedLabelAttribute() { }
    }

    public class LocalizedHeaderAttribute : PropertyAttribute {
        public string Key { get; private set; }

        public LocalizedHeaderAttribute(string key) {
            Key = key;
        }
    }
}