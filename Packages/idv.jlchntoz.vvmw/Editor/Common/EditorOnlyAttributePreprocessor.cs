using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    public class EditorOnlyAttributePreprocessor : IProcessSceneWithReport {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) {
            foreach (var mb in scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MonoBehaviour>(true)))
                if (mb.GetType().GetCustomAttribute<EditorOnlyAttribute>() != null)
                    DestroyImmediate(mb, true);
        }
    }
}