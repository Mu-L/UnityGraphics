using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using UnityEngine.TestTools.Graphics.Contexts;
using UnityEngine.TestTools.Graphics.Platforms;
using Object = UnityEngine.Object;
#if OCULUS_SDK || OPENXR_SDK
using UnityEngine.XR;
#endif
#if UNITY_EDITOR
using UnityEditor.TestTools.Graphics;
#endif

namespace Unity.Rendering.Universal.Tests
{
    public class UniversalGraphicsTestBase
    {
        protected readonly GpuResidentDrawerGlobalContext gpuResidentDrawerContext;
        protected readonly GpuResidentDrawerContext requestedGRDContext;
        protected readonly GpuResidentDrawerContext previousGRDContext;

        public UniversalGraphicsTestBase(GpuResidentDrawerContext grdContext)
        {
            requestedGRDContext = grdContext;

            gpuResidentDrawerContext = GlobalContextManager.Get<GpuResidentDrawerGlobalContext>();

            previousGRDContext = (GpuResidentDrawerContext)gpuResidentDrawerContext.Current;

            gpuResidentDrawerContext.Activate(requestedGRDContext);
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Standard resolution for backbuffer capture is 1080p
            Screen.SetResolution(1920, 1080, true);

            #if UNITY_EDITOR
            GameViewSize.SetGameViewSize(1920, 1080);
            #endif

            SceneManager.LoadScene("GraphicsTestTransitionScene", LoadSceneMode.Single);
        }

        [UnityOneTimeSetUp]
        public IEnumerator OneTimeSetup()
        {
            // Necessary because of inconsistencies on some platforms
            yield return GlobalResolutionSetter.SetResolutionWithRetry(1920, 1080, true);

            yield return TestContentLoader.WaitForContentLoadAsync(TimeSpan.FromSeconds(240));
        }

        [SetUp]
        public void SetUpContext()
        {
            gpuResidentDrawerContext.Activate(requestedGRDContext);

            GlobalContextManager.AssertContextIs<GpuResidentDrawerGlobalContext>(requestedGRDContext);
        }

        [TearDown]
        public void TearDown()
        {
            Debug.ClearDeveloperConsole();
#if ENABLE_VR
            XRGraphicsAutomatedTests.running = false;
#endif
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            SceneManager.LoadScene("GraphicsTestTransitionScene", LoadSceneMode.Single);

            gpuResidentDrawerContext.Activate(previousGRDContext);
        }
    }

    public class UniversalTestFixtureData
    {
        public static IEnumerable FixtureParams
        {
            get
            {
                yield return new TestFixtureData(
                    GpuResidentDrawerContext.GRDDisabled
                );

                if (GraphicsTestPlatform.Current.IsEditorPlatform)
                {
                    yield return new TestFixtureData(
                        GpuResidentDrawerContext.GRDEnabled
                    );
                }
            }
        }
    }
}
