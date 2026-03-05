using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityEditor.Rendering.Converter
{
    [Serializable]
    internal abstract class AssetsConverter : IRenderPipelineConverter
    {
        protected abstract List<(string query, string description)> contextSearchQueriesAndIds { get; }
        public abstract bool isEnabled { get; }
        public abstract string isDisabledMessage { get; }

        internal List<RenderPipelineConverterAssetItem> assets = new();

        public void Scan(Action<List<IRenderPipelineConverterItem>> onScanFinish)
        {
            assets.Clear();
            void OnSearchFinish()
            {
                var returnList = new List<IRenderPipelineConverterItem>(assets.Count);
                foreach (var asset in assets)
                    returnList.Add(asset);
                onScanFinish?.Invoke(returnList);
            }

            var processedIds = new HashSet<string>();

            SearchServiceUtils.RunQueuedSearch
            (
                SearchServiceUtils.IndexingOptions.DeepSearch,
                contextSearchQueriesAndIds,
                (item, description) =>
                {
                    // Direct conversion - works for both assets and scene objects
                    var unityObject = item.ToObject();

                    if (unityObject == null)
                            return;

                    // Ensure we're always working with GameObjects
                    GameObject go = null;

                    if (unityObject is GameObject gameObject)
                        go = gameObject;
                    else if (unityObject is Component component)
                        go = component.gameObject;
                    else
                        return; // Not a GameObject or Component

                    var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                    if (!processedIds.Add(gid.ToString()))
                        return;

                    int type = gid.identifierType; // 1=Asset, 2=SceneObject

                    var assetItem = new RenderPipelineConverterAssetItem(gid.ToString())
                    {
                        name = $"{unityObject.name} ({(type == 1 ? "Prefab" : "SceneObject")})",
                        info = type == 1 ? AssetDatabase.GetAssetPath(unityObject) : go.scene.path,
                    };

                    assets.Add(assetItem);
                },
                OnSearchFinish
            );
        }

        public virtual void BeforeConvert() { }

        protected abstract Status ConvertObject(UnityEngine.Object obj, StringBuilder message);

        public Status Convert(IRenderPipelineConverterItem item, out string message)
        {
            var assetItem = item as RenderPipelineConverterAssetItem;

            var obj = assetItem.LoadObject();

            if (obj == null)
            {
                message = $"Failed to load {assetItem.name} Global ID {assetItem.GlobalObjectId} Asset Path {assetItem.assetPath}";
                return Status.Error;
            }

            var errorString = new StringBuilder();

            var status = ConvertObject(obj, errorString);
            message = errorString.ToString();
            return status;
        }

        public virtual void AfterConvert() { }
    }
}
