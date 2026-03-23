using System.Collections;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace UnityEngine.Rendering.Tests
{
    class ProfilingSamplerWithCommandBufferTests
    {
        const int kWarmupFrames = 4 /*ProfilerRecorder.GPU_RESULTS_DELAY_FRAMES*/;
        const int kSampledFrames = 2;

        Texture2D m_Texture;

        [OneTimeSetUp]
        public void SetUp()
        {
            m_Texture = new Texture2D(1, 1) { name = "TestTexture" };
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(m_Texture);
        }

        [UnityTest]
        public IEnumerator CommandBufferBeginSample_IsCapturedByProfilerRecorder()
        {
            var sampler = new ProfilingSampler(nameof(CommandBufferBeginSample_IsCapturedByProfilerRecorder));
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, sampler.name);

            var commandBuffer = new CommandBuffer();
            using (new ProfilingScope(commandBuffer, sampler))
            { }

            yield return ExecuteCommandBufferForFrames(commandBuffer, kWarmupFrames, kSampledFrames);

            commandBuffer.Dispose();
            Assert.AreEqual(1, recorder.Count);
            Assert.Greater(recorder.GetSample(0).Count, 0);
        }

        [UnityTest]
        public IEnumerator CommandBufferBeginSampleWithObject_IsCapturedByProfilerRecorder()
        {
            var sampler = new ProfilingSampler(nameof(CommandBufferBeginSampleWithObject_IsCapturedByProfilerRecorder));
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, sampler.name);

            var commandBuffer = new CommandBuffer();
            using (new ProfilingScope(commandBuffer, sampler, m_Texture))
            { }

            yield return ExecuteCommandBufferForFrames(commandBuffer, kWarmupFrames, kSampledFrames);

            commandBuffer.Dispose();
            Assert.AreEqual(1, recorder.Count);
            Assert.Greater(recorder.GetSample(0).Count, 0);
        }

        [Test]
        public void CommandBufferBeginSampleWithNullObject_DoesNotCrash()
        {
            var sampler = new ProfilingSampler(nameof(CommandBufferBeginSampleWithNullObject_DoesNotCrash));
            var commandBuffer = new CommandBuffer();
            Assert.DoesNotThrow(() =>
            {
                using (new ProfilingScope(commandBuffer, sampler, null))
                { }
                Graphics.ExecuteCommandBuffer(commandBuffer);
            });
            commandBuffer.Dispose();
        }

        [UnityTest]
        [UnityPlatform(include = new[]
        {
            RuntimePlatform.WindowsPlayer,
            RuntimePlatform.WindowsEditor,
            RuntimePlatform.PS5,
            RuntimePlatform.Switch
        })]
        public IEnumerator CommandBufferBeginSampleWithObject_GpuSamples_ReturnsNonZeroCount()
        {
            if (!SystemInfo.supportsGpuRecorder)
                yield break;

            var sampler = new ProfilingSampler(nameof(CommandBufferBeginSampleWithObject_GpuSamples_ReturnsNonZeroCount));
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, sampler.name, 1, ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.Default);

            var commandBuffer = new CommandBuffer();
            using (new ProfilingScope(commandBuffer, sampler, m_Texture))
            { }

            yield return ExecuteCommandBufferForFrames(commandBuffer, kWarmupFrames, kSampledFrames);

            commandBuffer.Dispose();
            Assert.AreEqual(1, recorder.Count);
            Assert.Greater(recorder.GetSample(0).Count, 0);
        }

        [UnityTest]
        [UnityPlatform(include = new[]
        {
            RuntimePlatform.WindowsPlayer,
            RuntimePlatform.WindowsEditor,
            RuntimePlatform.PS5,
            RuntimePlatform.Switch
        })]
        public IEnumerator CommandBufferBeginSample_GpuSamples_ReturnsNonZeroCount()
        {
            if (!SystemInfo.supportsGpuRecorder)
                yield break;

            var sampler = new ProfilingSampler(nameof(CommandBufferBeginSample_GpuSamples_ReturnsNonZeroCount));
            using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, sampler.name, 1, ProfilerRecorderOptions.GpuRecorder | ProfilerRecorderOptions.Default);

            var commandBuffer = new CommandBuffer();
            using (new ProfilingScope(commandBuffer, sampler))
            { }

            yield return ExecuteCommandBufferForFrames(commandBuffer, kWarmupFrames, kSampledFrames);

            commandBuffer.Dispose();
            Assert.AreEqual(1, recorder.Count);
            Assert.Greater(recorder.GetSample(0).Count, 0);
        }

        static IEnumerator ExecuteCommandBufferForFrames(CommandBuffer commandBuffer, int warmupFrames, int sampledFrames)
        {
            for (int i = 0; i < warmupFrames; i++)
            {
                Graphics.ExecuteCommandBuffer(commandBuffer);
                yield return null;
            }
            for (int i = 0; i < sampledFrames; i++)
            {
                Graphics.ExecuteCommandBuffer(commandBuffer);
                yield return null;
            }
        }
    }
}
