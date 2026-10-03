using System.IO;
using System.Collections.Generic;
using Emerge.Core;
using UnityEditor;
using UnityEngine;

namespace Emerge.Editor
{
    public static class ExperimentSetup
    {
        public const string Folder = "Assets/_Emerge/Data/Experiments";
        public static CellExperiment[] EnsureDefaults()
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            var defaults = ExperimentCandidates.Defaults(); var assets = new CellExperiment[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                string path = Folder + "/" + defaults[i].id + ".asset";
                assets[i] = AssetDatabase.LoadAssetAtPath<CellExperiment>(path);
                if (assets[i] != null) continue;
                assets[i] = ScriptableObject.CreateInstance<CellExperiment>(); assets[i].data = defaults[i];
                AssetDatabase.CreateAsset(assets[i], path);
            }
            var all = new List<CellExperiment>(assets);
            foreach (var guid in AssetDatabase.FindAssets("t:CellExperiment"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CellExperiment>(AssetDatabase.GUIDToAssetPath(guid));
                if (!all.Contains(asset)) all.Add(asset);
            }
            AssetDatabase.SaveAssets(); return all.ToArray();
        }
    }
    public sealed class ExperimentEditorWindow : EditorWindow
    {
        private CellExperiment selected;
        private UnityEditor.Editor inspector;
        private Vector2 scroll;
        [MenuItem("Emerge/组合实验/打开实验库")]
        private static void Open() => GetWindow<ExperimentEditorWindow>("组合实验");
        [MenuItem("Emerge/组合实验/创建我的实验")]
        private static void Create()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建实验", "我的组合实验", "asset", "选择保存位置");
            if (string.IsNullOrEmpty(path)) return;
            var asset = CreateInstance<CellExperiment>();
            asset.data.title = "我的组合实验";
            asset.data.cells = new[] { new ExperimentCell { kind = Emerge.Cells.CellKind.Core, position = new Vector2(0,-1) } };
            AssetDatabase.CreateAsset(asset, path); AssetDatabase.SaveAssets(); Selection.activeObject = asset;
        }
        private void OnDisable() { if (inspector != null) DestroyImmediate(inspector); }
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Play 内点击右下角「组合实验 [B]」试验并保存布局。这里编辑预设数据；Cells/Links 的索引从 0 开始。只在核心直连出口配置信号。", MessageType.Info);
            if (GUILayout.Button("将实验资产加入当前场景菜单（非 Play）"))
            {
                var lab = FindAnyObjectByType<CellLabController>();
                if (!Application.isPlaying && lab != null)
                {
                    Undo.RecordObject(lab,"注册组合实验"); var fields = new SerializedObject(lab);
                    var assets = ExperimentSetup.EnsureDefaults(); var refs = fields.FindProperty("experimentTemplates"); refs.arraySize=assets.Length;
                    for(int i=0;i<assets.Length;i++) refs.GetArrayElementAtIndex(i).objectReferenceValue=assets[i];
                    fields.ApplyModifiedProperties(); UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
                }
            }
            if (GUILayout.Button("创建新的实验资产")) Create();
            foreach (var guid in AssetDatabase.FindAssets("t:CellExperiment"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<CellExperiment>(AssetDatabase.GUIDToAssetPath(guid));
                if (!GUILayout.Button(asset.data.title)) continue;
                selected = asset; Selection.activeObject = asset;
                if (inspector != null) DestroyImmediate(inspector);
                inspector = UnityEditor.Editor.CreateEditor(asset);
            }
            if (selected == null) return;
            if (GUILayout.Button("Play 中载入此实验"))
            {
                var lab = FindAnyObjectByType<CellLabController>();
                if (Application.isPlaying && lab != null && lab.Experiments != null)
                { if (!lab.IsEditing) lab.ToggleMode(); lab.Experiments.Load(selected.data); }
                else EditorUtility.DisplayDialog("组合实验", "请先打开 CellLab 场景并进入 Play。", "知道了");
            }
            if (GUILayout.Button("复制为我的实验"))
            {
                string path = EditorUtility.SaveFilePanelInProject("复制实验", selected.name + "-我的版本", "asset", "选择保存位置");
                if (!string.IsNullOrEmpty(path)) { AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(selected), path); AssetDatabase.SaveAssets(); }
            }
            scroll = EditorGUILayout.BeginScrollView(scroll); inspector.OnInspectorGUI(); EditorGUILayout.EndScrollView();
        }
    }
}
