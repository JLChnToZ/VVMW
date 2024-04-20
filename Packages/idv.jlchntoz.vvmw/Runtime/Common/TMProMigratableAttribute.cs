using System;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, Inherited = true)]
    public class TMProMigratableAttribute : Attribute {
        public string TMProFieldName { get; private set; }

        public TMProMigratableAttribute() : this("") { }

        public TMProMigratableAttribute(string tmProFieldName) {
            TMProFieldName = tmProFieldName;
        }
    }
}