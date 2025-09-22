using System;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UnityEditor.Rendering.Converter
{
    [Serializable]
    internal class RenderPipelineConverterAssetItem : IRenderPipelineConverterItem
    {
        public string assetPath { get; }
        public string guid { get; }

        public string name => assetPath;

        public string info => guid;

        public bool isEnabled { get; set; }
        public string isDisabledMessage { get; set; }

        public RenderPipelineConverterAssetItem(string id)
        {
            if (!GlobalObjectId.TryParse(id, out var gid))
                throw new ArgumentException(nameof(id), $"Unable to perform GlobalObjectId.TryParse with the given id {id}");

            assetPath = AssetDatabase.GUIDToAssetPath(gid.assetGUID);
            guid = gid.ToString();
        }

        public RenderPipelineConverterAssetItem(GlobalObjectId gid, string assetPath)
        {
            if (!AssetDatabase.AssetPathExists(assetPath))
                throw new ArgumentException(nameof(assetPath), $"{assetPath} does not exist");

            this.assetPath = assetPath;
            guid = gid.ToString();
        }

        public UnityEngine.Object LoadObject()
        {
            UnityEngine.Object obj = null;

            if (GlobalObjectId.TryParse(guid, out var globalId))
            {
                // Try loading the object
                // TODO: Upcoming changes to GlobalObjectIdentifierToObjectSlow will allow it
                //       to return direct references to prefabs and their children.
                //       Once that change happens there are several items which should be adjusted.
                obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);

                // If the object was not loaded, it is probably part of an unopened scene or prefab;
                // if so, then the solution is to first load the scene here.
                var objIsInSceneOrPrefab = globalId.identifierType == 2; // 2 is IdentifierType.kSceneObject
                if (!obj &&
                    objIsInSceneOrPrefab)
                {
                    // Open the Containing Scene Asset or Prefab in the Hierarchy so the Object can be manipulated
                    var mainAssetPath = AssetDatabase.GUIDToAssetPath(globalId.assetGUID);
                    var mainAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(mainAssetPath);
                    AssetDatabase.OpenAsset(mainAsset);

                    // If a prefab stage was opened, then mainAsset is the root of the
                    // prefab that contains the target object, so reference that for now,
                    // until GlobalObjectIdentifierToObjectSlow is updated
                    if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                    {
                        obj = mainAsset;
                    }

                    // Reload object if it is still null (because it's in a previously unopened scene)
                    if (!obj)
                    {
                        obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                    }
                }
            }

            return obj;
        }

        public void OnClicked()
        {
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath));
        }
    }
}
