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
            Debug.Log("CELL_LAB_T01_CHECK_START");
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
                    Debug.Log("CELL_LAB_T01_PASS: spawn, selection, reset, clear, capacity and cleanup.");
                }
                catch (Exception exception) { failure = exception.ToString(); }
            }
            if (failure != null) Debug.LogError("CELL_LAB_T01_FAIL: " + failure);
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
