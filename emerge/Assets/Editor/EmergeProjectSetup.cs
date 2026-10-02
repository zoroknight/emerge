using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Emerge.Editor
{
    public static class EmergeProjectSetup
    {
        private const string ScenePath = "Assets/_Emerge/Scenes/CellLab.unity";

        [MenuItem("Emerge/Environment/Configure Project")]
        public static void Configure()
        {
            string[] folders = {
                "Scenes", "Scripts/Core", "Scripts/Cells", "Scripts/World",
                "Scripts/BuildEditor", "Scripts/Presentation", "Data/Cells",
                "Data/Regions", "Prefabs/Cells", "Prefabs/World",
                "Art/Sprites", "Art/Materials", "Audio", "UI"
            };
            foreach (string folder in folders)
                Directory.CreateDirectory("Assets/_Emerge/" + folder);
            AssetDatabase.Refresh();

            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "GameJam";
            PlayerSettings.productName = "Emerge";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            if (pipeline == null) throw new InvalidOperationException("Template URP asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetApiCompatibilityLevel(NamedBuildTarget.Standalone, ApiCompatibilityLevel.NET_Standard);
            Time.fixedDeltaTime = 0.02f;
            Physics2D.gravity = Vector2.zero;
            QualitySettings.vSyncCount = 1;

            // Use the Input System already installed by the official 2D template.
            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            playerSettings.FindProperty("activeInputHandler").intValue = 1;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();

            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            string[] names = { "Cell", "Nutrient", "Terrain", "Hazard" };
            for (int i = 0; i < names.Length; i++)
            {
                var layer = layers.GetArrayElementAtIndex(8 + i);
                if (!string.IsNullOrEmpty(layer.stringValue) && layer.stringValue != names[i])
                    throw new InvalidOperationException("Layer slot is already used: " + layer.stringValue);
                layer.stringValue = names[i];
            }
            tags.ApplyModifiedPropertiesWithoutUndo();

            string scriptEditor = @"D:\VS Code\Microsoft VS Code\Code.exe";
            if (File.Exists(scriptEditor))
            {
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(scriptEditor);
                Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            }
            else
                throw new FileNotFoundException("VS Code is missing. Update the editor path in EmergeProjectSetup.", scriptEditor);

            const string inputPath = "Assets/_Emerge/Data/EmergeControls.inputactions";
            if (!File.Exists(inputPath))
            {
                var controls = ScriptableObject.CreateInstance<InputActionAsset>();
                var gameplay = controls.AddActionMap("Gameplay");
                gameplay.AddAction("IntentW", InputActionType.Button, "<Keyboard>/w");
                gameplay.AddAction("IntentA", InputActionType.Button, "<Keyboard>/a");
                gameplay.AddAction("IntentS", InputActionType.Button, "<Keyboard>/s");
                gameplay.AddAction("IntentD", InputActionType.Button, "<Keyboard>/d");
                gameplay.AddAction("ToggleEditor", InputActionType.Button, "<Keyboard>/tab");
                gameplay.AddAction("Assimilate", InputActionType.Button, "<Keyboard>/e");
                gameplay.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
                File.WriteAllText(inputPath, controls.ToJson());
                UnityEngine.Object.DestroyImmediate(controls);
                AssetDatabase.ImportAsset(inputPath);
            }

            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
                var camera = Camera.main;
                if (camera == null) throw new InvalidOperationException("Template camera is missing.");
                camera.orthographic = true;
                camera.orthographicSize = 8f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.055f, 0.14f, 0.19f);
                camera.transform.position = new Vector3(0, 0, -10);
                new GameObject("Organisms");
                new GameObject("World");
                new GameObject("Presentation");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("EMERGE_SETUP_OK");
        }

        [MenuItem("Emerge/Environment/Validate Project")]
        public static void Validate()
        {
            if (!File.Exists(ScenePath)) throw new InvalidOperationException("CellLab scene is missing.");
            if (GraphicsSettings.defaultRenderPipeline == null)
                throw new InvalidOperationException("URP pipeline is not assigned.");
            if (Physics2D.gravity != Vector2.zero)
                throw new InvalidOperationException("2D gravity must be zero.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows build support is unavailable.");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/environment-report.txt",
                "Unity: " + Application.unityVersion + "\n" +
                "Pipeline: " + GraphicsSettings.defaultRenderPipeline.name + "\n" +
                "Scene: " + ScenePath + "\n" +
                "Physics2D gravity: " + Physics2D.gravity + "\n" +
                "Fixed timestep: " + Time.fixedDeltaTime + "\n" +
                "Windows x64 build support: available\n" +
                "Input: Input System\n" +
                "Validated UTC: " + DateTime.UtcNow.ToString("O") + "\n");
            Debug.Log("EMERGE_VALIDATE_OK");
        }

        [MenuItem("Emerge/Build/Windows Development")]
        public static void BuildWindows()
        {
            Validate();
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Windows/Emerge.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText("Logs/build-report.txt", "Result: " + report.summary.result +
                "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings +
                "\nDuration: " + report.summary.totalTime + "\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + report.summary.result);
            Debug.Log("EMERGE_BUILD_OK");
        }
    }
}
