using Emerge.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Editor
{
    public static class SceneEnvironmentSetup
    {
        private const string Root = "Assets/_Emerge/Prefabs/World/";
        public static void EnsurePrefabs()
        {
            Create("FlowRegion", true, false);
            Create("NutrientPatch", false, false);
            Create("NutrientPoint", false, true);
        }
        private static void Create(string name, bool flow, bool point)
        {
            string path = Root + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var obj = new GameObject(flow ? "局部环境流场" : point ? "营养放置点" : "营养区域");
            if (flow) obj.AddComponent<SceneFlowRegion>();
            else
            {
                var patch = obj.AddComponent<SceneNutrientPatch>();
                if (point) { patch.count = 1; patch.size = Vector2.zero; }
            }
            PrefabUtility.SaveAsPrefabAsset(obj, path); Object.DestroyImmediate(obj);
        }
        public static void EnsureSceneExample(Scene scene)
        {
            const string title = "场景环境示例（启用后投放）";
            foreach (var obj in scene.GetRootGameObjects()) if (obj.name == title) return;
            var parent = new GameObject(title);
            parent.transform.position = new Vector3(-6, -1, 0);
            var flow = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "FlowRegion.prefab"), scene);
            flow.transform.SetParent(parent.transform, false);
            var patch = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "NutrientPatch.prefab"), scene);
            patch.transform.SetParent(parent.transform, false); patch.transform.localPosition = new Vector3(-0.6f, 0, 0);
            parent.SetActive(false);
        }
        [MenuItem("GameObject/Emerge/局部环境流场", false, 10)]
        private static void AddFlow() => Place("FlowRegion");
        [MenuItem("GameObject/Emerge/营养区域", false, 11)]
        private static void AddPatch() => Place("NutrientPatch");
        [MenuItem("GameObject/Emerge/单颗营养放置点", false, 12)]
        private static void AddPoint() => Place("NutrientPoint");
        private static void Place(string name)
        {
            EnsurePrefabs();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + name + ".prefab"));
            Undo.RegisterCreatedObjectUndo(obj, "放置场景环境对象"); Selection.activeGameObject = obj;
        }
    }
}
