using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace JLChnToZ.VRC.VVMW.Editors {
    [InitializeOnLoad]
    public class BuildPreprocessorManager : IProcessSceneWithReport {
        static readonly HashSet<Type> preprocessorTypes;
        List<IPreprocessor> preprocessors;
        public int callbackOrder => -100;

        static BuildPreprocessorManager() {
            preprocessorTypes = new HashSet<Type>();
            var appDomain = AppDomain.CurrentDomain;
            foreach (var assembly in appDomain.GetAssemblies())
                LoadTypeFromAssembly(assembly);
            appDomain.AssemblyLoad += OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) =>
            LoadTypeFromAssembly(args.LoadedAssembly);

        static void LoadTypeFromAssembly(Assembly assembly) {
            foreach (var type in assembly.GetTypes()) {
                if (type.IsInterface || type.IsAbstract)
                    continue;
                if (typeof(IPreprocessor).IsAssignableFrom(type))
                    preprocessorTypes.Add(type);
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report) {
            StartProcess(scene);
        }

        void StartProcess(Scene scene) {
            InitInstances();
            foreach (var preprocessor in preprocessors)
                try {
                    preprocessor.OnPreprocess(scene);
                } catch (Exception e) {
                    Debug.LogError($"Error while processing scene with {preprocessor.GetType().Name}: {e.Message}");
                }
        }

        void InitInstances() {
            if (preprocessors != null) return;
            preprocessors = new List<IPreprocessor>();
            foreach (var type in preprocessorTypes) {
                if (Activator.CreateInstance(type) is IPreprocessor preprocessor)
                    preprocessors.Add(preprocessor);
            }
            preprocessors.Sort(SortCallbackOrder);
        }

        static int SortCallbackOrder(IPreprocessor a, IPreprocessor b) =>
            a.CallbackOrder.CompareTo(b.CallbackOrder);
    }

    public interface IPreprocessor {
        int CallbackOrder { get; }
        void OnPreprocess(Scene scene);
    }
}