#if UNITY_EDITOR && (!UNITY_EDITOR_OSX || MAC_FORCE_TESTS)

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor.VFX.Block.Test;
using UnityEditor.VFX.UI;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UnityEditor.VFX.Test
{
    [TestFixture]
    public class VisualEffectTest
    {
        GameObject m_cubeEmpty;
        GameObject m_sphereEmpty;

        string m_pathTexture2D_A;
        string m_pathTexture2D_B;
        Texture2D m_texture2D_A;
        Texture2D m_texture2D_B;
        string m_pathTexture2DArray_A;
        string m_pathTexture2DArray_B;
        Texture2DArray m_texture2DArray_A;
        Texture2DArray m_texture2DArray_B;
        string m_pathTexture3D_A;
        string m_pathTexture3D_B;
        Texture3D m_texture3D_A;
        Texture3D m_texture3D_B;
        string m_pathTextureCube_A;
        string m_pathTextureCube_B;
        Cubemap m_textureCube_A;
        Cubemap m_textureCube_B;
        string m_pathTextureCubeArray_A;
        string m_pathTextureCubeArray_B;
        CubemapArray m_textureCubeArray_A;
        CubemapArray m_textureCubeArray_B;

        [OneTimeSetUp]
        public void Init()
        {
            VFXTestCommon.CloseAllUnecessaryWindows();

            var cubeEmptyName = "VFX_Test_Cube_Empty_Name";
            var sphereEmptyName = "VFX_Test_Sphere_Empty_Name";

            System.IO.Directory.CreateDirectory(VFXTestCommon.tempBasePath);

            m_cubeEmpty = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_cubeEmpty.name = cubeEmptyName;
            m_sphereEmpty = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_sphereEmpty.name = sphereEmptyName;

            m_pathTexture2D_A = VFXTestCommon.tempBasePath + "/texture2D_A.asset";
            m_pathTexture2D_B = VFXTestCommon.tempBasePath + "/texture2D_B.asset";
            m_texture2D_A = new Texture2D(16, 16);
            m_texture2D_B = new Texture2D(32, 32);
            AssetDatabase.CreateAsset(m_texture2D_A, m_pathTexture2D_A);
            AssetDatabase.CreateAsset(m_texture2D_B, m_pathTexture2D_B);
            m_texture2D_A = AssetDatabase.LoadAssetAtPath<Texture2D>(m_pathTexture2D_A);
            m_texture2D_B = AssetDatabase.LoadAssetAtPath<Texture2D>(m_pathTexture2D_B);

            m_pathTexture2DArray_A = VFXTestCommon.tempBasePath + "texture2DArray_A.asset";
            m_pathTexture2DArray_B = VFXTestCommon.tempBasePath + "texture2DArray_B.asset";
            m_texture2DArray_A = new Texture2DArray(16, 16, 4, TextureFormat.ARGB32, false);
            m_texture2DArray_B = new Texture2DArray(32, 32, 4, TextureFormat.ARGB32, false);
            AssetDatabase.CreateAsset(m_texture2DArray_A, m_pathTexture2DArray_A);
            AssetDatabase.CreateAsset(m_texture2DArray_B, m_pathTexture2DArray_B);
            m_texture2DArray_A = AssetDatabase.LoadAssetAtPath<Texture2DArray>(m_pathTexture2DArray_A);
            m_texture2DArray_B = AssetDatabase.LoadAssetAtPath<Texture2DArray>(m_pathTexture2DArray_B);

            m_pathTexture3D_A = VFXTestCommon.tempBasePath + "texture3D_A.asset";
            m_pathTexture3D_B = VFXTestCommon.tempBasePath + "texture3D_B.asset";
            m_texture3D_A = new Texture3D(16, 16, 16, TextureFormat.ARGB32, false);
            m_texture3D_B = new Texture3D(8, 8, 8, TextureFormat.ARGB32, false);
            AssetDatabase.CreateAsset(m_texture3D_A, m_pathTexture3D_A);
            AssetDatabase.CreateAsset(m_texture3D_B, m_pathTexture3D_B);
            m_texture3D_A = AssetDatabase.LoadAssetAtPath<Texture3D>(m_pathTexture3D_A);
            m_texture3D_B = AssetDatabase.LoadAssetAtPath<Texture3D>(m_pathTexture3D_B);

            m_pathTextureCube_A = VFXTestCommon.tempBasePath + "textureCube_A.asset";
            m_pathTextureCube_B = VFXTestCommon.tempBasePath + "textureCube_B.asset";
            m_textureCube_A = new Cubemap(16, TextureFormat.ARGB32, false);
            m_textureCube_B = new Cubemap(32, TextureFormat.ARGB32, false);
            AssetDatabase.CreateAsset(m_textureCube_A, m_pathTextureCube_A);
            AssetDatabase.CreateAsset(m_textureCube_B, m_pathTextureCube_B);
            m_textureCube_A = AssetDatabase.LoadAssetAtPath<Cubemap>(m_pathTextureCube_A);
            m_textureCube_B = AssetDatabase.LoadAssetAtPath<Cubemap>(m_pathTextureCube_B);

            m_pathTextureCubeArray_A = VFXTestCommon.tempBasePath + "textureCubeArray_A.asset";
            m_pathTextureCubeArray_B = VFXTestCommon.tempBasePath + "textureCubeArray_B.asset";
            m_textureCubeArray_A = new CubemapArray(16, 4, TextureFormat.ARGB32, false);
            m_textureCubeArray_B = new CubemapArray(32, 4, TextureFormat.ARGB32, false);
            AssetDatabase.CreateAsset(m_textureCubeArray_A, m_pathTextureCubeArray_A);
            AssetDatabase.CreateAsset(m_textureCubeArray_B, m_pathTextureCubeArray_B);
            m_textureCubeArray_A = AssetDatabase.LoadAssetAtPath<CubemapArray>(m_pathTextureCubeArray_A);
            m_textureCubeArray_B = AssetDatabase.LoadAssetAtPath<CubemapArray>(m_pathTextureCubeArray_B);
        }

        Scene m_SceneToUnload;
        GameObject m_mainObject;
        [SetUp]
        public void Setup()
        {
            m_SceneToUnload = SceneManager.CreateScene("EmptyVisualEffectTest_" + Guid.NewGuid());
            SceneManager.SetActiveScene(m_SceneToUnload);

            var mainObjectName = "VFX_Test_Main_Object";
            m_mainObject = new GameObject(mainObjectName);

            var mainCameraName = "VFX_Test_Main_Camera";
            var mainCamera = new GameObject(mainCameraName);
            var camera = mainCamera.AddComponent<Camera>();
            mainCamera.tag = "MainCamera";
            camera.transform.localPosition = Vector3.one;
            camera.transform.LookAt(m_mainObject.transform.position);
        }

        [TearDown]
        public void Teardown()
        {
            SceneManager.UnloadSceneAsync(m_SceneToUnload);
        }

        [OneTimeTearDown]
        public void CleanUp()
        {
            Debug.unityLogger.logEnabled = true;
            Time.captureFramerate = 0;
            VFXManager.fixedTimeStep = 1.0f / 60.0f;
            VFXManager.maxDeltaTime = 1.0f / 20.0f;

            UnityEngine.Object.DestroyImmediate(m_cubeEmpty);
            UnityEngine.Object.DestroyImmediate(m_sphereEmpty);
            AssetDatabase.DeleteAsset(m_pathTexture2D_A);
            AssetDatabase.DeleteAsset(m_pathTexture2D_B);
            AssetDatabase.DeleteAsset(m_pathTexture2DArray_A);
            AssetDatabase.DeleteAsset(m_pathTexture2DArray_B);
            AssetDatabase.DeleteAsset(m_pathTexture3D_A);
            AssetDatabase.DeleteAsset(m_pathTexture3D_B);
            AssetDatabase.DeleteAsset(m_pathTextureCube_A);
            AssetDatabase.DeleteAsset(m_pathTextureCube_B);
            AssetDatabase.DeleteAsset(m_pathTextureCubeArray_A);
            AssetDatabase.DeleteAsset(m_pathTextureCubeArray_B);

            VFXTestCommon.DeleteAllTemporaryGraph();
        }

        [UnityTest]
        public IEnumerator CreateComponent_And_Graph_Restart_Component_Expected()
        {
            var graph = VFXTestCommon.CreateGraph_And_System();

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfxComponent = m_mainObject.AddComponent<VisualEffect>();
            vfxComponent.visualEffectAsset = graph.visualEffectResource.asset;

            //Assert.DoesNotThrow(() => VFXTestCommon.GetSpawnerState(vfxComponent, 0)); //N.B. : This cannot be tested after EnterPlayMode due to the closure
            int maxFrame = 512;
            while (VFXTestCommon.GetSpawnerState(vfxComponent, 0).totalTime < 1.0f && maxFrame-- > 0)
                yield return null;

            Assert.GreaterOrEqual(VFXTestCommon.GetSpawnerState(vfxComponent, 0).totalTime, 1.0f);

            vfxComponent.enabled = false;
            vfxComponent.enabled = true;
            yield return null;

            maxFrame = 64;
            while (VFXTestCommon.GetSpawnerState(vfxComponent, 0).totalTime > 1.0f && maxFrame-- > 0)
                yield return null;

            Assert.Less(VFXTestCommon.GetSpawnerState(vfxComponent, 0).totalTime, 1.0f);
        }

        [UnityTest]
        public IEnumerator CreateComponent_And_VerifyRendererState()
        {
            var graph = VFXTestCommon.CreateGraph_And_System();

            //< Same Behavior as Drag & Drop
            GameObject currentObject = new GameObject("TemporaryGameObject_RenderState", /*typeof(Transform),*/ typeof(VisualEffect));
            var vfx = currentObject.GetComponent<VisualEffect>();
            var asset = graph.visualEffectResource.asset;
            Assert.IsNotNull(asset);

            vfx.visualEffectAsset = asset;

            int maxFrame = 512;
            while (vfx.culled && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null;

            Assert.IsNotNull(currentObject.GetComponent<VFXRenderer>());
            var actualShadowCastingMode = currentObject.GetComponent<VFXRenderer>().shadowCastingMode;
            Assert.AreEqual(actualShadowCastingMode, ShadowCastingMode.On);

        }

        [UnityTest]
        public IEnumerator CreateComponent_And_VerifyRenderBounds()
        {
            var graph = VFXTestCommon.CreateGraph_And_System();
            var initializeContext = graph.children.OfType<VFXBasicInitialize>().FirstOrDefault();

            var center = new Vector3(1.0f, 2.0f, 3.0f);
            var size = new Vector3(111.0f, 222.0f, 333.0f);

            initializeContext.inputSlots[0][0].value = center;
            initializeContext.inputSlots[0][1].value = size;
            graph.SetExpressionGraphDirty();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            //< Same Behavior as Drag & Drop
            GameObject currentObject = new GameObject("TemporaryGameObject_RenderBounds", /*typeof(Transform),*/ typeof(VisualEffect));
            var vfx = currentObject.GetComponent<VisualEffect>();
            var asset = graph.visualEffectResource.asset;
            Assert.IsNotNull(asset);

            vfx.visualEffectAsset = asset;

            int maxFrame = 512;
            while ((vfx.culled
                    || currentObject.GetComponent<VFXRenderer>().bounds.extents.x == 0.0f)
                   && --maxFrame > 0)
            {
                yield return null;
            }
            Assert.IsTrue(maxFrame > 0);
            yield return null;

            var vfxRenderer = currentObject.GetComponent<VFXRenderer>();
            var bounds = vfxRenderer.bounds;

            Assert.AreEqual(center.x, bounds.center.x, 10e-5);
            Assert.AreEqual(center.y, bounds.center.y, 10e-5);
            Assert.AreEqual(center.z, bounds.center.z, 10e-5);
            Assert.AreEqual(size.x / 2.0f, bounds.extents.x, 10e-5);
            Assert.AreEqual(size.y / 2.0f, bounds.extents.y, 10e-5);
            Assert.AreEqual(size.z / 2.0f, bounds.extents.z, 10e-5);
        }

        //Cover case : 1232862, RenderQuadIndirectCommand crashes
        [UnityTest]
        public IEnumerator CreateComponent_Disable_It_But_Enable_Renderer()
        {
            var graph = VFXTestCommon.CreateGraph_And_System();
            var initializeContext = graph.children.OfType<VFXBasicInitialize>().FirstOrDefault();

            //Really big bbox to be sure it is in view
            var center = new Vector3(0.0f, 0.0f, 0.0f);
            var size = new Vector3(100.0f, 100.0f, 100.0f);
            initializeContext.inputSlots[0][0].value = center;
            initializeContext.inputSlots[0][1].value = size;
            graph.SetExpressionGraphDirty();
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            var currentObject = new GameObject("CreateComponent_Disable_It_But_Enable_Renderer", typeof(VisualEffect));
            var vfx = currentObject.GetComponent<VisualEffect>();
            var asset = graph.visualEffectResource.asset;
            vfx.visualEffectAsset = asset;

            int maxFrame = 8;
            while (vfx.culled && --maxFrame > 0)
                yield return null;
            Assert.IsFalse(vfx.culled);
            vfx.enabled = false;
            //Assert.IsTrue(vfx.culled); //Culled state is not set to false on disabled anymore, but it will not be rendered anyway

            var renderer = currentObject.GetComponent<Renderer>();
            renderer.enabled = true;
            for (int i = 0; i < 4; ++i)
            {
                Assert.IsTrue(renderer.enabled);
                //Assert.IsTrue(vfx.culled); //Culled state is not set to false on disabled anymore, but it will not be rendered anyway
                yield return null;
            }

            //back to normal
            vfx.enabled = true;
            maxFrame = 8;
            while (vfx.culled && --maxFrame > 0)
                yield return null;
            Assert.IsFalse(vfx.culled);
        }

        [UnityTest]
        public IEnumerator CreateComponent_And_Check_NoneTexture_Constraint_Doesnt_Generate_Any_Error()
        {
            var graph = VFXTestCommon.CreateGraph_And_System();

            var burst = ScriptableObject.CreateInstance<VFXSpawnerBurst>();
            burst.inputSlots.First(o => o.name.ToLowerInvariant().Contains("count")).value = 147.0f;
            graph.children.OfType<VFXBasicSpawner>().First().AddChild(burst);

            var operatorSample3D = ScriptableObject.CreateInstance<Operator.SampleTexture3D>();
            operatorSample3D.inputSlots.First(o => o.valueType == VFXValueType.Texture3D).value = null;
            graph.AddChild(operatorSample3D);

            var initialize = graph.children.First(o => o is VFXBasicInitialize);
            bool r = operatorSample3D.outputSlots.First().Link(initialize.children.OfType<VFXBlock>().First().inputSlots.First());
            Assert.IsTrue(r);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            var currentObject = new GameObject("TemporaryGameObject_NoneTexture", typeof(VisualEffect));
            var vfx = currentObject.GetComponent<VisualEffect>();
            var asset = graph.visualEffectResource.asset;
            vfx.visualEffectAsset = asset;

            int maxFrame = 64;
            while (vfx.culled && --maxFrame > 0)
                yield return null;
            Assert.Greater(maxFrame, 0u, "Culling Test Failure");

            maxFrame = 64;
            while (vfx.aliveParticleCount == 0 && --maxFrame > 0)
                yield return null;
            Assert.Greater(maxFrame, 0u, "Alive Particle Count failure");

            //Wait for a few frame to be sure the rendering has been triggered
            for (int i = 0; i < 3; ++i)
                yield return null;
        }

        [UnityTest]
        public IEnumerator CreateComponent_And_CheckDimension_Constraint()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();

            var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var allType = ScriptableObject.CreateInstance<AllType>();

            contextInitialize.AddChild(allType);
            graph.AddChild(contextInitialize);

            // Needs a spawner and output for the system to be valid (TODOPAUL : Should not be needed here)
            {
                var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
                spawner.LinkTo(contextInitialize);
                graph.AddChild(spawner);

                var output = ScriptableObject.CreateInstance<VFXPointOutput>();
                output.LinkFrom(contextInitialize);
                graph.AddChild(output);
            }

            var parameter = VFXLibrary.GetParameters().First(o => o.modelType == typeof(Texture2D)).CreateInstance();
            var type = VFXValueType.Texture2D;

            var targetTextureName = "exposed_test_tex2D";

            if (type != VFXValueType.None)
            {
                parameter.SetSettingValue("m_ExposedName", targetTextureName);
                parameter.SetSettingValue("m_Exposed", true);
                graph.AddChild(parameter);
            }

            for (int i = 0; i < allType.GetNbInputSlots(); ++i)
            {
                var currentSlot = allType.GetInputSlot(i);
                var expression = currentSlot.GetExpression();
                if (expression != null && expression.valueType == type)
                {
                    currentSlot.Link(parameter.GetOutputSlot(0));
                    break;
                }
            }

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
            {
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            }
            var vfxComponent = m_mainObject.AddComponent<VisualEffect>();
            vfxComponent.visualEffectAsset = graph.visualEffectResource.asset;

            yield return null;

            Assert.IsTrue(vfxComponent.HasTexture(targetTextureName));
            Assert.AreEqual(TextureDimension.Tex2D, vfxComponent.GetTextureDimension(targetTextureName));

            var renderTartget3D = new RenderTexture(4, 4, 4, RenderTextureFormat.ARGB32);
            renderTartget3D.dimension = TextureDimension.Tex3D;

            vfxComponent.SetTexture(targetTextureName, renderTartget3D);
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("3D"));
            Assert.AreNotEqual(renderTartget3D, vfxComponent.GetTexture(targetTextureName));

            var renderTartget2D = new RenderTexture(4, 4, 4, RenderTextureFormat.ARGB32);
            renderTartget2D.dimension = TextureDimension.Tex2D;
            vfxComponent.SetTexture(targetTextureName, renderTartget2D);
            Assert.AreEqual(renderTartget2D, vfxComponent.GetTexture(targetTextureName));
            yield return null;

            /*
             * Actually, this error is only caught in debug mode, ignored in release for performance reason
            renderTartget2D.dimension = TextureDimension.Tex3D; //try to hack dimension
            Assert.AreEqual(renderTartget2D, vfxComponent.GetTexture(targetTextureName));
            yield return null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("3D"));
            */
        }

        [UnityTest]
        public IEnumerator CreateComponent_Switch_Asset_Keep_Override()
        {
            var graph_A = VFXTestCommon.MakeTemporaryGraph();
            var graph_B = VFXTestCommon.MakeTemporaryGraph();
            var parametersVector3Desc = VFXLibrary.GetParameters().Where(o => o.modelType == typeof(Vector3)).First();

            var commonExposedName = "vorfji";
            var parameter_A = parametersVector3Desc.CreateInstance();
            parameter_A.SetSettingValue("m_ExposedName", commonExposedName);
            parameter_A.SetSettingValue("m_Exposed", true);
            parameter_A.value = new Vector3(0, 0, 0);
            graph_A.AddChild(parameter_A);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph_A));

            var parameter_B = parametersVector3Desc.CreateInstance();
            parameter_B.SetSettingValue("m_ExposedName", commonExposedName);
            parameter_B.SetSettingValue("m_Exposed", true);
            parameter_B.value = new Vector3(0, 0, 0);
            graph_B.AddChild(parameter_B);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph_B));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph_A.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasVector3(commonExposedName));
            var expectedOverriden = new Vector3(1, 2, 3);
            vfx.SetVector3(commonExposedName, expectedOverriden);
            yield return null;

            var actualOverriden = vfx.GetVector3(commonExposedName);
            Assert.AreEqual(actualOverriden.x, expectedOverriden.x); Assert.AreEqual(actualOverriden.y, expectedOverriden.y); Assert.AreEqual(actualOverriden.z, expectedOverriden.z);

            vfx.visualEffectAsset = graph_B.visualEffectResource.asset;
            yield return null;

            actualOverriden = vfx.GetVector3(commonExposedName);
            Assert.AreEqual(actualOverriden.x, expectedOverriden.x); Assert.AreEqual(actualOverriden.y, expectedOverriden.y); Assert.AreEqual(actualOverriden.z, expectedOverriden.z);
        }

        [UnityTest, Description("Regression test for 1258022")]
        public IEnumerator CreateComponent_Change_ExposedType_Keeping_Same_Name()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            var parametersVector3Desc = VFXLibrary.GetParameters().Where(o => o.modelType == typeof(Vector3)).First();
            var parametersGradientDesc = VFXLibrary.GetParameters().Where(o => o.modelType == typeof(Gradient)).First();

            var commonExposedName = "azerty";
            var parameter_A = parametersVector3Desc.CreateInstance();
            parameter_A.SetSettingValue("m_ExposedName", commonExposedName);
            parameter_A.SetSettingValue("m_Exposed", true);
            parameter_A.value = new Vector3(0, 0, 0);
            graph.AddChild(parameter_A);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasVector3(commonExposedName));
            vfx.SetVector3(commonExposedName, Vector3.one * 8786.0f);

            yield return null;

            parameter_A.SetSettingValue("m_Exposed", false);
            parameter_A.SetSettingValue("m_ExposedName", commonExposedName + "old");

            var parameter_B = parametersGradientDesc.CreateInstance();
            parameter_B.SetSettingValue("m_ExposedName", commonExposedName);
            parameter_B.SetSettingValue("m_Exposed", true);
            graph.AddChild(parameter_B);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            yield return null;

            Assert.IsFalse(vfx.HasVector3(commonExposedName));
            Assert.IsTrue(vfx.HasGradient(commonExposedName));
            Assert.IsNotNull(vfx.GetGradient(commonExposedName));
        }

        [UnityTest, Description("Cover UUM-108512")]
        public IEnumerator CreateComponent_Change_Asset_Content_Keep_Override()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            var parameterUintDesc = VFXLibrary.GetParameters().First(o => o.modelType == typeof(uint));
            var parameterTextureDesc = VFXLibrary.GetParameters().First(o => o.modelType == typeof(Texture2D));

            var uintExposedName = "myExposeUInt";
            var parameterUint = parameterUintDesc.CreateInstance();
            parameterUint.SetSettingValue("m_ExposedName", uintExposedName);
            parameterUint.SetSettingValue("m_Exposed", true);
            parameterUint.value = 1u;

            var textureExposedName = "myExposeTexture";
            var parameterTexture = parameterTextureDesc.CreateInstance();
            parameterTexture.SetSettingValue("m_ExposedName", textureExposedName);
            parameterTexture.SetSettingValue("m_Exposed", true);
            parameterTexture.value = m_texture2D_A;

            graph.AddChild(parameterUint);
            graph.AddChild(parameterTexture);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            yield return null;

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());

            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasUInt(uintExposedName));
            Assert.AreEqual(1u, vfx.GetUInt(uintExposedName));
            Assert.IsTrue(vfx.HasTexture(textureExposedName));
            Assert.AreEqual(m_texture2D_A, vfx.GetTexture(textureExposedName));
            vfx.SetUInt(uintExposedName, 2u);
            Assert.AreEqual(2u, vfx.GetUInt(uintExposedName));
            vfx.SetTexture(textureExposedName, m_texture2D_B);
            Assert.AreEqual(m_texture2D_B, vfx.GetTexture(textureExposedName));

            yield return null;

            parameterUint.value = 3u;
            graph.SetExpressionValueDirty();
            Assert.IsFalse(graph.IsExpressionGraphDirty());
            
            graph.RecompileIfNeeded(); //Expecting an invocation of compiledData.UpdateValues();

            Assert.AreEqual(2u, vfx.GetUInt(uintExposedName));
            Assert.AreEqual(m_texture2D_B, vfx.GetTexture(textureExposedName));
        }

        [UnityTest, Description("Cover UUM-108512 (ResetOverride case)")]
        public IEnumerator CreateComponent_ResetOverride_Dont_Grow_Anything()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            var parameterTextureDesc = VFXLibrary.GetParameters().First(o => o.modelType == typeof(Texture2D));

            var textureExposedName = "myExposeTexture";
            var parameterTexture = parameterTextureDesc.CreateInstance();
            parameterTexture.SetSettingValue("m_ExposedName", textureExposedName);
            parameterTexture.SetSettingValue("m_Exposed", true);
            parameterTexture.value = m_texture2D_A;

            graph.AddChild(parameterTexture);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            yield return null;

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());

            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasTexture(textureExposedName));
            Assert.AreEqual(m_texture2D_A, vfx.GetTexture(textureExposedName));
            vfx.SetTexture(textureExposedName, m_texture2D_B);
            Assert.AreEqual(m_texture2D_B, vfx.GetTexture(textureExposedName));

            var initialMemory = Profiler.GetRuntimeMemorySizeLong(vfx);
            for (int step = 0; step < 8; step++)
            {
                vfx.ResetOverride(textureExposedName);

                Assert.AreEqual(m_texture2D_A, vfx.GetTexture(textureExposedName));
                vfx.SetTexture(textureExposedName, m_texture2D_B);
                Assert.AreEqual(m_texture2D_B, vfx.GetTexture(textureExposedName));

                yield return null;

                Assert.AreEqual(initialMemory, Profiler.GetRuntimeMemorySizeLong(vfx), "Unexpected growing allocated memory for this VFX, it might be something wrong internally.");
            }
        }

