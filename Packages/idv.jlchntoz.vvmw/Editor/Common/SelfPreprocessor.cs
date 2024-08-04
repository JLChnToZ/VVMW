using UnityEngine;
using UnityEngine.SceneManagement;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class SelfPreprocessor : IPreprocessor {
        public int CallbackOrder => -10;
        
        public void OnPreprocess(Scene scene) {
            foreach (var preprocessor in scene.IterateAllComponents<UdonSharpBehaviour>())
                if (preprocessor is ISelfPreProcess selfPreprocessor)
                    try {
                        selfPreprocessor.PreProcess();
                    } catch (System.Exception e) {
                        Debug.LogError($"[SelfPreprocessor] Error while processing {preprocessor.name}: {e.Message}", preprocessor);
                    }
        }
    }
}