using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Rendering.Analytics;
using UnityEditor.Rendering.Converter;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.Universal
{
    // This is the serialized class that stores the state of each item in the list of items to convert
    [Serializable]
    class ConverterItemState
    {
        public bool isSelected;
        public IRenderPipelineConverterItem item;
        public (Status Status, string Message) conversionResult = (Status.Pending, string.Empty);
        internal bool hasConverted => conversionResult.Status != Status.Pending;
    }

    // Each converter uses the active bool
    // Each converter has a list of active items/assets
    // We do this so that we can use the binding system of the UI Elements
    [Serializable]
    class ConverterState
    {
        public bool isSelected;
        public bool isLoading; // to name
        public bool isInitialized;
        public List<ConverterItemState> items = new List<ConverterItemState>();
        [SerializeReference]
        public IRenderPipelineConverter converter;

        private int CountItemWithFlag(Status status)
        {
            int count = 0;
            foreach (ConverterItemState itemState in items)
            {
                if (itemState.conversionResult.Status == status)
                {
                    count++;
                }
            }
            return count;
        }
        public int pending => CountItemWithFlag(Status.Pending);
        public int warnings => CountItemWithFlag(Status.Warning);
        public int errors => CountItemWithFlag(Status.Error);
        public int success => CountItemWithFlag(Status.Success);

        public override string ToString()
        {
            return $"Warnings: {warnings} - Errors: {errors} - Ok: {success} - Total: {items?.Count ?? 0}";
        }

        public void Clear()
        {
            isInitialized = false;
            items.Clear();
        }

        public int selectedItemsCount
        {
            get
            {
                int count = 0;
                foreach (ConverterItemState itemState in items)
                {
                    if (itemState.isSelected)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
    }

    class ConverterInfo
    {
        public IRenderPipelineConverter converter;
        public ConverterState state;

        public ConverterInfo(IRenderPipelineConverter converter, ConverterState state)
        {
            this.converter = converter;
            this.state = state;
        }

        public Type type => converter.GetType();
    }

    [Serializable]
    [EditorWindowTitle(title = "Render Pipeline Converters")]
    internal class RenderPipelineConvertersEditor : EditorWindow, IHasCustomMenu
    {
        public VisualTreeAsset converterEditorAsset;
        public VisualTreeAsset converterItem;
        public VisualTreeAsset converterWidgetMainAsset;

        ScrollView m_ScrollView;
        RenderPipelineConverterVisualElement m_ConverterSelectedVE;
        Button m_ConvertButton;
        Button m_InitButton;
        Button m_ContainerHelpButton;

        List<ConverterInfo> m_CoreConvertersList = new ();

        List<RenderPipelineConverterVisualElement> m_VEList = new ();

        internal static List<ConverterInfo> CategorizeConverters()
        {
            var elements = new List<ConverterInfo>();
            var manager = RenderPipelineConverterManager.instance;
            foreach (var state in manager.converterStates)
            {
                elements.Add(new ConverterInfo(state.converter, state));
            }
            return elements;
        }

        List<string> m_ContainerChoices = new List<string>();
        List<RenderPipelineConverterContainer> m_Containers = new List<RenderPipelineConverterContainer>();
        int m_ContainerChoiceIndex = 0;

        RenderPipelineConverterContainer currentContainer => m_Containers[m_ContainerChoiceIndex];

        [MenuItem("Window/Rendering/Render Pipeline Converter", false, 50)]
        public static void ShowWindow()
        {
            RenderPipelineConvertersEditor wnd = GetWindow<RenderPipelineConvertersEditor>();
            wnd.titleContent = new GUIContent("Render Pipeline Converter");
            DontSaveToLayout(wnd);
            wnd.minSize = new Vector2(650f, 400f);
            wnd.Show();
        }

        internal static void DontSaveToLayout(EditorWindow wnd)
        {
            // Making sure that the window is not saved in layouts.
            Assembly assembly = typeof(EditorWindow).Assembly;
            var editorWindowType = typeof(EditorWindow);
            var hostViewType = assembly.GetType("UnityEditor.HostView");
            var containerWindowType = assembly.GetType("UnityEditor.ContainerWindow");
            var parentViewField = editorWindowType.GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
            var parentViewValue = parentViewField.GetValue(wnd);
            // window should not be saved to layout
            var containerWindowProperty =
                hostViewType.GetProperty("window", BindingFlags.Instance | BindingFlags.Public);
            var parentContainerWindowValue = containerWindowProperty.GetValue(parentViewValue);
            var dontSaveToLayoutField =
                containerWindowType.GetField("m_DontSaveToLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            dontSaveToLayoutField.SetValue(parentContainerWindowValue, true);
        }

        void OnEnable()
        {
            GraphicsToolLifetimeAnalytic.WindowOpened<RenderPipelineConvertersEditor>();
        }

        private void OnDisable()
        {
            GraphicsToolLifetimeAnalytic.WindowClosed<RenderPipelineConvertersEditor>();
        }

        void InitIfNeeded()
        {
            if (m_CoreConvertersList.Count > 0)
                return;

            foreach (var containerType in TypeCache.GetTypesDerivedFrom<RenderPipelineConverterContainer>())
            {
                var container = (RenderPipelineConverterContainer)Activator.CreateInstance(containerType);
                m_Containers.Add(container);
            }

            // this need to be sorted by Priority property
            m_Containers = m_Containers
                .OrderBy(o => o.priority).ToList();

            foreach (var container in m_Containers)
            {
                m_ContainerChoices.Add(container.name);
            }

            m_CoreConvertersList = CategorizeConverters();                
        }

        public void CreateGUI()
        {
            InitIfNeeded();

            rootVisualElement.Clear();
            converterEditorAsset.CloneTree(rootVisualElement);

            rootVisualElement.Q<DropdownField>("conversionsDropDown").choices = m_ContainerChoices;
            rootVisualElement.Q<DropdownField>("conversionsDropDown").index = m_ContainerChoiceIndex;

            // Getting the scrollview where the converters should be added
            m_ScrollView = rootVisualElement.Q<ScrollView>("convertersScrollView");

            m_ConvertButton = rootVisualElement.Q<Button>("convertButton");
            m_ConvertButton.RegisterCallback<ClickEvent>(Convert);

            m_InitButton = rootVisualElement.Q<Button>("initializeButton");
            m_InitButton.RegisterCallback<ClickEvent>(InitializeAllActiveConverters);

            m_ContainerHelpButton = rootVisualElement.Q<Button>("containerHelpButton");
            m_ContainerHelpButton.RegisterCallback<ClickEvent>(GotoHelpURL);
            m_ContainerHelpButton.Q<Image>("containerHelpImage").image = CoreEditorStyles.iconHelp;
            m_ContainerHelpButton.RemoveFromClassList("unity-button");
            string theme = EditorGUIUtility.isProSkin ? "dark" : "light";
            m_ContainerHelpButton.AddToClassList(theme);

            // This is temp now to get the information filled in
            rootVisualElement.Q<DropdownField>("conversionsDropDown").RegisterCallback<ChangeEvent<string>>((evt) =>
            {
                m_ContainerChoiceIndex = rootVisualElement.Q<DropdownField>("conversionsDropDown").index;
                HideUnhideConverters();
            });

            m_VEList.Clear();
            foreach (var converterNode in m_CoreConvertersList)
            {
                RenderPipelineConverterVisualElement converterVisualElement = new RenderPipelineConverterVisualElement(converterNode);
                converterVisualElement.showMoreInfo += () => ShowConverterLayout(converterVisualElement);
                converterVisualElement.converterSelected += EnableOrDisableConvertButton;
                m_VEList.Add(converterVisualElement);
            }
            m_VEList.Sort((a, b) =>
            {
                int cmp = a.converter.priority.CompareTo(b.converter.priority);
                if (cmp == 0)
                    cmp = string.Compare(a.converter.name, b.converter.name, StringComparison.Ordinal);
                return cmp;
            });
            HideUnhideConverters();
            EnableOrDisableConvertButton();
        }

        private bool CanEnableConvert()
        {
            foreach (var ve in m_VEList)
            {
                if (ve.isActiveAndEnabled &&
                    ve.state.isInitialized &&
                    ve.state.selectedItemsCount > 0 &&
                    ve.state.pending > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private void EnableOrDisableConvertButton()
        {
            m_ConvertButton.SetEnabled(CanEnableConvert());
        }

        void GotoHelpURL(ClickEvent evt)
        {
            if (DocumentationUtils.TryGetHelpURL(currentContainer.GetType(), out var url))
            {
                Help.BrowseURL(url);
            }
        }

        void ShowConverterLayout(RenderPipelineConverterVisualElement element)
        {
            m_ConverterSelectedVE = element;
            
            rootVisualElement.Q<VisualElement>("converterEditorMainVE").style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("singleConverterVE").style.display = DisplayStyle.Flex;
            rootVisualElement.Q<VisualElement>("singleConverterVE").Add(element);

            m_ConverterSelectedVE.ShowConverterLayout();

            rootVisualElement.Q<Button>("backButton").RegisterCallback<ClickEvent>(BackToConverters);
        }

        void HideConverterLayout(VisualElement element)
        {
            rootVisualElement.Q<VisualElement>("converterEditorMainVE").style.display = DisplayStyle.Flex;
            rootVisualElement.Q<VisualElement>("singleConverterVE").style.display = DisplayStyle.None;
            rootVisualElement.Q<VisualElement>("singleConverterVE").Remove(element);

            m_ConverterSelectedVE.HideConverterLayout();

            m_ConverterSelectedVE = null;
        }

        void BackToConverters(ClickEvent evt)
        {
            HideConverterLayout(m_ConverterSelectedVE);
            HideUnhideConverters();
        }

        private void HideUnhideConverters()
        {
            rootVisualElement.Q<HelpBox>("conversionInfo").text = currentContainer.info + " Click the converters below to see more information.";

            var type = currentContainer.GetType();
            m_ContainerHelpButton.style.display = DocumentationUtils.TryGetHelpURL(type, out var url) ?
                DisplayStyle.Flex : DisplayStyle.None;

            m_ScrollView.Clear();
            foreach (var ve in m_VEList)
            {
                if (ve.converter.container == type)
                    m_ScrollView.Add(ve);
            }
        }

        void InitializeAllActiveConverters(ClickEvent evt)
        {
            if (!SaveCurrentSceneAndContinue())
                return;

            // Gather all the converters that are selected
            var selectedConverters = new List<RenderPipelineConverterVisualElement>();
            foreach (var converterVE in m_VEList)
            {
                if (converterVE.requiresInitialization)
                    selectedConverters.Add(converterVE);
            }

            int count = selectedConverters.Count;
            int iConverterIndex = 0;

            void InitializationFinish()
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                RefreshUI();
            }

            void ProcessNextConverter()
            {
                // Check if all the converters did finish
                if (iConverterIndex >= count)
                {
                    InitializationFinish();
                    return;
                }

                var current = selectedConverters[iConverterIndex];
                var converter = current.converter;

                if (EditorUtility.DisplayCancelableProgressBar("Initializing converters", $"({iConverterIndex} of {count}) {converter.name}", (float)iConverterIndex / (float)count))
                {
                    InitializationFinish();
                    return;
                }

                List<ConverterItemDescriptor> converterItemInfos = new List<ConverterItemDescriptor>();
                void OnConverterScanFinished()
                {
                    // Try to execute the next converter
                    ++iConverterIndex;
                    ProcessNextConverter();
                }

                current.Scan(OnConverterScanFinished);
            }

            ProcessNextConverter();
        }

        private void RefreshUI()
        {
            foreach (var converterVE in m_VEList)
            {
                converterVE.Refresh();
            }

            EnableOrDisableConvertButton();
        }

        private bool SaveCurrentSceneAndContinue()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.isDirty)
            {
                if (EditorUtility.DisplayDialog("Scene is not saved.",
                    "Current scene is not saved. Please save the scene before continuing.", "Save and Continue",
                    "Cancel"))
                {
                    EditorSceneManager.SaveScene(currentScene);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        struct AnalyticContextInfo
        {
            public string converter_id;
            public int items_count;
        }

        void Convert(ClickEvent evt)
        {
            // Ask to save save the current open scene and after the conversion is done reload the same scene.
            if (!SaveCurrentSceneAndContinue()) return;

            string currentScenePath = SceneManager.GetActiveScene().path;

            List<RenderPipelineConverterVisualElement> activeConverterStates = new ();

            // Getting all the active converters to use in the cancelable progressbar
            foreach (var ve in m_VEList)
            {
                if (ve.isActiveAndEnabled && ve.state.isInitialized)
                {
                    activeConverterStates.Add(ve);
                }
            }

            List<AnalyticContextInfo> contextInfo = new ();

            int converterCount = 1;
            int activeConvertersCount = activeConverterStates.Count;
            foreach (var activeConverter in activeConverterStates)
            {
                activeConverter.Convert($"({converterCount} of {activeConvertersCount}) {activeConverter.displayName}");
                converterCount++;

                // Add this converter to the analytics
                contextInfo.Add(new()
                {
                    converter_id = activeConverter.displayName,
                    items_count = 0
                });

                AssetDatabase.SaveAssets();
                EditorUtility.ClearProgressBar();
            }

            // Checking if we have changed current scene. If we have we reload the old scene we started from
            if (!string.IsNullOrEmpty(currentScenePath) && currentScenePath != SceneManager.GetActiveScene().path)
            {
                EditorSceneManager.OpenScene(currentScenePath);
            }

            RefreshUI();

            GraphicsToolUsageAnalytic.ActionPerformed<RenderPipelineConvertersEditor>(nameof(Convert), contextInfo.ToNestedColumn());
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem
            (
                EditorGUIUtility.TrTextContent("Reset"),
                false,
                () =>
                {
                    RenderPipelineConverterManager.instance.Reset();
                    m_CoreConvertersList.Clear();
                    CreateGUI();
                }
            );
        }
    }
}
