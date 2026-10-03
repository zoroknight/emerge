using System.IO;
using Emerge.Cells;
using Emerge.Core;
using Emerge.Presentation;
using Emerge.World;
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
            ExperimentSetup.EnsureDefaults();
            Sprite circle = CreateSprite("CellCircle", 0);
            Sprite ring = CreateSprite("SelectionRing", 1);
            Sprite star = CreateSprite("CoreStar", 2);
            Sprite strip = CreateSprite("MembraneStrip", 3);
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
            var absorber = Definition("Absorber", CellKind.Absorber, 0.65f, new Color(0.9f, 0.65f, 0.43f));
            var absorberPrefab = Prefab(absorber, circle, ring, star, material);
            var membrane = Definition("Membrane", CellKind.Membrane, 0.78f, new Color(0.70f, 0.55f, 0.88f));
            bool newMembrane = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/MembraneCell.prefab") == null;
            CellView membraneView = Prefab(membrane, circle, ring, star, material);
            if (newMembrane) ConfigureMembraneSurface(membraneView, strip, circle, material);
            var contractor = Definition("Contractor", CellKind.Contractor, 0.7f, new Color(0.92f, 0.48f, 0.62f));
            Prefab(contractor, circle, ring, star, material);
            ProvisionMetabolism(); ProvisionNutrient(circle, material);
            SceneEnvironmentSetup.EnsurePrefabs();
            string flowPath = Root + "Data/LocalFlow.asset";
            if (AssetDatabase.LoadAssetAtPath<LocalFlowSettings>(flowPath) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<LocalFlowSettings>(), flowPath);
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
            var experiments = ExperimentSetup.EnsureDefaults();
            var experimentRefs = settings.FindProperty("experimentTemplates"); experimentRefs.arraySize = experiments.Length;
            for (int i = 0; i < experiments.Length; i++) experimentRefs.GetArrayElementAtIndex(i).objectReferenceValue = experiments[i];
            settings.FindProperty("membrane").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CellDefinition>(Root + "Data/Cells/Membrane.asset");
            settings.FindProperty("membranePrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/MembraneCell.prefab").GetComponent<CellView>();
            settings.FindProperty("contractor").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CellDefinition>(Root + "Data/Cells/Contractor.asset");
            settings.FindProperty("contractorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/ContractorCell.prefab").GetComponent<CellView>();
            settings.FindProperty("absorber").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CellDefinition>(Root + "Data/Cells/Absorber.asset");
            settings.FindProperty("absorberPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Cells/AbsorberCell.prefab").GetComponent<CellView>();
            settings.FindProperty("metabolismSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MetabolismSettings>(Root + "Data/Metabolism.asset");
            settings.FindProperty("localFlowSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalFlowSettings>(flowPath);
            settings.FindProperty("nutrientPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/World/Nutrient.prefab").GetComponent<NutrientParticle>();
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
            SceneEnvironmentSetup.EnsureSceneExample(scene);
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
                data.displayName = CellName(kind);
                EditorUtility.SetDirty(data);
                return data;
            }
            data = ScriptableObject.CreateInstance<CellDefinition>();
            data.kind = kind; data.displayName = CellName(kind); data.radius = radius; data.bodyColor = color;
            data.maxConnections = kind == CellKind.Core ? 6 : 4;
            data.mass = kind == CellKind.Core ? 1.4f : 1f;
            data.thrust = kind == CellKind.Cilia ? 3.5f : 0;
            data.absorptionRate = kind == CellKind.Core ? 0.08f : kind == CellKind.Absorber ? 1.5f : 0;
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        private static string CellName(CellKind kind) => kind == CellKind.Core ? "核心细胞" :
            kind == CellKind.Cilia ? "纤毛细胞" : kind == CellKind.Membrane ? "膜细胞" : kind == CellKind.Contractor ? "收缩细胞" : "吸收细胞";

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
                        float alpha = shape == 3 ? 1 : shape == 1 ? 1f - Mathf.Clamp01(Mathf.Abs(distance - 0.45f) / 0.016f) :
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
            else if (definition.kind == CellKind.Cilia)
                for (int i = 0; i < 3; i++)
                    Part(obj.transform, "Cilia tail " + i, circle, material, new Vector2(0.55f, (i - 1) * 0.17f), new Vector2(0.5f, 0.095f), definition.bodyColor, -1);
            if (definition.kind == CellKind.Absorber)
            {
                Part(obj.transform, "Absorption opening", ring, material, Vector2.zero, Vector2.one * 0.65f, new Color(1, 0.9f, 0.55f), 2);
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.PI * 2 / 5;
                    Part(obj.transform, "Absorption petal " + i, circle, material, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.38f,
                        Vector2.one * 0.32f, definition.bodyColor, 1);
                }
            }
            if (definition.kind == CellKind.Membrane)
            {
                // The pale bar is the actual finite blocking surface (local Y).
                body.color = new Color(definition.bodyColor.r, definition.bodyColor.g, definition.bodyColor.b, 0.3f);
                Part(obj.transform, "Membrane surface", circle, material, Vector2.zero,
                    new Vector2(MembraneTransport.HalfThickness / definition.radius / 0.47f, 1.064f), new Color(0.93f, 0.82f, 1), 3);
            }
            if (definition.kind == CellKind.Contractor)
                for (int i = 0; i < 3; i++)
                    Part(obj.transform, "Contractile stripe " + i, circle, material, new Vector2((i - 1) * 0.2f, 0),
                        new Vector2(0.07f, 0.7f), new Color(1, 0.83f, 0.9f), 2);
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

        private static void ProvisionMetabolism()
        {
            string path = Root + "Data/Metabolism.asset";
            if (AssetDatabase.LoadAssetAtPath<MetabolismSettings>(path) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MetabolismSettings>(), path);
        }
        private static void ConfigureMembraneSurface(CellView template, Sprite strip, Sprite circle, Material material)
        {
            // Only provision the placeholder once; later art substitutions stay under user control.
            string path = AssetDatabase.GetAssetPath(template.gameObject);
            var obj = PrefabUtility.LoadPrefabContents(path);
            if (obj.transform.Find("Membrane end A") == null)
            {
                var surface = obj.transform.Find("Membrane surface");
                surface.GetComponent<SpriteRenderer>().sprite = strip;
                float half = MembraneTransport.HalfThickness / template.Definition.radius / 2;
                surface.localScale = new Vector3(half * 2, 1, 1);
                Part(obj.transform, "Membrane end A", circle, material, new Vector2(0, -0.5f), Vector2.one * (half / 0.47f), new Color(0.93f, 0.82f, 1), 3);
                Part(obj.transform, "Membrane end B", circle, material, new Vector2(0, 0.5f), Vector2.one * (half / 0.47f), new Color(0.93f, 0.82f, 1), 3);
                PrefabUtility.SaveAsPrefabAsset(obj, path);
            }
            PrefabUtility.UnloadPrefabContents(obj);
        }
        private static void ProvisionNutrient(Sprite circle, Material material)
        {
            string path = Root + "Prefabs/World/Nutrient.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            var obj = new GameObject("营养颗粒"); obj.layer = LayerMask.NameToLayer("Nutrient");
            var particle = obj.AddComponent<NutrientParticle>();
            var visual = obj.AddComponent<SpriteRenderer>(); visual.sprite = circle; visual.sharedMaterial = material;
            visual.color = new Color(0.85f, 1f, 0.45f); visual.sortingOrder = 2;
            var fields = new SerializedObject(particle); fields.FindProperty("visual").objectReferenceValue = visual; fields.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(obj, path); Object.DestroyImmediate(obj);
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
