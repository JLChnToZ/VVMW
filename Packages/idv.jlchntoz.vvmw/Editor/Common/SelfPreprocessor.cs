using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class SelfPreprocessor : IPreprocessor {
        public int CallbackOrder => -10;
        
        public void OnPreprocess(Scene scene) {
            foreach (var mb in scene.IterateAllComponents<MonoBehaviour>())
                if (mb is ISelfPreProcess selfPreprocessor)
                    try {
                        selfPreprocessor.PreProcess();
                    } catch (Exception ex) {
                        Debug.LogError($"[SelfPreprocessor] Error while processing {mb.name}: {ex.Message}\n{ex.StackTrace}", mb);
                    }
        }
    }
}