#pragma warning disable 0414
        private static bool[] trueOrFalse = { true, false };
#pragma warning restore 0414

        [UnityTest]
        public IEnumerator CreateComponent_Modify_Value_Doesnt_Reset([ValueSource("trueOrFalse")] bool modifyValue, [ValueSource("trueOrFalse")] bool modifyAssetValue)
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            var parametersVector2Desc = VFXLibrary.GetParameters().Where(o => o.modelType == typeof(Vector2)).First();

            Vector2 expectedValue = new Vector2(1.0f, 2.0f);

            var exposedName = "bvcxw";
            var parameter = parametersVector2Desc.CreateInstance();
            parameter.SetSettingValue("m_ExposedName", exposedName);
            parameter.SetSettingValue("m_Exposed", true);
            parameter.value = expectedValue;
            graph.AddChild(parameter);

            var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            graph.AddChild(contextInitialize);

            var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var constantRate = ScriptableObject.CreateInstance<VFXSpawnerConstantRate>();
            spawner.AddChild(constantRate);

            graph.AddChild(spawner);
            spawner.LinkTo(contextInitialize);

            var output = ScriptableObject.CreateInstance<VFXPointOutput>();
            graph.AddChild(output);
            output.LinkFrom(contextInitialize);

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasVector2(exposedName));
            if (modifyValue)
            {
                expectedValue = new Vector2(3.0f, 4.0f);
                vfx.SetVector2(exposedName, expectedValue);
            }
            Assert.AreEqual(expectedValue.x, vfx.GetVector2(exposedName).x); Assert.AreEqual(expectedValue.y, vfx.GetVector2(exposedName).y);

            float spawnerLimit = 1.8f; //Arbitrary enough large time
            int maxFrameCount = 1024;
            while (maxFrameCount-- > 0)
            {
                var spawnerState = VFXTestCommon.GetSpawnerState(vfx, 0u);
                if (spawnerState.totalTime > spawnerLimit)
                    break;
                yield return null;
            }
            Assert.IsTrue(maxFrameCount > 0);

            if (modifyAssetValue)
            {
                expectedValue = new Vector2(5.0f, 6.0f);
                parameter.value = expectedValue;
                graph.RecompileIfNeeded(false, true);
            }

            if (modifyValue)
            {
                var editor = Editor.CreateEditor(vfx);
                editor.serializedObject.Update();

                var propertySheet = editor.serializedObject.FindProperty("m_PropertySheet");
                var fieldName = VisualEffectSerializationUtility.GetTypeField(VFXExpression.TypeToType(VFXValueType.Float2)) + ".m_Array";
                var vfxField = propertySheet.FindPropertyRelative(fieldName);

                Assert.AreEqual(1, vfxField.arraySize);

                var property = vfxField.GetArrayElementAtIndex(0);
                property = property.FindPropertyRelative("m_Value");
                expectedValue = new Vector2(7.0f, 8.0f);
                property.vector2Value = expectedValue;
                editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                GameObject.DestroyImmediate(editor);
            }
            yield return null;

            var spawnerStateFinal = VFXTestCommon.GetSpawnerState(vfx, 0u);
            Assert.IsTrue(spawnerStateFinal.totalTime > spawnerLimit); //Check there isn't any reset time
            Assert.IsTrue(vfx.HasVector2(exposedName));
            Assert.AreEqual(expectedValue.x, vfx.GetVector2(exposedName).x); Assert.AreEqual(expectedValue.y, vfx.GetVector2(exposedName).y);

            //Last step, if trying to modify component value, verify reset override restore value in asset without reinit
            if (modifyValue)
            {
                var editor = Editor.CreateEditor(vfx);
                editor.serializedObject.Update();

                var propertySheet = editor.serializedObject.FindProperty("m_PropertySheet");
                var fieldName = VisualEffectSerializationUtility.GetTypeField(VFXExpression.TypeToType(VFXValueType.Float2)) + ".m_Array";
                var vfxField = propertySheet.FindPropertyRelative(fieldName);

                Assert.AreEqual(1, vfxField.arraySize);

                var property = vfxField.GetArrayElementAtIndex(0);
                property = property.FindPropertyRelative("m_Overridden");
                expectedValue = (Vector2)parameter.value;
                property.boolValue = false;
                editor.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                GameObject.DestroyImmediate(editor);

                yield return null;
                spawnerStateFinal = VFXTestCommon.GetSpawnerState(vfx, 0u);

                Assert.IsTrue(spawnerStateFinal.totalTime > spawnerLimit); //Check there isn't any reset time
                Assert.IsTrue(vfx.HasVector2(exposedName));
                Assert.AreEqual(expectedValue.x, vfx.GetVector2(exposedName).x); Assert.AreEqual(expectedValue.y, vfx.GetVector2(exposedName).y);
            }
        }

        [UnityTest]
        public IEnumerator CreateComponent_Modify_Asset_Keep_Override()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();

            var parametersVector3Desc = VFXLibrary.GetParameters().Where(o => o.modelType == typeof(Vector3)).First();

            var exposedName = "poiuyt";
            var parameter = parametersVector3Desc.CreateInstance();
            parameter.SetSettingValue("m_ExposedName", exposedName);
            parameter.SetSettingValue("m_Exposed", true);
            parameter.value = new Vector3(0, 0, 0);
            graph.AddChild(parameter);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            Assert.IsTrue(vfx.HasVector3(exposedName));
            var expectedOverriden = new Vector3(1, 2, 3);
            vfx.SetVector3(exposedName, expectedOverriden);

            yield return null;

            var actualOverriden = vfx.GetVector3(exposedName);
            Assert.AreEqual(actualOverriden.x, expectedOverriden.x); Assert.AreEqual(actualOverriden.y, expectedOverriden.y); Assert.AreEqual(actualOverriden.z, expectedOverriden.z);

            /* Add system & another exposed */
            var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var allType = ScriptableObject.CreateInstance<AllType>();

            contextInitialize.AddChild(allType);
            graph.AddChild(contextInitialize);

            var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            spawner.LinkTo(contextInitialize);
            graph.AddChild(spawner);

            var output = ScriptableObject.CreateInstance<VFXPointOutput>();
            output.LinkFrom(contextInitialize);
            graph.AddChild(output);

            var parameter_Other = parametersVector3Desc.CreateInstance();
            var exposedName_Other = "tyuiop";
            parameter_Other.SetSettingValue("m_ExposedName", exposedName_Other);
            parameter_Other.SetSettingValue("m_Exposed", true);
            parameter_Other.value = new Vector3(6, 6, 6);
            graph.AddChild(parameter_Other);
            parameter.value = new Vector3(5, 5, 5);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            yield return null;

            Assert.IsTrue(vfx.HasVector3(exposedName));
            Assert.IsTrue(vfx.HasVector3(exposedName_Other));
            actualOverriden = vfx.GetVector3(exposedName);

            Assert.AreEqual(actualOverriden.x, expectedOverriden.x);
            Assert.AreEqual(actualOverriden.y, expectedOverriden.y);
            Assert.AreEqual(actualOverriden.z, expectedOverriden.z);
        }

        [UnityTest, Description("Regression test UUM-6234")]
        public IEnumerator Delete_Mesh_While_Rendering()
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();
            string meshFilePath;
            {
                //Create Mesh Asset
                Mesh mesh;
                {
                    var resourceMesh = new Mesh()
                    {
                        vertices = new[]
                        {
                            new Vector3(0, 0, 0),
                            new Vector3(1, 1, 0),
                            new Vector3(1, 0, 0),
                        },
                        triangles = new[] {0, 1, 2}
                    };
                    var guid = System.Guid.NewGuid().ToString();
                    meshFilePath = string.Format(VFXTestCommon.tempBasePath + "Mesh_{0}.asset", guid);
                    AssetDatabase.CreateAsset(resourceMesh, meshFilePath);
                    AssetDatabase.SaveAssets();
                    mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshFilePath);
                }
                Assert.IsNotNull(mesh);

                //Create VFXAsset
                {
                    var staticMeshOutput = ScriptableObject.CreateInstance<VFXStaticMeshOutput>();
                    var slots = staticMeshOutput.inputSlots.Where(o => o.value is Mesh).ToArray();
                    Assert.IsTrue(slots.Any());
                    foreach (var slot in slots)
                    {
                        if (slot.value is Mesh)
                            slot.value = mesh;
                    }
                    graph.AddChild(staticMeshOutput);

                    var particleOutput = ScriptableObject.CreateInstance<VFXMeshOutput>();
                    particleOutput.SetSettingValue("castShadows", true);
                    slots = particleOutput.inputSlots.Where(o => o.value is Mesh).ToArray();
                    Assert.IsTrue(slots.Any());
                    foreach (var slot in slots)
                    {
                        if (slot.value is Mesh)
                            slot.value = mesh;
                    }
                    graph.AddChild(particleOutput);

                    var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
                    contextInitialize.LinkTo(particleOutput);
                    graph.AddChild(contextInitialize);

                    var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
                    spawner.LinkTo(contextInitialize);
                    graph.AddChild(spawner);

                    var burst = ScriptableObject.CreateInstance<VFXSpawnerBurst>();
                    spawner.AddChild(burst);
                    burst.inputSlots[0].value = 1.0f;
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));
                }
            }

            var cameraTransform = Camera.main.transform;
            cameraTransform.localPosition = Vector3.one;
            cameraTransform.LookAt(Vector3.zero);

            //Create object and wait to have visible particles
            GameObject currentObject = new GameObject("Delete_Mesh_While_Rendered_With_Output", /*typeof(Transform),*/ typeof(VisualEffect));
            var vfx = currentObject.GetComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            int maxFrame = 64;
            while (vfx.aliveParticleCount == 0 && maxFrame-- > 0)
                yield return null;
            Assert.IsTrue(maxFrame > 0);

            //Delete Mesh & Wait a few frame
            File.Delete(meshFilePath);
            File.Delete(meshFilePath + ".meta");
            for (int i = 0; i < 4; ++i)
                yield return null;
            AssetDatabase.Refresh();
            for (int i = 0; i < 4; ++i)
                yield return null;

            //Check content from VFX
            {
                var meshOutput = graph.children.OfType<VFXMeshOutput>().First();
                var meshSlot = meshOutput.inputSlots.First(o => o.property.type == typeof(Mesh));

                var mesh = meshSlot.value as UnityEngine.Object;
                Assert.IsTrue(mesh == null); //Mesh should be deleted at this point...
                Assert.IsFalse(ReferenceEquals(mesh, null)); //... but expected missing reference
            }
        }

        private object GetValue_A_Type(Type type)
        {
            if (typeof(float) == type)
                return 2.0f;
            else if (typeof(Vector2) == type)
                return new Vector2(3.0f, 4.0f);
            else if (typeof(Vector3) == type)
                return new Vector3(8.0f, 9.0f, 10.0f);
            else if (typeof(Vector4) == type)
                return new Vector4(11.0f, 12.0f, 13.0f, 14.0f);
            else if (typeof(Color) == type)
                return new Color(0.1f, 0.2f, 0.3f, 0.4f);
            else if (typeof(int) == type)
                return 15;
            else if (typeof(uint) == type)
                return 16u;
            else if (typeof(AnimationCurve) == type)
                return new AnimationCurve(new Keyframe(0, 13), new Keyframe(1, 14));
            else if (typeof(Gradient) == type)
                return new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey(Color.white, 0.2f) } };
            else if (typeof(Mesh) == type)
                return m_cubeEmpty.GetComponent<MeshFilter>().sharedMesh;
            else if (typeof(Texture2D) == type)
                return m_texture2D_A;
            else if (typeof(Texture2DArray) == type)
                return m_texture2DArray_A;
            else if (typeof(Texture3D) == type)
                return m_texture3D_A;
            else if (typeof(Cubemap) == type)
                return m_textureCube_A;
            else if (typeof(CubemapArray) == type)
                return m_textureCubeArray_A;
            else if (typeof(bool) == type)
                return true;
            else if (typeof(Matrix4x4) == type)
                return Matrix4x4.identity;
            return null;
        }

        private object GetValue_B_Type(Type type)
        {
            if (typeof(float) == type)
                return 50.0f;
            else if (typeof(Vector2) == type)
                return new Vector2(53.0f, 54.0f);
            else if (typeof(Vector3) == type)
                return new Vector3(58.0f, 59.0f, 510.0f);
            else if (typeof(Vector4) == type || typeof(Color) == type)// ValueB_Type is used to set a component value, so return a Vector4 with color values
                return new Vector4(511.0f, 512.0f, 513.0f, 514.0f);
            else if (typeof(int) == type)
                return 515;
            else if (typeof(uint) == type)
                return 516u;
            else if (typeof(AnimationCurve) == type)
                return new AnimationCurve(new Keyframe(0, 47), new Keyframe(0.5f, 23), new Keyframe(1.0f, 17));
            else if (typeof(Gradient) == type)
                return new Gradient() { colorKeys = new GradientColorKey[] { new GradientColorKey(Color.white, 0.2f), new GradientColorKey(Color.black, 0.6f) } };
            else if (typeof(Mesh) == type)
                return m_sphereEmpty.GetComponent<MeshFilter>().sharedMesh;
            else if (typeof(Texture2D) == type)
                return m_texture2D_B;
            else if (typeof(Texture2DArray) == type)
                return m_texture2DArray_B;
            else if (typeof(Texture3D) == type)
                return m_texture3D_B;
            else if (typeof(Cubemap) == type)
                return m_textureCube_B;
            else if (typeof(CubemapArray) == type)
                return m_textureCubeArray_B;
            else if (typeof(bool) == type)
                return true;
            else if (typeof(Matrix4x4) == type)
                return Matrix4x4.identity;
            return null;
        }

        bool fnHas_UsingBindings(VFXValueType type, VisualEffect vfx, string name)
        {
            switch (type)
            {
                case VFXValueType.Float: return vfx.HasFloat(name);
                case VFXValueType.Float2: return vfx.HasVector2(name);
                case VFXValueType.Float3: return vfx.HasVector3(name);
                case VFXValueType.Float4: return vfx.HasVector4(name);
                case VFXValueType.Int32: return vfx.HasInt(name);
                case VFXValueType.Uint32: return vfx.HasUInt(name);
                case VFXValueType.Curve: return vfx.HasAnimationCurve(name);
                case VFXValueType.ColorGradient: return vfx.HasGradient(name);
                case VFXValueType.Mesh: return vfx.HasMesh(name);
                case VFXValueType.Texture2D: return vfx.HasTexture(name) && vfx.GetTextureDimension(name) == TextureDimension.Tex2D;
                case VFXValueType.Texture2DArray: return vfx.HasTexture(name) && vfx.GetTextureDimension(name) == TextureDimension.Tex2DArray;
                case VFXValueType.Texture3D: return vfx.HasTexture(name) && vfx.GetTextureDimension(name) == TextureDimension.Tex3D;
                case VFXValueType.TextureCube: return vfx.HasTexture(name) && vfx.GetTextureDimension(name) == TextureDimension.Cube;
                case VFXValueType.TextureCubeArray: return vfx.HasTexture(name) && vfx.GetTextureDimension(name) == TextureDimension.CubeArray;
                case VFXValueType.CameraBuffer: return vfx.HasTexture(name);
                case VFXValueType.Boolean: return vfx.HasBool(name);
                case VFXValueType.Matrix4x4: return vfx.HasMatrix4x4(name);
            }
            return false;
        }

        object fnGet_UsingBindings(VFXValueType type, VisualEffect vfx, string name)
        {
            switch (type)
            {
                case VFXValueType.Float: return vfx.GetFloat(name);
                case VFXValueType.Float2: return vfx.GetVector2(name);
                case VFXValueType.Float3: return vfx.GetVector3(name);
                case VFXValueType.Float4: return vfx.GetVector4(name);
                case VFXValueType.Int32: return vfx.GetInt(name);
                case VFXValueType.Uint32: return vfx.GetUInt(name);
                case VFXValueType.Curve: return vfx.GetAnimationCurve(name);
                case VFXValueType.ColorGradient: return vfx.GetGradient(name);
                case VFXValueType.Mesh: return vfx.GetMesh(name);
                case VFXValueType.Texture2D:
                case VFXValueType.Texture2DArray:
                case VFXValueType.Texture3D:
                case VFXValueType.TextureCube:
                case VFXValueType.TextureCubeArray: return vfx.GetTexture(name);
                case VFXValueType.CameraBuffer: return vfx.GetTexture(name);
                case VFXValueType.Boolean: return vfx.GetBool(name);
                case VFXValueType.Matrix4x4: return vfx.GetMatrix4x4(name);
            }
            return null;
        }

        void fnSet_UsingBindings(VFXValueType type, VisualEffect vfx, string name, object value)
        {
            switch (type)
            {
                case VFXValueType.Float: vfx.SetFloat(name, (float)value); break;
                case VFXValueType.Float2: vfx.SetVector2(name, (Vector2)value); break;
                case VFXValueType.Float3: vfx.SetVector3(name, (Vector3)value); break;
                case VFXValueType.Float4: vfx.SetVector4(name, (Vector4)value); break;
                case VFXValueType.Int32: vfx.SetInt(name, (int)value); break;
                case VFXValueType.Uint32: vfx.SetUInt(name, (uint)value); break;
                case VFXValueType.Curve: vfx.SetAnimationCurve(name, (AnimationCurve)value); break;
                case VFXValueType.ColorGradient: vfx.SetGradient(name, (Gradient)value); break;
                case VFXValueType.Mesh: vfx.SetMesh(name, (Mesh)value); break;
                case VFXValueType.Texture2D:
                case VFXValueType.Texture2DArray:
                case VFXValueType.Texture3D:
                case VFXValueType.TextureCube:
                case VFXValueType.TextureCubeArray: vfx.SetTexture(name, (Texture)value); break;
                case VFXValueType.CameraBuffer: vfx.SetTexture(name, (Texture)value); break;
                case VFXValueType.Boolean: vfx.SetBool(name, (bool)value); break;
                case VFXValueType.Matrix4x4: vfx.SetMatrix4x4(name, (Matrix4x4)value); break;
            }
        }

        bool fnHas_UsingSerializedProperty(VFXValueType type, VisualEffect vfx, string name)
        {
            var editor = Editor.CreateEditor(vfx);
            try
            {
                var propertySheet = editor.serializedObject.FindProperty("m_PropertySheet");
                var fieldName = VisualEffectSerializationUtility.GetTypeField(VFXExpression.TypeToType(type)) + ".m_Array";
                var vfxField = propertySheet.FindPropertyRelative(fieldName);
                if (vfxField != null)
                {
                    for (int i = 0; i < vfxField.arraySize; ++i)
                    {
                        var property = vfxField.GetArrayElementAtIndex(i);
                        var nameProperty = property.FindPropertyRelative("m_Name").stringValue;
                        if (nameProperty == name)
                        {
                            return true;
                        }
                    }
                }
            }
            finally
            {
                GameObject.DestroyImmediate(editor);
            }
            return false;
        }

        Matrix4x4 fnMatrixFromSerializedProperty(SerializedProperty property)
        {
            var mat = new Matrix4x4();

            mat.m00 = property.FindPropertyRelative("e00").floatValue;
            mat.m01 = property.FindPropertyRelative("e01").floatValue;
            mat.m02 = property.FindPropertyRelative("e02").floatValue;
            mat.m03 = property.FindPropertyRelative("e03").floatValue;

            mat.m10 = property.FindPropertyRelative("e10").floatValue;
            mat.m11 = property.FindPropertyRelative("e11").floatValue;
            mat.m12 = property.FindPropertyRelative("e12").floatValue;
            mat.m13 = property.FindPropertyRelative("e13").floatValue;

            mat.m20 = property.FindPropertyRelative("e20").floatValue;
            mat.m21 = property.FindPropertyRelative("e21").floatValue;
            mat.m22 = property.FindPropertyRelative("e22").floatValue;
            mat.m23 = property.FindPropertyRelative("e23").floatValue;

            mat.m30 = property.FindPropertyRelative("e30").floatValue;
            mat.m31 = property.FindPropertyRelative("e31").floatValue;
            mat.m32 = property.FindPropertyRelative("e32").floatValue;
            mat.m33 = property.FindPropertyRelative("e33").floatValue;

            return mat;
        }

        void fnMatrixToSerializedProperty(SerializedProperty property, Matrix4x4 mat)
        {
            property.FindPropertyRelative("e00").floatValue = mat.m00;
            property.FindPropertyRelative("e01").floatValue = mat.m01;
            property.FindPropertyRelative("e02").floatValue = mat.m02;
            property.FindPropertyRelative("e03").floatValue = mat.m03;

            property.FindPropertyRelative("e10").floatValue = mat.m10;
            property.FindPropertyRelative("e11").floatValue = mat.m11;
            property.FindPropertyRelative("e12").floatValue = mat.m12;
            property.FindPropertyRelative("e13").floatValue = mat.m13;

            property.FindPropertyRelative("e20").floatValue = mat.m20;
            property.FindPropertyRelative("e21").floatValue = mat.m21;
            property.FindPropertyRelative("e22").floatValue = mat.m22;
            property.FindPropertyRelative("e23").floatValue = mat.m23;

            property.FindPropertyRelative("e30").floatValue = mat.m30;
            property.FindPropertyRelative("e31").floatValue = mat.m31;
            property.FindPropertyRelative("e32").floatValue = mat.m32;
            property.FindPropertyRelative("e33").floatValue = mat.m33;
        }

        object fnGet_UsingSerializedProperty(VFXValueType type, VisualEffect vfx, string name)
        {
            var editor = Editor.CreateEditor(vfx);
            try
            {
                var propertySheet = editor.serializedObject.FindProperty("m_PropertySheet");
                editor.serializedObject.Update();

                var fieldName = VisualEffectSerializationUtility.GetTypeField(VFXExpression.TypeToType(type)) + ".m_Array";
                var vfxField = propertySheet.FindPropertyRelative(fieldName);
                if (vfxField != null)
                {
                    for (int i = 0; i < vfxField.arraySize; ++i)
                    {
                        var property = vfxField.GetArrayElementAtIndex(i);
                        var nameProperty = property.FindPropertyRelative("m_Name").stringValue;
                        if (nameProperty == name)
                        {
                            property = property.FindPropertyRelative("m_Value");

                            switch (type)
                            {
                                case VFXValueType.Float: return property.floatValue;
                                case VFXValueType.Float2: return property.vector2Value;
                                case VFXValueType.Float3: return property.vector3Value;
                                case VFXValueType.Float4: return property.vector4Value;
                                case VFXValueType.Int32: return property.intValue;
                                case VFXValueType.Uint32: return property.intValue;     // there isn't uintValue
                                case VFXValueType.Curve: return property.animationCurveValue;
                                case VFXValueType.ColorGradient: return property.gradientValue;
                                case VFXValueType.Mesh: return property.objectReferenceValue;
                                case VFXValueType.Texture2D:
                                case VFXValueType.Texture2DArray:
                                case VFXValueType.Texture3D:
                                case VFXValueType.TextureCube:
                                case VFXValueType.TextureCubeArray: return property.objectReferenceValue;
                                case VFXValueType.CameraBuffer: return property.objectReferenceValue;
                                case VFXValueType.Boolean: return property.boolValue;
                                case VFXValueType.Matrix4x4: return fnMatrixFromSerializedProperty(property);
                            }
                            Assert.Fail();
                        }
                    }
                }
            }
            finally
            {
                GameObject.DestroyImmediate(editor);
            }
            return null;
        }

        void fnSet_UsingSerializedProperty(VFXValueType type, VisualEffect vfx, string name, object value)
        {
            var editor = Editor.CreateEditor(vfx);
            try
            {
                editor.serializedObject.Update();

                var propertySheet = editor.serializedObject.FindProperty("m_PropertySheet");
                var fieldName = VisualEffectSerializationUtility.GetTypeField(VFXExpression.TypeToType(type)) + ".m_Array";
                var vfxField = propertySheet.FindPropertyRelative(fieldName);
                if (vfxField != null)
                {
                    for (int i = 0; i < vfxField.arraySize; ++i)
                    {
                        var property = vfxField.GetArrayElementAtIndex(i);
                        var propertyName = property.FindPropertyRelative("m_Name").stringValue;
                        if (propertyName == name)
                        {
                            var propertyValue = property.FindPropertyRelative("m_Value");
                            var propertyOverriden = property.FindPropertyRelative("m_Overridden");

                            switch (type)
                            {
                                case VFXValueType.Float: propertyValue.floatValue = (float)value; break;
                                case VFXValueType.Float2: propertyValue.vector2Value = (Vector2)value; break;
                                case VFXValueType.Float3: propertyValue.vector3Value = (Vector3)value; break;
                                case VFXValueType.Float4: propertyValue.vector4Value = (Vector4)value; break;
                                case VFXValueType.Int32: propertyValue.intValue = (int)value; break;
                                case VFXValueType.Uint32: propertyValue.intValue = (int)((uint)value); break;     // there isn't uintValue
                                case VFXValueType.Curve: propertyValue.animationCurveValue = (AnimationCurve)value; break;
                                case VFXValueType.ColorGradient: propertyValue.gradientValue = (Gradient)value; break;
                                case VFXValueType.Mesh: propertyValue.objectReferenceValue = (UnityEngine.Object)value; break;
                                case VFXValueType.Texture2D:
                                case VFXValueType.Texture2DArray:
                                case VFXValueType.Texture3D:
                                case VFXValueType.TextureCube:
                                case VFXValueType.TextureCubeArray: propertyValue.objectReferenceValue = (UnityEngine.Object)value; break;
                                case VFXValueType.CameraBuffer: propertyValue.objectReferenceValue = (UnityEngine.Object)value; break;
                                case VFXValueType.Boolean: propertyValue.boolValue = (bool)value; break;
                                case VFXValueType.Matrix4x4: fnMatrixToSerializedProperty(propertyValue, (Matrix4x4)value); break;
                            }
                            propertyOverriden.boolValue = true;
                        }
                    }
                }
                editor.serializedObject.ApplyModifiedProperties();
            }
            finally
            {
                GameObject.DestroyImmediate(editor);
            }
        }

        [UnityTest]
        public IEnumerator CreateComponentWithAllBasicTypeExposed_Check_Animation_Curve()
        {
            var commonBaseName = "animation_expected_";
            var graph = VFXTestCommon.MakeTemporaryGraph();
            foreach (var parameter in VFXLibrary.GetParameters())
            {
                var newInstance = parameter.CreateInstance();
                var type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(newInstance.type) == e);
                if (type != VFXValueType.None)
                {
                    newInstance.SetSettingValue("m_ExposedName", commonBaseName + newInstance.type.UserFriendlyName().ToLowerInvariant());
                    newInstance.SetSettingValue("m_Exposed", true);
                    var value = GetValue_A_Type(newInstance.type);
                    Assert.IsNotNull(value);
                    newInstance.value = value;
                    graph.AddChild(newInstance);
                }
            }
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));
            while (m_mainObject.GetComponent<VisualEffect>() != null)
            {
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            }
            var vfxComponent = m_mainObject.AddComponent<VisualEffect>();
            vfxComponent.visualEffectAsset = graph.visualEffectResource.asset;
            yield return null;

            var editorCurveUtility = AnimationUtility.GetAnimatableBindings(vfxComponent.gameObject, vfxComponent.gameObject);
            editorCurveUtility = editorCurveUtility.Where(o => o.type == typeof(VisualEffect) || o.type == typeof(VFXRenderer)).ToArray();
            var allCurveName = editorCurveUtility
                .Select(o => $"{o.type.Name}:{o.propertyName}")
                .OrderBy(o => o)
                .ToArray();

            var dump = new StringBuilder("\n");
            foreach (var curve in allCurveName)
            {
                dump.Append(curve + "\n");
            }
            
            var expected = @"
