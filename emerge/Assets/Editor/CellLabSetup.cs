using System.IO;
using Emerge.Cells;
using Emerge.Core;
using Emerge.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emerge.Editor
{
    public static class CellLabSetup
    {
        private const string Root = "Assets/_Emerge/";

        [MenuItem("Emerge/Cell Lab/Create T01 Assets and Scene")]
        public static void Configure()
        {
            Sprite circle = CreateSprite("CellCircle", 0);
            Sprite ring = CreateSprite("SelectionRing", 1);
            Sprite star = CreateSprite("CoreStar", 2);
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new System.InvalidOperationException("URP 2D unlit shader missing.");
            string materialPath = Root + "Art/Materials/CellPlaceholder.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            var core = Definition("Core", CellKind.Core, 0.72f, new Color(0.38f, 0.83f, 0.82f));
            var cilia = Definition("Cilia", CellKind.Cilia, 0.58f, new Color(0.57f, 0.73f, 0.92f));
            CellView corePrefab = Prefab(core, circle, ring, star, material);
            CellView ciliaPrefab = Prefab(cilia, circle, ring, star, material);

            AssetDatabase.SaveAssets();
            var scene = EditorSceneManager.OpenScene(Root + "Scenes/CellLab.unity", OpenSceneMode.Single);
            // Opening a scene can unload unused asset objects; resolve persistent references afterwards.
            core = AssetDatabase.LoadAssetAtPath<CellDefinition>(Root + "Data/Cells/Core.asset");
            cilia = AssetDatabase.LoadAssetAtPath<CellDefinition>(Root + "Data/Cells/Cilia.asset");
            corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/CoreCell.prefab").GetComponent<CellView>();
            ciliaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/CiliaCell.prefab").GetComponent<CellView>();
            if (core == null || cilia == null || corePrefab == null || ciliaPrefab == null)
                throw new System.InvalidOperationException("CellLab asset references are missing.");
            var lab = Object.FindAnyObjectByType<CellLabController>();
            if (lab == null) lab = new GameObject("CellLab").AddComponent<CellLabController>();
            var panel = lab.GetComponent<CellLabPanel>();
            if (panel == null) panel = lab.gameObject.AddComponent<CellLabPanel>();
            var panelSettings = new SerializedObject(panel);
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "Art/Fonts/NotoSansCJKsc-Regular.otf");
            if (font == null) throw new System.InvalidOperationException("Chinese font missing.");
            panelSettings.FindProperty("chineseFont").objectReferenceValue = font;
            panelSettings.ApplyModifiedPropertiesWithoutUndo();
            var settings = new SerializedObject(lab);
            settings.FindProperty("core").objectReferenceValue = core;
            settings.FindProperty("cilia").objectReferenceValue = cilia;
            settings.FindProperty("corePrefab").objectReferenceValue = corePrefab;
            settings.FindProperty("ciliaPrefab").objectReferenceValue = ciliaPrefab;
            settings.FindProperty("cellsRoot").objectReferenceValue = GameObject.Find("Organisms").transform;
            settings.FindProperty("worldCamera").objectReferenceValue = Camera.main;
            settings.FindProperty("panel").objectReferenceValue = panel;
            settings.FindProperty("connectionMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            settings.FindProperty("inputControls").objectReferenceValue = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(Root + "Data/EmergeControls.inputactions");
            settings.ApplyModifiedPropertiesWithoutUndo();
            Camera.main.orthographicSize = 6f;
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
            Debug.Log("CELL_LAB_T01_SETUP_OK");
        }

        public static void ConfigureAndBuild()
        {
            Configure();
            EmergeProjectSetup.BuildWindows();
        }

        private static CellDefinition Definition(string name, CellKind kind, float radius, Color color)
        {
            string path = Root + "Data/Cells/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<CellDefinition>(path);
            if (data != null)
            {
                data.displayName = kind == CellKind.Core ? "核心细胞" : "纤毛细胞";
                EditorUtility.SetDirty(data);
                return data;
            }
            data = ScriptableObject.CreateInstance<CellDefinition>();
            data.kind = kind; data.displayName = kind == CellKind.Core ? "核心细胞" : "纤毛细胞"; data.radius = radius; data.bodyColor = color;
            data.maxConnections = kind == CellKind.Core ? 6 : 4;
            data.mass = kind == CellKind.Core ? 1.4f : 1f;
            data.thrust = kind == CellKind.Core ? 0 : 3.5f;
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static Sprite CreateSprite(string name, int shape)
        {
            string path = Root + "Art/Sprites/" + name + ".png";
            if (!File.Exists(path))
            {
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float px = (x + 0.5f) / size - 0.5f;
                        float py = (y + 0.5f) / size - 0.5f;
                        float distance = Mathf.Sqrt(px * px + py * py);
                        float alpha = shape == 1 ? 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.45f) / 0.016f) :
                            shape == 2 ? Mathf.Clamp01((0.46f - Mathf.Sqrt(Mathf.Abs(px)) * Mathf.Sqrt(Mathf.Abs(py)) - (Mathf.Abs(px) + Mathf.Abs(py)) * 0.55f) * 80f) :
                            Mathf.Clamp01((0.47f - distance) * size);
                        texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                    }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 128;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static CellView Prefab(CellDefinition definition, Sprite circle, Sprite ring, Sprite star, Material material)
        {
            string path = Root + "Prefabs/Cells/" + definition.kind + "Cell.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<CellView>();
            var obj = new GameObject(definition.displayName + "Cell");
            obj.layer = LayerMask.NameToLayer("Cell");
            var view = obj.AddComponent<CellView>();
            SpriteRenderer body = Part(obj.transform, "Body", circle, material, Vector2.zero, Vector2.one, definition.bodyColor, 0);
            Part(obj.transform, "Highlight", circle, material, new Vector2(-0.16f, 0.18f), new Vector2(0.24f, 0.12f), new Color(1, 1, 1, 0.55f), 2);
            SpriteRenderer selection = Part(obj.transform, "Selection", ring, material, Vector2.zero, Vector2.one * 1.15f, new Color(0.98f, 0.88f, 0.49f), -1);
            if (definition.kind == CellKind.Core)
                Part(obj.transform, "Core glow", star, material, Vector2.zero, Vector2.one * 0.42f, new Color(1, 0.93f, 0.61f), 1);
            else
                for (int i = 0; i < 3; i++)
                    Part(obj.transform, "Cilia tail " + i, circle, material, new Vector2(0.55f, (i - 1) * 0.17f), new Vector2(0.5f, 0.095f), definition.bodyColor, -1);
            var fields = new SerializedObject(view);
            fields.FindProperty("body").objectReferenceValue = body;
            fields.FindProperty("selection").objectReferenceValue = selection.gameObject;
            fields.FindProperty("definition").objectReferenceValue = definition;
            fields.ApplyModifiedPropertiesWithoutUndo();
            selection.gameObject.SetActive(false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path).GetComponent<CellView>();
            Object.DestroyImmediate(obj);
            return prefab;
        }

        private static SpriteRenderer Part(Transform parent, string name, Sprite sprite, Material material, Vector2 position, Vector2 scale, Color color, int order)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(scale.x, scale.y, 1);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material;
            renderer.color = color; renderer.sortingOrder = order;
            return renderer;
        }
    }
}
