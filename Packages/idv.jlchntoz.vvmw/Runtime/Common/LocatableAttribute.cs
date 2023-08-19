using System;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field)]
    public class LocatableAttribute : PropertyAttribute {
        public string[] TypeNames { get; set; }

        public LocatableAttribute() { }

        public LocatableAttribute(params string[] typeNames) {
            TypeNames = typeNames;
        }
    }
}