VFXRenderer:m_Enabled
VFXRenderer:m_MeshLodSelectionBias
VFXRenderer:m_ReceiveShadows
VFXRenderer:m_RendererPriority
VFXRenderer:m_SortingOrder
VisualEffect:bool.animation_expected_bool
VisualEffect:float.animation_expected_float
VisualEffect:int.animation_expected_int
VisualEffect:m_Enabled
VisualEffect:Object.animation_expected_cubemap
VisualEffect:Object.animation_expected_cubemaparray
VisualEffect:Object.animation_expected_mesh
VisualEffect:Object.animation_expected_texture2d
VisualEffect:Object.animation_expected_texture2darray
VisualEffect:Object.animation_expected_texture3d
VisualEffect:uint.animation_expected_uint
VisualEffect:Vector2.animation_expected_vector2.x
VisualEffect:Vector2.animation_expected_vector2.y
VisualEffect:Vector3.animation_expected_vector3.x
VisualEffect:Vector3.animation_expected_vector3.y
VisualEffect:Vector3.animation_expected_vector3.z
VisualEffect:Vector4.animation_expected_color.w
VisualEffect:Vector4.animation_expected_color.x
VisualEffect:Vector4.animation_expected_color.y
VisualEffect:Vector4.animation_expected_color.z
VisualEffect:Vector4.animation_expected_vector4.w
VisualEffect:Vector4.animation_expected_vector4.x
VisualEffect:Vector4.animation_expected_vector4.y
VisualEffect:Vector4.animation_expected_vector4.z
";

            var dumpActual = dump.ToString();
            Assert.AreEqual(expected, dumpActual, "Unexpected Curve Listing:" + dumpActual);
            yield return null;
        }

        public static string[] kModifyPropertyAndInsureSerializedUpdatedCases = new[] { "Float", "Int32", "Float3", "Curve", "ColorGradient", "Mesh", "Texture2D", "Texture3D" };

        [UnityTest, Description("Cover UUM-96024")]
        public IEnumerator ModifySinglePropertyAndInsureSerializedUpdated([ValueSource(nameof(kModifyPropertyAndInsureSerializedUpdatedCases))] string typeCase)
        {
            var graph = VFXTestCommon.MakeTemporaryGraph();

            bool success = Enum.TryParse(typeof(VFXValueType), typeCase, false, out var r);
            Assert.IsTrue(success);
            Assert.AreNotEqual(VFXValueType.None, r);
            var valueType = (VFXValueType)r;

            var exposedPropertyName = "wxcv_" + typeCase;
            var desc = VFXLibrary.GetParameters().FirstOrDefault(o => VFXExpression.GetVFXValueTypeFromType(o.model.type) == valueType);
            Assert.IsNotNull(desc);

            var currentParameterType = desc.model.type;
            var currentParameter = desc.CreateInstance();
            currentParameter.SetSettingValue("m_ExposedName", exposedPropertyName);
            currentParameter.SetSettingValue("m_Exposed", true);
            var value = GetValue_A_Type(currentParameter.type);
            Assert.IsNotNull(value);
            currentParameter.value = value;
            graph.AddChild(currentParameter);

            Assert.IsNotNull(currentParameterType);
            Assert.IsTrue(graph.children.OfType<VFXParameter>().First() == currentParameter);

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));
            yield return null;

            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;

            Assert.IsTrue(fnHas_UsingBindings(valueType, vfx, exposedPropertyName));

            var baseValue = GetValue_A_Type(currentParameterType);
            var newValue = GetValue_B_Type(currentParameterType);
            Assert.AreNotEqual(baseValue, newValue);

            var readValue = fnGet_UsingSerializedProperty(valueType, vfx, exposedPropertyName);
            Assert.AreEqual(readValue, null);

            readValue = fnGet_UsingBindings(valueType, vfx, exposedPropertyName);
            Assert.AreEqual(readValue, baseValue);

            //Change the single value through bindings
            fnSet_UsingBindings((VFXValueType)r, vfx, exposedPropertyName, newValue);

            readValue = fnGet_UsingSerializedProperty(valueType, vfx, exposedPropertyName);
            Assert.AreEqual(readValue, newValue);

            readValue = fnGet_UsingBindings(valueType, vfx, exposedPropertyName);
            Assert.AreEqual(readValue, newValue);
        }

        [UnityTest]
        public IEnumerator CreateComponentWithAllBasicTypeExposed([ValueSource("trueOrFalse")] bool linkMode, [ValueSource("trueOrFalse")] bool bindingModes)
        {
            var commonBaseName = "abcd_";

            var graph = VFXTestCommon.MakeTemporaryGraph();

            var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            var allType = ScriptableObject.CreateInstance<AllType>();

            contextInitialize.AddChild(allType);
            graph.AddChild(contextInitialize);

            // Needs a spawner and output for the system to be valid
            {
                var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
                spawner.LinkTo(contextInitialize);
                graph.AddChild(spawner);

                var output = ScriptableObject.CreateInstance<VFXPointOutput>();
                output.LinkFrom(contextInitialize);
                graph.AddChild(output);
            }

            foreach (var parameter in VFXLibrary.GetParameters())
            {
                var newInstance = parameter.CreateInstance();

                VFXValueType type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(newInstance.type) == e);
                if (type != VFXValueType.None)
                {
                    newInstance.SetSettingValue("m_ExposedName", commonBaseName + newInstance.type.UserFriendlyName());
                    newInstance.SetSettingValue("m_Exposed", true);
                    var value = GetValue_A_Type(newInstance.type);
                    Assert.IsNotNull(value);
                    newInstance.value = value;
                    graph.AddChild(newInstance);
                }
            }

            if (linkMode)
            {
                foreach (var type in VFXTestCommon.s_supportedValueType)
                {
                    VFXSlot slot = null;
                    for (int i = 0; i < allType.GetNbInputSlots(); ++i)
                    {
                        var currentSlot = allType.GetInputSlot(i);
                        var expression = currentSlot.GetExpression();
                        if (expression != null && expression.valueType == type)
                        {
                            slot = currentSlot;
                            break;
                        }
                    }
                    Assert.IsNotNull(slot, type.ToString());

                    var parameter = graph.children.OfType<VFXParameter>().FirstOrDefault(o =>
                    {
                        if (o.GetNbOutputSlots() > 0)
                        {
                            var expression = o.outputSlots[0].GetExpression();
                            if (expression != null && expression.valueType == type)
                            {
                                return true;
                            }
                        }
                        return false;
                    });
                    Assert.IsNotNull(parameter, "parameter with type : " + type.ToString());
                    slot.Link(parameter.GetOutputSlot(0));
                }
            }

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            while (m_mainObject.GetComponent<VisualEffect>() != null)
            {
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());
            }
            var vfxComponent = m_mainObject.AddComponent<VisualEffect>();
            vfxComponent.visualEffectAsset = graph.visualEffectResource.asset;

            yield return null;

            Func<AnimationCurve, AnimationCurve, bool> fnCompareCurve = delegate (AnimationCurve left, AnimationCurve right)
            {
                return left.keys.Length == right.keys.Length;
            };

            Func<Gradient, Gradient, bool> fnCompareGradient = delegate (Gradient left, Gradient right)
            {
                return left.colorKeys.Length == right.colorKeys.Length;
            };

            //Check default Value_A & change to Value_B (At this stage, it's useless to access with SerializedProperty)
            foreach (var parameter in VFXLibrary.GetParameters())
            {
                VFXValueType type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(parameter.modelType) == e);
                if (type == VFXValueType.None)
                    continue;
                var currentName = commonBaseName + parameter.modelType.UserFriendlyName();
                var baseValue = GetValue_A_Type(parameter.modelType);
                var newValue = GetValue_B_Type(parameter.modelType);

                Assert.IsTrue(fnHas_UsingBindings(type, vfxComponent, currentName));
                var currentValue = fnGet_UsingBindings(type, vfxComponent, currentName);
                if (type == VFXValueType.ColorGradient)
                {
                    Assert.IsTrue(fnCompareGradient((Gradient)baseValue, (Gradient)currentValue));
                }
                else if (type == VFXValueType.Curve)
                {
                    Assert.IsTrue(fnCompareCurve((AnimationCurve)baseValue, (AnimationCurve)currentValue));
                }
                else if (parameter.modelType == typeof(Color))
                {
                    Color col = (Color)baseValue;
                    Assert.AreEqual(new Vector4(col.r, col.g, col.b, col.a), currentValue);
                }
                else
                {
                    Assert.AreEqual(baseValue, currentValue);
                }
                fnSet_UsingBindings(type, vfxComponent, currentName, newValue);

                yield return null;
            }

            //Compare new setted values
            foreach (var parameter in VFXLibrary.GetParameters())
            {
                VFXValueType type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(parameter.modelType) == e);
                if (type == VFXValueType.None)
                    continue;
                var currentName = commonBaseName + parameter.modelType.UserFriendlyName();
                var baseValue = GetValue_B_Type(parameter.modelType);
                if (bindingModes)
                    Assert.IsTrue(fnHas_UsingBindings(type, vfxComponent, currentName));
                else
                    Assert.IsTrue(fnHas_UsingSerializedProperty(type, vfxComponent, currentName));

                object currentValue = null;
                if (bindingModes)
                    currentValue = fnGet_UsingBindings(type, vfxComponent, currentName);
                else
                    currentValue = fnGet_UsingSerializedProperty(type, vfxComponent, currentName);

                //current = fnGet(type, vfxComponent, currentName);
                if (type == VFXValueType.ColorGradient)
                {
                    Assert.IsTrue(fnCompareGradient((Gradient)baseValue, (Gradient)currentValue));
                }
                else if (type == VFXValueType.Curve)
                {
                    Assert.IsTrue(fnCompareCurve((AnimationCurve)baseValue, (AnimationCurve)currentValue));
                }
                else
                {
                    Assert.AreEqual(baseValue, currentValue);
                }
                yield return null;
            }

            //Test ResetOverride function
            foreach (var parameter in VFXLibrary.GetParameters())
            {
                VFXValueType type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(parameter.modelType) == e);
                if (type == VFXValueType.None)
                    continue;
                var currentName = commonBaseName + parameter.modelType.UserFriendlyName();
                vfxComponent.ResetOverride(currentName);

                var baseValue = GetValue_A_Type(parameter.modelType);
                object currentValue = null;
                if (bindingModes)
                    currentValue = fnGet_UsingBindings(type, vfxComponent, currentName);
                else
                    currentValue = fnGet_UsingSerializedProperty(type, vfxComponent, currentName);
                if (type == VFXValueType.ColorGradient)
                {
                    Assert.IsTrue(fnCompareGradient((Gradient)baseValue, (Gradient)currentValue));
                }
                else if (type == VFXValueType.Curve)
                {
                    Assert.IsTrue(fnCompareCurve((AnimationCurve)baseValue, (AnimationCurve)currentValue));
                }
                else if (parameter.modelType == typeof(Color))
                {
                    Color col = (Color)baseValue;
                    Assert.AreEqual(new Vector4(col.r, col.g, col.b, col.a), currentValue);
                }
                else
                {
                    Assert.AreEqual(baseValue, currentValue);
                }

                if (!bindingModes)
                {
                    var internalValue = fnGet_UsingBindings(type, vfxComponent, currentName);
                    var originalAssetValue = GetValue_A_Type(parameter.modelType);

                    if (type == VFXValueType.ColorGradient)
                    {
                        Assert.IsTrue(fnCompareGradient((Gradient)originalAssetValue, (Gradient)internalValue));
                    }
                    else if (type == VFXValueType.Curve)
                    {
                        Assert.IsTrue(fnCompareCurve((AnimationCurve)originalAssetValue, (AnimationCurve)internalValue));
                    }
                    else if (parameter.modelType == typeof(Color))
                    {
                        Color col = (Color)originalAssetValue;
                        Assert.AreEqual(new Vector4(col.r, col.g, col.b, col.a), internalValue);
                    }
                    else
                    {
                        Assert.AreEqual(originalAssetValue, internalValue);
                    }
                }
                yield return null;
            }
        }

        static uint s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount = 0u;
        static void VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCountFn(VFXOutputEventArgs evt)
        {
            s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount++;
        }

        [UnityTest, Description("Regression test UUM-6379")]
        public IEnumerator VisualEffect_Spawned_Behind_Camera_Doesnt_Update()
        {
            VFXTestCommon.CloseAllUnecessaryWindows();
            while (EditorWindow.HasOpenInstances<SceneView>())
                EditorWindow.GetWindow<SceneView>().Close();

            EditorApplication.ExecuteMenuItem("Window/General/Game");

            var graph = VFXTestCommon.MakeTemporaryGraph();
            var mainCamera = Camera.main;

            var contextInitialize = ScriptableObject.CreateInstance<VFXBasicInitialize>();
            contextInitialize.GetData().SetSettingValue(nameof(VFXDataParticle.boundsMode), BoundsSettingMode.Manual);
            var bounds = contextInitialize.inputSlots.FirstOrDefault(o => o.name.ToLowerInvariant() == nameof(VFXBasicInitialize.InputPropertiesBounds.bounds));
            Assert.IsNotNull(bounds);
            bounds.value = new AABox() { center = Vector3.zero, size = Vector3.one };
            graph.AddChild(contextInitialize);

            var spawner = ScriptableObject.CreateInstance<VFXBasicSpawner>();
            var setSpawnCount = ScriptableObject.CreateInstance<Block.VFXSpawnerSetAttribute>();
            setSpawnCount.SetSettingValue("attribute", VFXAttribute.SpawnCount.name);
            setSpawnCount.inputSlots[0].value = 1.0f;
            spawner.AddChild(setSpawnCount);
            spawner.LinkTo(contextInitialize);
            graph.AddChild(spawner);

            var output = ScriptableObject.CreateInstance<VFXPointOutput>();
            output.LinkFrom(contextInitialize);
            graph.AddChild(output);

            var outputEvent = ScriptableObject.CreateInstance<VFX.VFXOutputEvent>();
            outputEvent.LinkFrom(spawner);
            graph.AddChild(outputEvent);

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));

            var visualEffectObject = new GameObject("VFX_Behind_Camera");
            visualEffectObject.transform.position = 10.0f * mainCamera.transform.position;

            var vfx = visualEffectObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount = 0u;
            vfx.outputEventReceived += VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCountFn;

            //Really first update, the culled status is true for one frame when we don't know
            //See https://unity.slack.com/archives/G1BTWN88Z/p1655996888047749?thread_ts=1655796328.440779&cid=G1BTWN88Z
            yield return null;
            Assert.AreEqual(1u, s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount);
            Assert.IsFalse(vfx.culled);
            s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount = 0u;

            //Wait for a few frames
            for (int i = 0; i < 16; i++)
                yield return null;

            Assert.AreEqual(0u, s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount);
            Assert.IsTrue(vfx.culled);

            s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount = 0u;
            visualEffectObject.transform.position = Vector3.zero;
            //Wait for a few frames, after back in frustum
            for (int i = 0; i < 8; i++)
                yield return null;

            Assert.IsTrue(s_VisualEffect_Spawned_Behind_Camera_Doesnt_Update_EventCount > 0u);
            Assert.IsFalse(vfx.culled);
        }

        private static Vector4 s_Constant_Curve_And_Gradient_Readback;
        static void Constant_Curve_And_Gradient_Readback(AsyncGPUReadbackRequest request)
        {
            if (request.hasError)
                Debug.LogError("Constant_Curve_And_Gradient_Readback failure.");

            var data = request.GetData<Vector4>();
            s_Constant_Curve_And_Gradient_Readback = data[0];
        }

        static int GetVisualEffectVisibleCount(VisualEffectAsset reference)
        {
            int visibleCount = 0;
            foreach (var vfx in VFXManager.GetComponents())
            {
                if (vfx.visualEffectAsset != reference)
                    continue;

                visibleCount += vfx.culled ? 0 : 1;
            }
            return visibleCount;
        }

        [UnityTest, Description("Regression test UUM-52510")]
        public IEnumerator VisualEffectAsset_Authoring_Constant_Curve_And_Gradient()
        {
            VFXTestCommon.CloseAllUnecessaryWindows();
            while (EditorWindow.HasOpenInstances<SceneView>())
                EditorWindow.GetWindow<SceneView>().Close();
            EditorApplication.ExecuteMenuItem("Window/General/Game");

            var structuredBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.None, 1, 16);
            Shader.SetGlobalBuffer("global_debug_buffer", structuredBuffer);

            var mainCamera = Camera.main;
            mainCamera.transform.position = Vector3.zero;
            mainCamera.transform.eulerAngles = Vector3.zero;

            var graph = VFXTestCommon.CopyTemporaryGraph("Packages/com.unity.testing.visualeffectgraph/Scenes/Repro_ConstantCurveAndGradient.vfx");

            var initialAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(AssetDatabase.GetAssetPath(graph));
            var window = VFXViewWindow.GetWindow<VFXViewWindow>();
            window.LoadAsset(initialAsset, null);
            for (int i = 0; i < 4; ++i)
                yield return null;

            var expectedValues = new[]
            {
                new Vector4(1, 0, 0, 1),
                new Vector4(0, 1, 0, 0.5f),
                new Vector4(0, 0, 1, 0.25f),
                new Vector4(0.1f, 0.2f, 0.3f, 0.3f),
            };

            mainCamera.transform.Translate(Vector3.up * 500.0f);

            var kMaxFrame = 32;
            for (int step = 0; step < 3; ++step)
            {
                //Move ahead to get a new instance while the old one is culled
                mainCamera.transform.Translate(Vector3.forward * 2.0f);

                var visualEffectObject = new GameObject("VFX_Step_" + step);
                visualEffectObject.transform.position = mainCamera.transform.position + Vector3.forward;

                var vfx = visualEffectObject.AddComponent<VisualEffect>();
                vfx.visualEffectAsset = graph.visualEffectResource.asset;
                yield return null;

                int maxFrame = kMaxFrame;
                while (GetVisualEffectVisibleCount(vfx.visualEffectAsset) != 1 && --maxFrame > 0)
                    yield return null;
                Assert.IsTrue(maxFrame > 0, "Fail at isolating vfx moving camera at step {0}({1})", step, GetVisualEffectVisibleCount(vfx.visualEffectAsset));

                s_Constant_Curve_And_Gradient_Readback = Vector4.zero;
                var request = AsyncGPUReadback.Request(structuredBuffer, Constant_Curve_And_Gradient_Readback);
                var expectedValue = expectedValues[step];

                maxFrame = kMaxFrame;
                while (Vector4.Magnitude(expectedValue - s_Constant_Curve_And_Gradient_Readback) > 1e-3f && --maxFrame > 0)
                {
                    if (request.done)
                        request = AsyncGPUReadback.Request(structuredBuffer, Constant_Curve_And_Gradient_Readback);
                    yield return null;
                }
                Assert.IsTrue(maxFrame > 0, "Fail before modifying curve at step {0} ({1})", step, s_Constant_Curve_And_Gradient_Readback.ToString());

                var vfxUpdate = graph.children.OfType<VFXBasicUpdate>().First();
                var colorSlot = vfxUpdate.children.SelectMany(o => o.inputSlots).First(o => o.valueType == VFXValueType.ColorGradient);
                var curveSlot = vfxUpdate.children.SelectMany(o => o.inputSlots).First(o => o.valueType == VFXValueType.Curve);

                var nextExpectedValue = expectedValues[step + 1];
                colorSlot.value = new Gradient()
                {
                    colorKeys = new []
                    {
                        new GradientColorKey(new Color(nextExpectedValue.x, nextExpectedValue.y, nextExpectedValue.z), 0.0f),
                        new GradientColorKey(new Color(nextExpectedValue.x, nextExpectedValue.y, nextExpectedValue.z), 1.0f)
                    }
                };
                curveSlot.value = new AnimationCurve(new Keyframe(0, nextExpectedValue.w), new Keyframe(1, nextExpectedValue.w));
                graph.RecompileIfNeeded(); //This recompile is expecting to invoke UpdateValues 

                s_Constant_Curve_And_Gradient_Readback = Vector4.zero;
                request = AsyncGPUReadback.Request(structuredBuffer, Constant_Curve_And_Gradient_Readback);
                expectedValue = nextExpectedValue;
                maxFrame = kMaxFrame;
                while (Vector4.Magnitude(expectedValue - s_Constant_Curve_And_Gradient_Readback) > 1e-3f && --maxFrame > 0)
                {
                    if (request.done)
                        request = AsyncGPUReadback.Request(structuredBuffer, Constant_Curve_And_Gradient_Readback);
                    yield return null;
                }
                Assert.IsTrue(maxFrame > 0, "Fail after modifying curve at step {0} ({1})", step, s_Constant_Curve_And_Gradient_Readback.ToString());
            }

            structuredBuffer.Release();
            window.Close();
        }

        [UnityTest]
        public IEnumerator CreateComponent_ManyChanges_Of_Exposed_Property_Insure_No_Leak([ValueSource("trueOrFalse")] bool reinit)
        {
            var commonBaseName = "ouiouioui_";

            var graph = VFXTestCommon.MakeTemporaryGraph();
            var parameterList = new List<VFXModelDescriptorParameters>();
            foreach (var parameter in VFXLibrary.GetParameters())
            {
                var type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(parameter.modelType) == e);
                if (type != VFXValueType.None && parameter.modelType != typeof(Color))
                    parameterList.Add(parameter);
            }
            Assert.IsTrue(parameterList.Count == VFXTestCommon.s_supportedValueType.Length);

            foreach (var parameter in parameterList)
            {
                var newInstance = parameter.CreateInstance();
                newInstance.SetSettingValue("m_ExposedName", commonBaseName + newInstance.type.UserFriendlyName());
                newInstance.SetSettingValue("m_Exposed", true);
                graph.AddChild(newInstance);
            }
            yield return null;

            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(graph));
            while (m_mainObject.GetComponent<VisualEffect>() != null)
                UnityEngine.Object.DestroyImmediate(m_mainObject.GetComponent<VisualEffect>());

            var vfx = m_mainObject.AddComponent<VisualEffect>();
            vfx.visualEffectAsset = graph.visualEffectResource.asset;
            yield return null;

            var memoryHistory = new List<long>() { Profiler.GetRuntimeMemorySizeLong(vfx) };
            var iterationCount = 8;
            for (int iteration = 0; iteration < iterationCount; iteration++)
            {
                foreach (var parameter in parameterList)
                {
                    var type = VFXTestCommon.s_supportedValueType.FirstOrDefault(e => VFXExpression.GetVFXValueTypeFromType(parameter.modelType) == e);
                    var currentName = commonBaseName + parameter.modelType.UserFriendlyName();
                    var newValue = iteration % 2 == 0 ? GetValue_A_Type(parameter.modelType) : GetValue_B_Type(parameter.modelType);
                    Assert.IsTrue(fnHas_UsingBindings(type, vfx, currentName));
                    fnSet_UsingBindings(type, vfx, currentName, newValue);
                }

                if (reinit && iteration % 3 == 0)
                    vfx.Reinit();

                yield return null;
                memoryHistory.Add(Profiler.GetRuntimeMemorySizeLong(vfx));
            }

            var memoryHistoryLog = memoryHistory.Select(o => o.ToString()).Aggregate((a, b) => $"{a}, {b}");
            Assert.AreEqual(iterationCount + 1, memoryHistory.Count, memoryHistoryLog);
            Assert.IsTrue(memoryHistory.All(o => o != 0u && o != long.MaxValue), memoryHistoryLog);

            var first = memoryHistory[0];
            var second = memoryHistory[1];
            var allOther = memoryHistory.Skip(2).ToArray();
            Assert.IsTrue(memoryHistory.All(o => o != 0u && o != long.MaxValue), memoryHistoryLog);
            Assert.IsTrue(allOther.GroupBy(o => o).Count() == 1, memoryHistoryLog);
            Assert.IsTrue(first < allOther.First(), memoryHistoryLog);
            Assert.IsTrue(first < second, memoryHistoryLog);
            Assert.IsTrue(second < allOther.First(), memoryHistoryLog);
        }
    }
}

#endif
