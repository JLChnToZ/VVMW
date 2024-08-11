using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    public interface ISingleton<T> where T : UdonSharpBehaviour, ISingleton<T> {
        void Merge(T[] others);
    }

    public interface IPrioritizedPreProcessor {
        int Priority { get; }
    }

    public interface ISelfPreProcess : IPrioritizedPreProcessor {
        void PreProcess();
    }
}