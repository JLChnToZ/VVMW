using System;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field)]
    public class LocatableAttribute : PropertyAttribute {
        public string[] TypeNames { get; set; }

        public string InstaniatePrefabPath { get; set; }

        public string InstaniatePrefabGuid { get; set; }

        public InstaniatePrefabHierachyPosition InstaniatePrefabPosition { get; set; }

        public LocatableAttribute() { }

        public LocatableAttribute(params string[] typeNames) {
            TypeNames = typeNames;
        }

        [Flags]
        public enum InstaniatePrefabHierachyPosition : byte {
            Root = 0,
            Child = 1,
            SameLevel = 2,
            PreviousSibling = 4,
            NextSibling = 8,
            First = 16,
            Last = 32,

            FirstRoot = First | Root,
            LastRoot = Last | Root,
            FirstChild = First | Child,
            LastChild = Last | Child,
            FirstSameLevel = First | SameLevel,
            LastSameLevel = Last | SameLevel,
            Before = PreviousSibling | SameLevel,
            After = NextSibling | SameLevel,
        }
    }
}
