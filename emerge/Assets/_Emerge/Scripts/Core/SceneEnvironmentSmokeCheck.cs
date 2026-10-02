#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Emerge.Presentation;
using Emerge.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    [Serializable]
    public sealed class SceneEnvironmentReport
    {
        public string timestampUtc, unity, failure;
        public bool passed;
        public int placedParticles, restoredParticles, localFlowSamples, flowVertices;
        public Vector2 translatedParticle;
    }
    public static class SceneEnvironmentSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool value, string message) { if (!value && Failure == null) Failure = message; }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new SceneEnvironmentReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            var previousMode = Physics2D.simulationMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            bool previousDisplay = lab.ShowFlow;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            var zoneObject = new GameObject("场景流场测试"); zoneObject.SetActive(false);
            var zone = zoneObject.AddComponent<SceneFlowRegion>(); zone.radius = 2; zone.speed = 1; zone.fadeAtEdge = false;
            zoneObject.transform.position = new Vector3(-5, -3, 0);
            var secondObject = new GameObject("叠加流场测试"); secondObject.SetActive(false);
            var second = secondObject.AddComponent<SceneFlowRegion>(); second.radius = 2; second.speed = 0.5f; second.fadeAtEdge = false;
            var patchObject = new GameObject("营养区域测试"); patchObject.SetActive(false);
            var patch = patchObject.AddComponent<SceneNutrientPatch>(); patch.count = 4; patch.size = new Vector2(1.2f, 0.8f); patch.amount = 0.3f;
            patchObject.transform.SetPositionAndRotation(new Vector3(4, -2, 0), Quaternion.Euler(0, 0, 90));
            var pointObject = new GameObject("单颗营养测试"); pointObject.SetActive(false);
            var point = pointObject.AddComponent<SceneNutrientPatch>(); point.count = 1; point.amount = 0.6f;
            pointObject.transform.position = new Vector3(-3, -3, 0);
            try
            {
                lab.ResetLab(); zoneObject.SetActive(true);
                Check(Vector2.Distance(lab.Food.FlowVelocityAt(new Vector2(-5, -3)), Vector2.right) < 0.001f,
                    "Placed flow region did not affect its center");
                Check(lab.Food.FlowVelocityAt(new Vector2(0, -3)) == Vector2.zero, "Placed region affected positions outside its radius");
                zoneObject.transform.rotation = Quaternion.Euler(0, 0, 90);
                Check(Vector2.Distance(lab.Food.FlowVelocityAt(new Vector2(-5, -3)), Vector2.up) < 0.001f, "Region rotation did not change flow");
                zoneObject.transform.position = new Vector3(-3, -3, 0);
                Check(lab.Food.FlowVelocityAt(new Vector2(-5, -3)).sqrMagnitude < 0.00001f, "Moving the region left flow at the old position");
                secondObject.transform.position = zoneObject.transform.position; secondObject.SetActive(true);
                Check(Vector2.Distance(lab.Food.FlowVelocityAt(new Vector2(-3, -3)), new Vector2(0.5f, 1)) < 0.001f,
                    "Scene flow contributions did not combine");
                patchObject.SetActive(true); pointObject.SetActive(true);
                report.placedParticles = lab.Food.Particles.Count;
                Check(report.placedParticles == 5, "Scene patches did not populate exactly once");
                for (int i = 0; i < 4; i++)
                    Check(Vector2.Distance(lab.Food.Particles[i].transform.position, patch.PositionFor(i)) < 0.001f &&
                        Mathf.Abs(lab.Food.Particles[i].Remaining - 0.3f) < 0.001f, "Rotated nutrient placement or amount failed");
                var mote = lab.Food.Particles[4]; Vector2 start = mote.transform.position;
                Check(Vector2.Distance(start, pointObject.transform.position) < 0.001f, "Single nutrient placement missed its scene position");
                patchObject.SetActive(false); patchObject.SetActive(true);
                Check(lab.Food.Particles.Count == 5, "Re-enabling a patch duplicated its food");
                lab.ToggleMode();
                for (int i = 0; i < 50; i++) { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); }
                report.translatedParticle = (Vector2)mote.transform.position - start;
                Check(Vector2.Distance(report.translatedParticle, new Vector2(0.5f, 1)) < 0.001f, "Placed flow did not advect placed nutrients");
                if (!lab.ShowFlow) lab.ToggleFlowDisplay();
                yield return null; yield return new WaitForEndOfFrame(); Directory.CreateDirectory(Folder);
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "scene-environment.png"));
                zone.enabled = false; second.enabled = false;
                Check(lab.Food.FlowVelocityAt(new Vector2(-3, -3)) == Vector2.zero, "Disabled regions retained flow");
                lab.ToggleMode(); lab.ResetLab(); report.restoredParticles = lab.Food.Particles.Count;
                bool restoredPoint = false;
                foreach (var particle in lab.Food.Particles)
                    restoredPoint |= Vector2.Distance(particle.transform.position, start) < 0.001f && Mathf.Abs(particle.Remaining - point.amount) < 0.001f;
                Check(report.restoredParticles == 5 && restoredPoint, "Reset failed to restore scene-authored nutrients");
                for (int i = 0; i < patch.count; i++)
                {
                    bool restored = false;
                    foreach (var particle in lab.Food.Particles)
                        restored |= Vector2.Distance(particle.transform.position, patch.PositionFor(i)) < 0.001f;
                    Check(restored, "Reset failed to restore a nutrient grid position");
                }
                lab.ClearLab(); Check(lab.Food.Particles.Count == 0, "Clear did not remove scene-generated nutrients");
                patchObject.SetActive(false); pointObject.SetActive(false);
                lab.LoadExample(LabExample.Filter); InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update();
                lab.ToggleMode(); lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime);
                yield return null; yield return new WaitForEndOfFrame();
                var flowView = UnityEngine.Object.FindAnyObjectByType<CellFlowView>();
                report.localFlowSamples = flowView.VisibleLocalSamples;
                report.flowVertices = flowView.GetComponent<MeshFilter>().sharedMesh.vertexCount;
                Check(report.localFlowSamples >= 24 && report.flowVertices > 300, "Cilia local flow visualization was missing or too sparse");
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "cilia-local-water.png"));
                Vector2 beforeHide = lab.Food.FlowVelocityAt(new Vector2(0, 1.4f));
                lab.ToggleFlowDisplay(); yield return null; yield return new WaitForEndOfFrame();
                Check(!flowView.GetComponent<MeshRenderer>().enabled && lab.Food.FlowVelocityAt(new Vector2(0, 1.4f)) == beforeHide,
                    "Hiding visualization changed flow or left it visible");
                lab.ToggleFlowDisplay(); InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime);
                yield return null; yield return new WaitForEndOfFrame();
                Check(flowView.VisibleLocalSamples == 0, "Released cilia left active local water arrows");
                report.passed = Failure == null; report.failure = Failure ?? "";
                File.WriteAllText(Path.Combine(Folder, "scene-environment-results.json"), JsonUtility.ToJson(report, true));
                if (report.passed) Debug.Log("CELL_LAB_SCENE_ENVIRONMENT_PASS: placed regions, rotation, translation, composition, disable, placed nutrient grid and point, actual advection, reset, no duplicate food, dense cilia flow feedback and display-only toggle.");
            }
            finally
            {
                zoneObject.SetActive(false); secondObject.SetActive(false); patchObject.SetActive(false); pointObject.SetActive(false);
                UnityEngine.Object.Destroy(zoneObject); UnityEngine.Object.Destroy(secondObject);
                UnityEngine.Object.Destroy(patchObject); UnityEngine.Object.Destroy(pointObject);
                if (!lab.IsEditing) lab.ToggleMode(); if (lab.ShowFlow != previousDisplay) lab.ToggleFlowDisplay();
                Physics2D.simulationMode = previousMode;
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground; lab.ResetLab();
            }
        }
    }
}
#endif
