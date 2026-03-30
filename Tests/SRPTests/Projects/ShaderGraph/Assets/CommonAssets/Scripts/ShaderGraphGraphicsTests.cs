using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Graphics;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

public class ShaderGraphGraphicsTests
{
    [IgnoreGraphicsTest("InputNodes|SamplerStateTests|UVNodes", "GLES3 renders these tests incorrectly (FB: 1354427)", RuntimePlatform.Android, GraphicsDeviceType.OpenGLES3)]
    [IgnoreGraphicsTest("InputNodes", "UUM-134140", RuntimePlatform.WindowsEditor, GraphicsDeviceType.Direct3D12)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.OpenGLES3)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.PlayStation4)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.XboxOne)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.Metal)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.OpenGLCore)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.Direct3D12)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.Vulkan)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.Switch)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.XboxOneD3D12)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.GameCoreXboxOne)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.GameCoreXboxSeries)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.PlayStation5)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.PlayStation5NGGC)]
    [IgnoreGraphicsTest("InstanceIDWithKeywords", "Platform Independent", GraphicsDeviceType.WebGPU)]
    [IgnoreGraphicsTest("TransformNode", "Test is unstable", ColorSpace.Linear, RuntimePlatform.Android, GraphicsDeviceType.Vulkan)]
    [IgnoreGraphicsTest("InstancedRendering", "Test requires conversion to Render Graph")]
    
    [SceneGraphicsTest("Assets/Scenes")]
    [UnityTest, Category("ShaderGraph")]
    public IEnumerator Run(SceneGraphicsTestCase testCase)
    {
        GraphicsTestLogger.Log($"Running test case {testCase.ScenePath} with reference image {testCase.ScenePath}. {testCase.ReferenceImage.LoadMessage}.");
		GraphicsTestLogger.Log($"Running test case '{testCase}' with scene '{testCase.ScenePath}' {testCase.ReferenceImage.LoadMessage}.");
        SceneManager.LoadScene(testCase.ScenePath);

        // Always wait one frame for scene load
        yield return null;

        var camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        var settings = Object.FindAnyObjectByType<ShaderGraphGraphicsTestSettings>();
        Assert.IsNotNull(settings, "Invalid test scene, couldn't find ShaderGraphGraphicsTestSettings");
        settings.OnTestBegin();

        for (int i = 0; i < settings.WaitFrames; i++)
            yield return null;

        ImageAssert.AreEqual(testCase.ReferenceImage.Image, camera, settings.ImageComparisonSettings, testCase.ReferenceImage.LoadMessage);
        settings.OnTestComplete();
    }
}
