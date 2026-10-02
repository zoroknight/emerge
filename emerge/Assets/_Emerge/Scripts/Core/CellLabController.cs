using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Emerge.Core
{
    public enum LabMode { Edit, Swim }

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
        private CellView dragged;
        private Vector3 dragOffset;
        public LabMode Mode { get; private set; } = LabMode.Edit;
        public bool IsEditing => Mode == LabMode.Edit;
        public bool IsDragging => dragged != null;
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
            if (!IsEditing || cells.Count >= capacity) return;
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
            if (!IsEditing) return;
            ClearLab();
            SpawnCore();
            SpawnCilia();
        }

        public void ClearLab()
        {
            if (!IsEditing) return;
            EndDrag();
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

        public void ToggleMode()
        {
            EndDrag();
            Mode = IsEditing ? LabMode.Swim : LabMode.Edit;
            panel.Refresh();
        }

        public void BeginDrag(CellView cell, Vector3 pointer)
        {
            if (!IsEditing || cell == null || !cells.Contains(cell)) return;
            Select(cell);
            dragged = cell;
            dragOffset = cell.transform.position - pointer;
        }

        public void MoveDrag(Vector3 pointer)
        {
            if (!IsEditing || dragged == null) return;
            Vector3 position = pointer + dragOffset;
            float radius = dragged.Definition.radius;
            Vector3 min = worldCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -worldCamera.transform.position.z));
            Vector3 max = worldCamera.ViewportToWorldPoint(new Vector3(1, 0.81f, -worldCamera.transform.position.z));
            position.x = Mathf.Clamp(position.x, min.x + radius, max.x - radius);
            position.y = Mathf.Clamp(position.y, min.y + radius, max.y - radius);
            position.z = 0;
            dragged.transform.position = position;
        }

        public void EndDrag() => dragged = null;

        public void RotateSelected(float degrees)
        {
            if (!IsEditing || selected == null) return;
            selected.transform.Rotate(0, 0, degrees);
            panel.Refresh();
        }

        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(); }
        private void OnDisable() => EndDrag();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleMode();
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnCore();
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnCilia();
                if (keyboard.backspaceKey.wasPressedThisFrame) ResetLab();
                if (keyboard.escapeKey.wasPressedThisFrame && !Application.isEditor) Application.Quit();
                float direction = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                if (direction != 0) RotateSelected(-direction * 90f * Time.deltaTime);
            }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed) EndDrag();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3 pointer = worldCamera.ScreenToWorldPoint(new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), -worldCamera.transform.position.z));
            if (IsDragging && mouse.leftButton.isPressed) MoveDrag(pointer);
            if (!overUI && mouse.scroll.y.ReadValue() != 0) RotateSelected(-Mathf.Sign(mouse.scroll.y.ReadValue()) * 15f);
            if (overUI || !mouse.leftButton.wasPressedThisFrame) return;
            CellView hit = null;
            for (int i = cells.Count - 1; i >= 0; i--)
                if (Vector2.Distance(pointer, cells[i].transform.position) <= cells[i].Definition.radius)
                { hit = cells[i]; break; }
            Select(hit);
            BeginDrag(hit, pointer);
        }
    }
}
