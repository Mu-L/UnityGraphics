using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.Rendering.HighDefinition
{
    /// <summary>
    /// HDRP Rendering Debugger Display Stats.
    /// </summary>
    class HDDebugDisplayStats : DebugDisplayStats
    {
        private DebugFrameTiming m_DebugFrameTiming = new();

        private List<ProfilingSampler> m_RecordedSamplers = new();

        private bool raytracingEnabled => HDRenderPipeline.isReady &&
                                          HDRenderPipeline.currentAsset.currentPlatformRenderPipelineSettings
                                              .supportRayTracing;

        /// <summary>Display ray tracing ray count per frame.</summary>
        public bool countRays = false;

        // Bridge: returns ProfilingSamplers via ProfilingSampler.Get(HDProfileId.X).
        // Replace with HDRPProfilerMarkers direct field references once HDRPProfilerMarkers is created.
        List<ProfilingSampler> GetDefaultSamplers() => GetSamplers(
            HDProfileId.HDRenderPipelineAllRenderRequest,
            HDProfileId.VolumeUpdate,
            HDProfileId.RenderShadowMaps,
            HDProfileId.GBuffer,
            HDProfileId.PrepareLightsForGPU,
            HDProfileId.VolumeVoxelization,
            HDProfileId.VolumetricLighting,
            HDProfileId.VolumetricClouds,
            HDProfileId.VolumetricCloudsTrace,
            HDProfileId.VolumetricCloudsReproject,
            HDProfileId.VolumetricCloudsUpscale,
            HDProfileId.VolumetricCloudsCombine,
            HDProfileId.RenderDeferredLightingCompute,
            HDProfileId.ForwardOpaque,
            HDProfileId.ForwardTransparent,
            HDProfileId.ForwardPreRefraction,
            HDProfileId.ColorPyramid,
            HDProfileId.DepthPyramid,
            HDProfileId.PostProcessing
        );

        List<ProfilingSampler> GetRayTracingSamplers() => GetSamplers(
            HDProfileId.RaytracingBuildCluster,
            HDProfileId.RaytracingCullLights,
            HDProfileId.RaytracingBuildAccelerationStructure,

            // Ray Traced Reflections
            HDProfileId.RaytracingReflectionDirectionGeneration,
            HDProfileId.RaytracingReflectionEvaluation,
            HDProfileId.RaytracingReflectionAdjustWeight,
            HDProfileId.RaytracingReflectionUpscale,
            HDProfileId.RaytracingReflectionFilter,

            // Ray Traced Ambient Occlusion
            HDProfileId.RaytracingAmbientOcclusion,
            HDProfileId.RaytracingFilterAmbientOcclusion,

            // Ray Traced Shadows
            HDProfileId.RaytracingDirectionalLightShadow,
            HDProfileId.RaytracingLightShadow,

            // Ray Traced Indirect Diffuse
            HDProfileId.RaytracingIndirectDiffuseDirectionGeneration,
            HDProfileId.RaytracingIndirectDiffuseEvaluation,
            HDProfileId.RaytracingIndirectDiffuseUpscale,
            HDProfileId.RaytracingFilterIndirectDiffuse,

            HDProfileId.RaytracingDebugOverlay,
            HDProfileId.ForwardPreRefraction,
            HDProfileId.RayTracingRecursiveRendering,
            HDProfileId.RayTracingDepthPrepass,
            HDProfileId.RayTracingFlagMask,
            HDProfileId.RaytracingDeferredLighting
        );

        static List<ProfilingSampler> GetSamplers(params HDProfileId[] ids)
        {
            var result = new List<ProfilingSampler>(ids.Length);
            foreach (var id in ids)
            {
                var sampler = ProfilingSampler.Get(id);
                if (sampler != null)
                    result.Add(sampler);
            }
            return result;
        }

        /// <inheritdoc/>
        public override void EnableProfilingRecorders()
        {
            Debug.Assert(m_RecordedSamplers.Count == 0);

            m_RecordedSamplers.AddRange(GetDefaultSamplers());
            if (raytracingEnabled)
                m_RecordedSamplers.AddRange(GetRayTracingSamplers());
        }

        /// <inheritdoc/>
        public override void DisableProfilingRecorders()
        {
            foreach (var sampler in m_RecordedSamplers)
                sampler.enableRecording = false;

            m_RecordedSamplers.Clear();
        }

        /// <inheritdoc/>
        public override void RegisterDebugUI(List<DebugUI.Widget> list)
        {
            m_DebugFrameTiming.RegisterDebugUI(list);

            var detailedStatsFoldout = new DebugUI.Foldout
            {
                displayName = "Detailed Stats",
                opened = false,
                children =
                {
                    new DebugUI.BoolField
                    {
                        displayName = "Update every second with average",
                        getter = () => averageProfilerTimingsOverASecond,
                        setter = value => averageProfilerTimingsOverASecond = value
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Hide empty scopes",
                        tooltip = "Hide profiling scopes where elapsed time in each category is zero",
                        getter = () => hideEmptyScopes,
                        setter = value => hideEmptyScopes = value
                    }
                }
            };
            detailedStatsFoldout.children.Add(BuildDetailedStatsList("Profiling Scopes", GetDefaultSamplers()));

            if (raytracingEnabled)
            {
                DebugUI.Value CreateRayCountDebugValue(string name, RayCountValues counter)
                {
                    return new DebugUI.Value
                    {
                        displayName = name,
                        getter = () => (HDRenderPipeline.currentPipeline?.GetRaysPerFrame(counter) ?? 0) / 1e6f,
                        refreshRate = 1f / 30f
                    };
                }

                detailedStatsFoldout.children.Add(BuildDetailedStatsList("Ray Tracing Profiling Scopes", GetRayTracingSamplers()));
                detailedStatsFoldout.children.Add(new DebugUI.BoolField { displayName = "Count Rays (MRays/Frame)", getter = () => countRays, setter = value => countRays = value });
                detailedStatsFoldout.children.Add(new DebugUI.Container
                {
                    isHiddenCallback = () => !countRays,
                    children =
                    {
                        CreateRayCountDebugValue("Ambient Occlusion", RayCountValues.AmbientOcclusion),
                        CreateRayCountDebugValue("Shadows Directional", RayCountValues.ShadowDirectional),
                        CreateRayCountDebugValue("Shadows Area", RayCountValues.ShadowAreaLight),
                        CreateRayCountDebugValue("Shadows Point/Spot", RayCountValues.ShadowPointSpot),
                        CreateRayCountDebugValue("Reflections Forward ", RayCountValues.ReflectionForward),
                        CreateRayCountDebugValue("Reflections Deferred", RayCountValues.ReflectionDeferred),
                        CreateRayCountDebugValue("Diffuse GI Forward", RayCountValues.DiffuseGI_Forward),
                        CreateRayCountDebugValue("Diffuse GI Deferred", RayCountValues.DiffuseGI_Deferred),
                        CreateRayCountDebugValue("Recursive Rendering", RayCountValues.Recursive),
                        CreateRayCountDebugValue("Total", RayCountValues.Total),
                    }
                });
            }

            list.Add(detailedStatsFoldout);
        }

        /// <inheritdoc/>
        public override void Update()
        {
            m_DebugFrameTiming.UpdateFrameTiming();
            UpdateDetailedStats(m_RecordedSamplers);
        }
    }
}
