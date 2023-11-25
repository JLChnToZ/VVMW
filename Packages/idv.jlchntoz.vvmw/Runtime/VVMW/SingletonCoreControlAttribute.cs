using System;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    internal class SingletonCoreControlAttribute : Attribute {}
}