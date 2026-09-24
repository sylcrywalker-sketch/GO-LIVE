using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoLive.Editor.Lighting
{
    public static class PracticalLightMigrationMenu
    {
        [MenuItem("GO LIVE/Lighting/Migrate Practical Light Intensities")]
        public static void MigrateProject()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("Save the open scenes before migrating Practical Lights: the migration rewrites scene files.");
                    return;
                }
            }

            AssetDatabase.SaveAssets();

            Dictionary<string, string> files = new();
            Dictionary<string, string> guids = new();

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (!path.EndsWith(".prefab") && !path.EndsWith(".unity"))
                    continue;

                files[path] = File.ReadAllText(path, Encoding.UTF8);
                guids[path] = guid;
            }

            PracticalLightMigration.Result result = PracticalLightMigration.Migrate(files, guids);

            if (!result.Succeeded)
            {
                Debug.LogError("Practical Light migration changed nothing:\n" + string.Join("\n", result.Errors));
                return;
            }

            SceneSetup[] openScenes = EditorSceneManager.GetSceneManagerSetup();
            bool openSceneChanged = false;

            foreach (SceneSetup scene in openScenes)
                openSceneChanged |= result.ChangedFiles.ContainsKey(scene.path);

            foreach (KeyValuePair<string, string> file in result.ChangedFiles)
                File.WriteAllText(file.Key, file.Value, new UTF8Encoding(false));

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            // Reopen the open scenes from disk so the Editor does not keep (and later save) its old copy.
            if (openSceneChanged)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.RestoreSceneManagerSetup(openScenes);
            }

            Debug.Log(result.ChangedFiles.Count == 0
                ? "Practical Lights are already migrated."
                : $"Practical Light migration updated {result.ChangedFiles.Count} file(s):\n" + string.Join("\n", result.Changes));
        }
    }
}
