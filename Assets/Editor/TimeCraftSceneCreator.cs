#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TimeCraft.IP1;

namespace TimeCraft.IP1.Editor
{
    public static class TimeCraftSceneCreator
    {
        [MenuItem("TimeCraft IP1/Create Editable Prototype Scene")]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var manager = new GameObject("TimeCraft IP1 Prototype Manager");
            var prototype = manager.AddComponent<TimeCraftPrototypeManager>();
            prototype.BuildEditableScene();

            const string scenePath = "Assets/Scenes/TimeCraft_IP1_Editable_Prototype.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();

            var floor = GameObject.Find("Floor");
            Selection.activeGameObject = floor != null ? floor : manager;
            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }

            Debug.Log("Created editable TimeCraft IP1 scene at " + scenePath);
        }
    }
}
#endif
