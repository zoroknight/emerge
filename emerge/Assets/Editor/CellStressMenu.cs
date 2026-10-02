using UnityEditor;
using UnityEditor.SceneManagement;

namespace Emerge.Editor
{
    public static class CellStressMenu
    {
        [MenuItem("Emerge/Tests/Run T05 Stress Suite")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/_Emerge/Scenes/CellLab.unity");
            SessionState.SetBool("Emerge.RunT05", true);
            EditorApplication.isPlaying = true;
        }
    }
}
