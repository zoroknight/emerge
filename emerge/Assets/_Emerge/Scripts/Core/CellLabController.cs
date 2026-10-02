using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Emerge.Core
{
    public sealed class CellLabController : MonoBehaviour
    {
        [SerializeField] private CellDefinition core;
        [SerializeField] private CellDefinition cilia;
        [SerializeField] private CellView corePrefab;
        [SerializeField] private CellView ciliaPrefab;
        [SerializeField] private Transform cellsRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CellLabPanel panel;
        [SerializeField, Range(2, 30)] private int capacity = 20;

        private readonly List<CellView> cells = new List<CellView>();
        private CellView selected;
        public IReadOnlyList<CellView> Cells => cells;
        public int Capacity => capacity;
        public CellView Selected => selected;
        public CellLabPanel Panel => panel;

        private void Start()
        {
            panel.Initialize(this);
            ResetLab();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--cell-lab-smoke") >= 0)
            {
                Application.runInBackground = true;
                gameObject.AddComponent<CellLabSmokeCheck>();
            }
#endif
        }

        public void SpawnCore() => Spawn(core, corePrefab);
        public void SpawnCilia() => Spawn(cilia, ciliaPrefab);

        private void Spawn(CellDefinition data, CellView prefab)
        {
            if (cells.Count >= capacity) return;
            int slot = cells.Count;
            // Fixed slots keep every sample visible and separate during T01.
            Vector3 position = new Vector3(-5.1f + slot % 5 * 2.55f, 2.8f - slot / 5 * 2.15f, 0);
            CellView cell = Instantiate(prefab, position, Quaternion.identity, cellsRoot);
            cell.name = data.displayName + " " + (slot + 1);
            cell.Initialize(data);
            cells.Add(cell);
            Select(cell);
        }

        public void ResetLab()
        {
            ClearLab();
            SpawnCore();
            SpawnCilia();
        }

        public void ClearLab()
        {
            selected = null;
            foreach (CellView cell in cells)
            {
                // Destroy is deferred; hide samples immediately before respawning.
                cell.gameObject.SetActive(false);
                Destroy(cell.gameObject);
            }
            cells.Clear();
            panel.Refresh();
        }

        public void Select(CellView cell)
        {
            if (selected != null) selected.SetSelected(false);
            selected = cell;
            if (selected != null) selected.SetSelected(true);
            panel.Refresh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnCore();
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnCilia();
                if (keyboard.backspaceKey.wasPressedThisFrame) ResetLab();
                if (keyboard.escapeKey.wasPressedThisFrame && !Application.isEditor) Application.Quit();
            }
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            Vector3 pointer = worldCamera.ScreenToWorldPoint(new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), -worldCamera.transform.position.z));
            CellView hit = null;
            for (int i = cells.Count - 1; i >= 0; i--)
                if (Vector2.Distance(pointer, cells[i].transform.position) <= cells[i].Definition.radius)
                { hit = cells[i]; break; }
            Select(hit);
        }
    }
}
