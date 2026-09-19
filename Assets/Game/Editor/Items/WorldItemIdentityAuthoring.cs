using System;
using System.Collections.Generic;
using GoLive.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GoLive.Editor.Items
{
    public static class WorldItemIdentityAuthoring
    {
        [MenuItem("GO LIVE/Items/Prepare Loaded Scene Item IDs")]
        public static void PrepareLoadedScenes()
        {
            HashSet<string> usedIds = new(StringComparer.Ordinal);
            int assigned = 0;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);

                if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene))
                    continue;

                List<WorldItem> items = FindItems(scene);

                items.Sort((left, right) => string.CompareOrdinal(
                    GlobalObjectId.GetGlobalObjectIdSlow(left).ToString(),
                    GlobalObjectId.GetGlobalObjectIdSlow(right).ToString()));

                for (int i = 0; i < items.Count; i++)
                {
                    WorldItem item = items[i];

                    if (item.Definition == null)
                        throw new InvalidOperationException($"WorldItem '{item.name}' has no ItemDefinition.");

                    string currentId = item.AuthoredInstanceId;

                    if (!string.IsNullOrWhiteSpace(currentId) && usedIds.Add(currentId))
                        continue;

                    string newId;

                    do
                    {
                        newId = Guid.NewGuid().ToString("N");
                    }
                    while (!usedIds.Add(newId));

                    SerializedObject serializedItem = new(item);
                    serializedItem.FindProperty("authoredInstanceId").stringValue = newId;
                    serializedItem.ApplyModifiedProperties();

                    PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                    EditorSceneManager.MarkSceneDirty(scene);
                    assigned++;
                }
            }

            Debug.Log($"GO! LIVE item identity preparation complete. Assigned {assigned} persistent scene IDs.");
        }

        private static List<WorldItem> FindItems(Scene scene)
        {
            List<WorldItem> items = new();

            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
                items.AddRange(roots[i].GetComponentsInChildren<WorldItem>(true));

            return items;
        }
    }
}