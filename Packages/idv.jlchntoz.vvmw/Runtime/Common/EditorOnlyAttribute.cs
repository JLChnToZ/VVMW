using System;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Class)]
    public class EditorOnlyAttribute : Attribute {
        public EditorOnlyAttribute() { }
    }
}