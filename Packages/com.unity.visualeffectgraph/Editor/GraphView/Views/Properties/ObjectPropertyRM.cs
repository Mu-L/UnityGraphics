using System;

using UnityEditor.SearchService;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.VFX.UI
{
    class ObjectPropertyRM : PropertyRM<UnityObject>
    {
        readonly ObjectField m_ObjectField;

        public ObjectPropertyRM(IPropertyRMProvider controller, float labelWidth) : base(controller, labelWidth)
        {
            if (m_Provider.portType.IsSubclassOf(typeof(Texture)))
            {
                m_ObjectField = new ObjectField(ObjectNames.NicifyVariableName(controller.name)) { objectType = typeof(Texture), allowSceneObjects = false };
                m_ObjectField.onObjectSelectorShow += OnShowObjectSelector;
            }
            else
            {
                m_ObjectField = new ObjectField(ObjectNames.NicifyVariableName(controller.name)) { objectType = m_Provider.portType, allowSceneObjects = false };
            }

            m_ObjectField.RegisterCallback<ChangeEvent<UnityObject>>(OnValueChanged);
            Add(m_ObjectField);

            if (m_Provider.portType == typeof(ShaderGraphVfxAsset))
            {
                var newButton = new Button(OnNewVFXShaderGraph) { name = "NewButton", text = "New..." };
                Add(newButton);
            }
            else if (m_Provider.portType == typeof(Shader))
            {
                var newButton = new Button(OnNewShaderGraph) { name = "NewButton", text = "New..." };
                Add(newButton);
            }
        }

        public override float GetPreferredControlWidth() => 140;

        public override void UpdateGUI(bool force)
        {
            m_ObjectField.value = m_Value;
        }

        public override void SetValue(object obj)
        {
            try
            {
                m_Value = (UnityObject)obj;
            }
            catch (Exception)
            {
                Debug.Log($"Error Trying to convert {obj?.GetType().Name ?? "null"} to Object");
            }

            UpdateGUI(!ReferenceEquals(m_Value, obj));
        }

        public override bool showsEverything => true;

        protected override void UpdateEnabled() => m_ObjectField.SetEnabled(propertyEnabled);

        protected override void UpdateIndeterminate() => visible = true;

        private void OnValueChanged(ChangeEvent<UnityObject> evt)
        {
            var newValueType = evt.newValue != null ? evt.newValue.GetType() : null;
            if (newValueType != null && newValueType != m_Provider.portType && (!typeof(RenderTexture).IsAssignableFrom(newValueType) || m_Provider.portType == typeof(CubemapArray)))
            {
                m_ObjectField.SetValueWithoutNotify(evt.previousValue);
            }
            else
            {
                SetValue(evt.newValue);
                NotifyValueChanged();
            }
        }

        private void OnShowObjectSelector()
        {
            var searchFilter = $"t:{m_Provider.portType.Name}";
            if (m_Provider.portType != typeof(CubemapArray))
            {
                searchFilter += ObjectSelectorSearch.HasEngineOverride()
                    ? " or t:RenderTexture"
                    : " t:RenderTexture";
            }

            ObjectSelector.get.searchFilter = searchFilter;
        }

        private void OnNewVFXShaderGraph() => OnNewShaderGraphCommon(typeof(ShaderGraphVfxAsset), "shadergraph.vfx=\"supported\"");
        private void OnNewShaderGraph() => OnNewShaderGraphCommon(typeof(Shader));

        private void OnNewShaderGraphCommon(Type shaderType, string hiddenQuery = null)
        {
            void OnTemplateCreated(string assetPath)
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    var shaderGraph = AssetDatabase.LoadAssetAtPath(assetPath, shaderType);
                    SetValue(shaderGraph);
                    NotifyValueChanged();
                }
            }

            CreateShaderGraph.CreateFromTemplate(OnTemplateCreated, hiddenQuery: hiddenQuery);
        }
    }
}
