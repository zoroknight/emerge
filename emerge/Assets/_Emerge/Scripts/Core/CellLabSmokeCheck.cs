#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Emerge.Core
{
    public sealed class CellLabSmokeCheck : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            Debug.Log("CELL_LAB_T02_CHECK_START");
            string failure = null;
            try
            {
                var lab = FindAnyObjectByType<CellLabController>();
                Require(lab != null && lab.Cells.Count == 2, "Initial core and cilia missing.");
                Require(lab.Cells[0].Definition.kind == CellKind.Core && lab.Cells[1].Definition.kind == CellKind.Cilia, "Initial types incorrect.");
                lab.Panel.CoreButton.onClick.Invoke();
                lab.Panel.CiliaButton.onClick.Invoke();
                Require(lab.Cells.Count == 4, "Spawn button wiring failed.");
                lab.Select(lab.Cells[0]);
                Require(lab.Selected == lab.Cells[0], "Selection failed.");
                var cell = lab.Selected;
                Vector3 initial = cell.transform.position;
                Vector3 grab = initial + new Vector3(0.2f, 0.1f, 0);
                lab.BeginDrag(cell, grab);
                lab.MoveDrag(grab + new Vector3(1, -1, 0));
                Require(Vector3.Distance(cell.transform.position, initial + new Vector3(1, -1, 0)) < 0.001f, "Drag offset failed.");
                lab.EndDrag();
                var released = cell.transform.position;
                lab.MoveDrag(Vector3.zero);
                Require(cell.transform.position == released && !lab.IsDragging, "Drag release failed.");
                lab.RotateSelected(90);
                Require(Mathf.Abs(Mathf.DeltaAngle(cell.transform.eulerAngles.z, 90)) < 0.01f, "Rotation failed.");
                lab.BeginDrag(cell, cell.transform.position);
                lab.Panel.ModeButton.onClick.Invoke();
                Require(!lab.IsEditing && !lab.IsDragging, "Mode switch left active drag.");
                lab.MoveDrag(Vector3.zero); lab.BeginDrag(cell, Vector3.zero); lab.RotateSelected(45);
                lab.SpawnCore(); lab.ResetLab(); lab.ClearLab();
                Require(cell.transform.position == released && Mathf.Abs(Mathf.DeltaAngle(cell.transform.eulerAngles.z, 90)) < 0.01f && lab.Cells.Count == 4 && !lab.IsDragging,
                    "Swim mode allowed layout edits.");
                Require(!lab.Panel.CoreButton.interactable && !lab.Panel.ResetButton.interactable && !lab.Panel.ClearButton.interactable, "Swim UI lock failed.");
                lab.Panel.ModeButton.onClick.Invoke();
                Require(lab.IsEditing && lab.Panel.ResetButton.interactable && cell.transform.position == released, "Return to edit changed layout.");
                lab.BeginDrag(cell, cell.transform.position);
                lab.MoveDrag(new Vector3(1000, -1000, 0));
                Require(cell.transform.position.x < 20 && cell.transform.position.y > -6, "Drag bounds failed.");
                lab.ResetLab();
                Require(!lab.IsDragging, "Reset left active drag.");
                var labels = lab.Panel.GetComponentsInChildren<UnityEngine.UI.Text>();
                bool chineseTitle = false;
                foreach (var label in labels)
                {
                    if (label.text == "细胞实验室") chineseTitle = true;
                    foreach (char glyph in label.text)
                        if (glyph > 127 && !char.IsWhiteSpace(glyph)) Require(label.font.HasCharacter(glyph), "Missing Chinese glyph: " + glyph);
                }
                Require(chineseTitle && lab.Cells[0].Definition.displayName == "核心细胞", "Chinese localization failed.");
                for (int i = 0; i < 3; i++) lab.Panel.ResetButton.onClick.Invoke();
                Require(lab.Cells.Count == 2, "Repeated reset failed.");
                lab.Panel.ClearButton.onClick.Invoke();
                Require(lab.Cells.Count == 0 && lab.Selected == null, "Clear left stale state.");
                for (int i = 0; i < lab.Capacity + 5; i++) lab.SpawnCilia();
                Require(lab.Cells.Count == lab.Capacity && !lab.Panel.CiliaButton.interactable, "Capacity limit failed.");
                lab.ResetLab();
            }
            catch (Exception exception) { failure = exception.ToString(); }
            yield return null;
            if (failure == null)
            {
                var views = FindObjectsByType<CellView>();
                if (views.Length != 2) failure = "Reset left orphan active samples: " + views.Length;
                var lab = FindAnyObjectByType<CellLabController>();
                if (lab.Cells[0].transform.parent.childCount != 2) failure = "Reset left orphan samples in hierarchy.";
            }
            if (failure == null)
            {
                var lab = FindAnyObjectByType<CellLabController>();
                lab.SpawnCore(); lab.SpawnCilia();
                yield return new WaitForEndOfFrame();
                string[] args = Environment.GetCommandLineArgs();
                int screenshotArg = Array.IndexOf(args, "--cell-lab-screenshot");
                string screenshot = screenshotArg >= 0 && screenshotArg + 1 < args.Length ? args[screenshotArg + 1] : Path.Combine(Application.persistentDataPath, "cell-lab-t01.png");
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(screenshot)));
                try
                {
                    CapturePreview(screenshot);
                    Debug.Log("CELL_LAB_T02_PASS: T01 regression, drag offset/release/bounds, rotation, mode lock/cancel, Chinese font and cleanup.");
                }
                catch (Exception exception) { failure = exception.ToString(); }
            }
            if (failure != null) Debug.LogError("CELL_LAB_T02_FAIL: " + failure);
            Application.Quit(failure == null ? 0 : 1);
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void CapturePreview(string path)
        {
            // Render explicitly: a hidden Windows swap chain can return an all-black screenshot.
            Camera camera = Camera.main;
            var canvas = FindAnyObjectByType<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            camera.aspect = 1280f / 720f;
            Canvas.ForceUpdateCanvases();
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(texture);
            }
        }
    }
}
#endif
