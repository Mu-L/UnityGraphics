using System.Collections;
using System.Linq;

using NUnit.Framework;

using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements.TestFramework;
using UnityEditor.VFX.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace UnityEditor.VFX.Test
{
    class VFXViewWindowTests : EditorWindowUITestFixture<VFXViewWindow>
    {
        public VFXViewWindowTests()
        {
            createWindowFunction = () => VFXViewWindow.GetWindow((VFXGraph)null, true);
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            VFXViewWindow.GetAllWindows().ToList().ForEach(x => x.Close());
        }

        [OneTimeTearDown]
        public void DestroyTestAssets()
        {
            VFXTestCommon.DeleteAllTemporaryGraph();
        }

        [TearDown]
        public void TearDown()
        {
            if (EditorWindow.HasOpenInstances<GraphViewTemplateWindow>())
            {
                EditorWindow.GetWindow<GraphViewTemplateWindow>().Close();
            }
        }

        [UnityTest]
        public IEnumerator Create_New_ShaderGraph_Output_Context()
        {
            const int kTemplateWindowTimeoutInMs = 5000;

            var graph = VFXTestCommon.MakeTemporaryGraph();
            var viewController = VFXViewController.GetController(graph.GetResource(), true);
            window.graphView.controller = viewController;
            var outputContextDesc = VFXLibrary.GetContexts().First(x => x.modelType == typeof(VFXComposedParticleOutput));
            viewController.AddVFXContext(new Vector2(300, 2000), outputContextDesc.variant);
            viewController.ApplyChanges();
            simulate.FrameUpdate();
            window.graphView.FrameAll();
            simulate.FrameUpdate();

            Debug.Log($"view size: {window.graphView.contentRect.size}");
            var contextUI = window.graphView.Query<VFXContextUI>().First();
            var newButton = contextUI.Q<Button>("NewButton");
            Assert.NotNull(newButton);
            simulate.Click(newButton);

            var isTemplateWindowOpen = EditorWindow.HasOpenInstances<GraphViewTemplateWindow>();
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            while (!isTemplateWindowOpen && sw.ElapsedMilliseconds < kTemplateWindowTimeoutInMs)
            {
                yield return null;
                isTemplateWindowOpen = EditorWindow.HasOpenInstances<GraphViewTemplateWindow>();
            }
            sw.Stop();
            Assert.IsTrue(isTemplateWindowOpen, $"Template window did not open within {sw.ElapsedMilliseconds} ms after clicking the New Button.");

            var templateWindow = EditorWindow.GetWindow<GraphViewTemplateWindow>();
            yield return GraphViewTemplateWindowHelpers.WaitUntilTemplatesAreCollected(templateWindow);
            simulate.FrameUpdate();

            var templateTree = GraphViewTemplateWindowHelpers.GetTemplateTree(templateWindow);
            Assert.AreEqual(1, templateTree.Count);
            Assert.AreEqual(3, templateTree[0].children.Count());
        }
    }
}
