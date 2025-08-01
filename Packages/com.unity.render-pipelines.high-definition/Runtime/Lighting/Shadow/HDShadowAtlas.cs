using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityEngine.Rendering.HighDefinition
{
    class HDShadowAtlas
    {
        internal struct HDShadowAtlasInitParameters
        {
            internal HDRenderPipeline renderPipeline;
            internal RenderGraph renderGraph;
            internal bool usePersistentTexture;
            internal int width;
            internal int height;
            internal int maxShadowRequests;
            internal string name;
            internal bool isShadowCache;

            internal Material clearMaterial;
            internal HDShadowInitParameters initParams;
            internal BlurAlgorithm blurAlgorithm;
            internal FilterMode filterMode;
            internal DepthBits depthBufferBits;
            internal RenderTextureFormat format;
            internal ConstantBuffer<ShaderVariablesGlobal> cb;

            internal HDShadowAtlasInitParameters(HDRenderPipeline renderPipeline, RenderGraph renderGraph, bool usePersistentTexture, int width, int height,
                                                 Material clearMaterial, int maxShadowRequests, HDShadowInitParameters initParams, ConstantBuffer<ShaderVariablesGlobal> cb)
            {
                this.renderPipeline = renderPipeline;
                this.renderGraph = renderGraph;
                this.usePersistentTexture = usePersistentTexture;
                this.width = width;
                this.height = height;
                this.clearMaterial = clearMaterial;
                this.maxShadowRequests = maxShadowRequests;
                this.initParams = initParams;
                this.blurAlgorithm = BlurAlgorithm.None;
                this.filterMode = FilterMode.Bilinear;
                this.depthBufferBits = DepthBits.Depth16;
                this.format = RenderTextureFormat.Shadowmap;
                this.name = "";
                this.isShadowCache = false;

                this.cb = cb;
            }
        }

        public enum BlurAlgorithm
        {
            None,
            EVSM, // exponential variance shadow maps
            IM // Improved Moment shadow maps
        }

        internal NativeList<HDShadowRequestHandle> m_ShadowRequests  = new NativeList<HDShadowRequestHandle>(Allocator.Persistent); // Lifetime handled by HDShadowManager
        internal bool HasShadowRequests()
        {
            return m_ShadowRequests.Length > 0;
        }

        public int width { get; private set; }
        public int height { get; private set; }

        Material m_ClearMaterial;
        LightingDebugSettings m_LightingDebugSettings;
        FilterMode m_FilterMode;
        DepthBits m_DepthBufferBits;
        RenderTextureFormat m_Format;
        string m_Name;
        string m_MomentName;
        string m_MomentCopyName;
        string m_IntermediateSummedAreaName;
        string m_SummedAreaName;
        HDRenderPipeline m_RenderPipeline;

        // Moment shadow data
        BlurAlgorithm m_BlurAlgorithm;

        // This is only a reference that is hold by the atlas, but its lifetime is responsibility of the shadow manager.
        ConstantBuffer<ShaderVariablesGlobal> m_GlobalConstantBuffer;

        internal NativeList<HDShadowResolutionRequest> shadowResolutionRequestStorage => HDShadowManager.instance.shadowResolutionRequestStorage;

        // This must be true for atlas that contain cached data (effectively this
        // drives what to do with mixed cached shadow map -> if true we filter with only static
        // if false we filter only for dynamic)
        protected bool m_IsACacheForShadows;

        // In case of using persistent textures imported in Render Graph
        bool m_UsePersistentTexture;
        protected RTHandle m_PersistentTexture;
        // m_Output is either pointing to m_PersistentTexture once imported to Render Graph or to a Render Graph-owned texture
        protected TextureHandle m_Output;
        protected TextureHandle m_ShadowMapOutput;

        public TextureDesc GetShadowMapTextureDesc()
        {
            return new TextureDesc(width, height)
            { filterMode = m_FilterMode, depthBufferBits = m_DepthBufferBits, isShadowMap = true, name = m_Name };
        }

        public HDShadowAtlas() { }

        public virtual void InitAtlas(HDShadowAtlasInitParameters initParams)
        {
            if (!m_ShadowRequests.IsCreated)
                m_ShadowRequests = new NativeList<HDShadowRequestHandle>(Allocator.Persistent);
            else
                m_ShadowRequests.Clear();

            this.width = initParams.width;
            this.height = initParams.height;
            m_FilterMode = initParams.filterMode;
            m_DepthBufferBits = initParams.depthBufferBits;
            m_Format = initParams.format;
            m_Name = initParams.name;
            // With render graph, textures are "allocated" every frame so we need to prepare strings beforehand.
            m_MomentName = m_Name + "Moment";
            m_MomentCopyName = m_Name + "MomentCopy";
            m_IntermediateSummedAreaName = m_Name + "IntermediateSummedArea";
            m_SummedAreaName = m_Name + "SummedAreaFinal";
            m_ClearMaterial = initParams.clearMaterial;
            m_BlurAlgorithm = initParams.blurAlgorithm;
            m_RenderPipeline = initParams.renderPipeline;
            m_IsACacheForShadows = initParams.isShadowCache;

            m_GlobalConstantBuffer = initParams.cb;

            AllocPersistentTextureIfNeeded(initParams.renderGraph, initParams.usePersistentTexture);
        }

        public HDShadowAtlas(HDShadowAtlasInitParameters initParams)
        {
            InitAtlas(initParams);
        }

        TextureDesc GetMomentAtlasDesc(string name)
        {
            return new TextureDesc(width / 2, height / 2)
            { format = GraphicsFormat.R32G32_SFloat, useMipMap = true, autoGenerateMips = false, name = name, enableRandomWrite = true };
        }

        TextureDesc GetImprovedMomentAtlasDesc()
        {
            return new TextureDesc(width, height)
            { format = GraphicsFormat.R32G32B32A32_SFloat, name = m_MomentName, enableRandomWrite = true, clearColor = Color.black };
        }

        internal TextureDesc GetAtlasDesc()
        {
            switch (m_BlurAlgorithm)
            {
                case (BlurAlgorithm.None):
                    return GetShadowMapTextureDesc();
                case BlurAlgorithm.EVSM:
                    return GetMomentAtlasDesc(m_MomentName);
                case BlurAlgorithm.IM:
                    return GetImprovedMomentAtlasDesc();
            }

            return default;
        }

        internal RTHandleAllocInfo GetAtlasRTInfo()
        {
            var desc = GetAtlasDesc();

            RTHandleAllocInfo rtAllocInfo = new RTHandleAllocInfo(desc.name)
            {
                slices = desc.slices,
                format = desc.format,
                filterMode = desc.filterMode,
                wrapModeU = desc.wrapMode,
                wrapModeV = desc.wrapMode,
                wrapModeW = desc.wrapMode,
                dimension = desc.dimension,
                enableRandomWrite = desc.enableRandomWrite,
                useMipMap = desc.useMipMap,
                autoGenerateMips = desc.autoGenerateMips,
                anisoLevel = desc.anisoLevel,
                mipMapBias = desc.mipMapBias,
                isShadowMap = desc.isShadowMap,
                msaaSamples = (MSAASamples)desc.msaaSamples,
                bindTextureMS = desc.bindTextureMS,
                useDynamicScale = desc.useDynamicScale,
                useDynamicScaleExplicit = desc.useDynamicScaleExplicit,
                memoryless = desc.memoryless,
                vrUsage = desc.vrUsage,
                enableShadingRate = desc.enableShadingRate,
            };

            return rtAllocInfo;
        }

        public void UpdateSize(Vector2Int size)
        {
            width = size.x;
            height = size.y;
        }

        internal void AddShadowRequest(HDShadowRequestHandle shadowRequest)
        {
            m_ShadowRequests.Add(shadowRequest);
        }

        internal static void AddShadowRequest(ref HDDynamicShadowAtlasDataForShadowRequestUpdateJob shadowAtlas, HDShadowRequestHandle shadowRequest)
        {
            shadowAtlas.shadowRequests.Add(shadowRequest);
        }

        internal static void AddShadowRequest(ref HDCachedShadowAtlasDataForShadowRequestUpdateJob shadowAtlas, HDShadowRequestHandle shadowRequest)
        {
            shadowAtlas.shadowRequests.Add(shadowRequest);
        }


        public void UpdateDebugSettings(LightingDebugSettings lightingDebugSettings)
        {
            m_LightingDebugSettings = lightingDebugSettings;
        }

        public void InvalidateOutput()
        {
            // Since we now store the output TextureHandle (because we only want to create the texture once depending on the control flow),
            // we need to be careful not to keep a "valid" handle when not desired.
            m_Output = TextureHandle.nullHandle;
            m_ShadowMapOutput = TextureHandle.nullHandle;
        }

        public void CreateOrUpdateOutputTexture(RenderGraph renderGraph)
        {
            if (m_UsePersistentTexture)
            {
                var requestedDesc = GetAtlasDesc();
                var currSize = m_PersistentTexture?.GetScaledSize() ?? new Vector2Int();
                // We check if we need to refresh the desc. It is needed for directional lights.
                if (currSize.x != requestedDesc.width ||
                    currSize.y != requestedDesc.height)
                {
                    m_PersistentTexture?.Release();
                    RTHandleAllocInfo rtInfo = GetAtlasRTInfo();
                    m_PersistentTexture = RTHandles.Alloc(requestedDesc.width, requestedDesc.height, rtInfo);
                    m_Output = TextureHandle.nullHandle;
                }

                // Import it only once per RG execution
                if (!m_Output.IsValid())
                    m_Output = renderGraph.ImportTexture(m_PersistentTexture);
            }
            else
            {
                renderGraph.CreateTextureIfInvalid(GetAtlasDesc(), ref m_Output);
            }
        }

        public TextureHandle GetOutputTexture(RenderGraph renderGraph)
        {
            CreateOrUpdateOutputTexture(renderGraph);
            return m_Output;
        }

        public TextureHandle GetShadowMapDepthTexture(RenderGraph renderGraph)
        {
            if (m_BlurAlgorithm == BlurAlgorithm.None)
                return GetOutputTexture(renderGraph);

            // We use the actual shadow map as intermediate target
            renderGraph.CreateTextureIfInvalid(GetShadowMapTextureDesc(), ref m_ShadowMapOutput);
            return m_ShadowMapOutput;
        }

        protected void AllocPersistentTextureIfNeeded(RenderGraph renderGraph, bool usePersistentTexture)
        {
            // First release if not needed anymore.
            if (m_UsePersistentTexture)
            {
                Debug.Assert(usePersistentTexture, "Shadow atlas can't go from imported texture to texture created by Render Graph");
            }

            m_UsePersistentTexture = usePersistentTexture;
            // When we want to manage lifetime manually (for static shadows), we import a texture into Render Graph. Not doing it would break static shadow caching.
            // Otherwise it's created on the fly like a regular render graph texture.
            if (m_UsePersistentTexture)
            {
                var requestedDesc = GetAtlasDesc();
                m_PersistentTexture = RTHandles.Alloc(requestedDesc.width, requestedDesc.height, GetAtlasRTInfo());
            }
        }

        internal void CleanupPersistentTexture(RenderGraph renderGraph)
        {
            if (m_UsePersistentTexture)
            {
                m_UsePersistentTexture = false;
                m_PersistentTexture?.Release();
                m_Output = TextureHandle.nullHandle;
            }
        }

        public bool HasBlurredEVSM()
        {
            return (m_BlurAlgorithm == BlurAlgorithm.EVSM);
        }

        // This is a 9 tap filter, a gaussian with std. dev of 3. This standard deviation with this amount of taps probably cuts
        // the tail of the gaussian a bit too much, and it is a very fat curve, but it seems to work fine for our use case.
        static readonly Vector4[] evsmBlurWeights =
        {
            new Vector4(0.1531703f, 0.1448929f, 0.1226492f, 0.0929025f),
            new Vector4(0.06297021f, 0.0f, 0.0f, 0.0f),
        };

        class RenderShadowMapsCommonPassData
        {
            public TextureHandle atlasTexture;
            public ShaderVariablesGlobal globalCBData;
            public ConstantBuffer<ShaderVariablesGlobal> globalCB;
            public NativeList<HDShadowRequestHandle> shadowRequests;
            public bool isRenderingOnACache;
        }

        class RenderShadowMapsPassData : RenderShadowMapsCommonPassData
        {
            public ShadowDrawingSettings shadowDrawSettings;
            public Material clearMaterial;
            public bool debugClearAtlas;
            public ScriptableRenderContext renderContext;
        }

        private void SetCommonRenderPassData(RenderShadowMapsCommonPassData passData, in IUnsafeRenderGraphBuilder builder, RenderGraph renderGraph, in ShaderVariablesGlobal globalCBData)
        {
            passData.globalCBData = globalCBData;
            passData.globalCB = m_GlobalConstantBuffer;
            passData.shadowRequests = m_ShadowRequests;
            passData.isRenderingOnACache = m_IsACacheForShadows;

            // Only in case of regular shadow map do we render directly in the output texture of the atlas.
            if (m_BlurAlgorithm == BlurAlgorithm.EVSM || m_BlurAlgorithm == BlurAlgorithm.IM)
                passData.atlasTexture = GetShadowMapDepthTexture(renderGraph);
            else
                passData.atlasTexture = GetOutputTexture(renderGraph);
            builder.UseTexture(passData.atlasTexture, AccessFlags.Write);
        }

        struct RenderShadowMapsCommonState
        {
            public bool shouldSkipRequest;
            public bool mixedInDynamicAtlas;

            public static RenderShadowMapsCommonState NewDefault()
            {
                return new RenderShadowMapsCommonState
                {
                    shouldSkipRequest = true
                };
            }
        }

        private static RenderShadowMapsCommonState CommonPerShadowRequestUpdate(CommandBuffer cmd, RenderShadowMapsCommonPassData data, in HDShadowRequest shadowRequest, in HDShadowRequestHandle shadowRequestHandle, ref Vector4[] planesScratchpad, ref UnsafeList<float4> frustumPlanesStorageUnsafe)
        {
            RenderShadowMapsCommonState commonState = RenderShadowMapsCommonState.NewDefault();
            commonState.shouldSkipRequest = shadowRequest.shadowMapType != ShadowMapType.CascadedDirectional ? !shadowRequest.shouldRenderCachedComponent && data.isRenderingOnACache :
                                !shadowRequest.shouldRenderCachedComponent && shadowRequest.shouldUseCachedShadowData;

            if (shadowRequest.shadowMapType == ShadowMapType.CascadedDirectional && shadowRequest.isMixedCached)
            {
                commonState.shouldSkipRequest = !shadowRequest.shouldRenderCachedComponent && data.isRenderingOnACache;
            }

            if (commonState.shouldSkipRequest)
                return commonState;

            commonState.mixedInDynamicAtlas = false;
#if UNITY_2021_1_OR_NEWER
            if (shadowRequest.isMixedCached)
            {
                commonState.mixedInDynamicAtlas = !data.isRenderingOnACache;
            }
#endif

            cmd.SetGlobalDepthBias(1.0f, shadowRequest.slopeBias);
            cmd.SetViewport(data.isRenderingOnACache ? shadowRequest.cachedAtlasViewport : shadowRequest.dynamicAtlasViewport);

            cmd.SetGlobalFloat(HDShaderIDs._ZClip, shadowRequest.zClip ? 1.0f : 0.0f);

            // Setup matrices for shadow rendering:
            Matrix4x4 view = shadowRequest.cullingSplit.view;
            // For dynamic objects to be read in the same "space" as the cached ones we need to take cache translation delta in consideration.
            // otherwise the dynamic objects won't stay attached to casters as camera moves.
            if (commonState.mixedInDynamicAtlas && shadowRequest.shadowMapType == ShadowMapType.CascadedDirectional)
            {
                view *= Matrix4x4.Translate(shadowRequest.cachedShadowData.cacheTranslationDelta);
            }
            Matrix4x4 viewProjection = shadowRequest.cullingSplit.deviceProjectionYFlip * view;
            data.globalCBData._ViewMatrix = view;
            data.globalCBData._InvViewMatrix = view.inverse;
            data.globalCBData._ProjMatrix = shadowRequest.cullingSplit.deviceProjectionYFlip;
            data.globalCBData._InvProjMatrix = shadowRequest.cullingSplit.deviceProjectionYFlip.inverse;
            data.globalCBData._ViewProjMatrix = viewProjection;
            data.globalCBData._InvViewProjMatrix = viewProjection.inverse;
            data.globalCBData._SlopeScaleDepthBias = -shadowRequest.slopeBias;
            data.globalCBData._GlobalMipBias = 0.0f;
            data.globalCBData._GlobalMipBiasPow2 = 1.0f;


            data.globalCB.PushGlobal(cmd, data.globalCBData, HDShaderIDs._ShaderVariablesGlobal);


            for (int i = 0; i < HDShadowRequest.frustumPlanesCount; i++)
            {
                planesScratchpad[i] = frustumPlanesStorageUnsafe[shadowRequestHandle.storageIndexForFrustumPlanes + i];
            }

            cmd.SetGlobalVectorArray(HDShaderIDs._ShadowFrustumPlanes, planesScratchpad);

            return commonState;
        }

        private static void ResetDepthState(CommandBuffer cmd)
        {
            cmd.SetGlobalFloat(HDShaderIDs._ZClip, 1.0f);   // Re-enable zclip globally
            cmd.SetGlobalDepthBias(0.0f, 0.0f);             // Reset depth bias.
        }

        public static Vector4[] frustumPlanesScratchpad = new Vector4[HDShadowRequest.frustumPlanesCount];
        internal unsafe TextureHandle RenderShadowMaps(RenderGraph renderGraph, ScriptableRenderContext renderContext, CullingResults cullResults, in ShaderVariablesGlobal globalCBData, FrameSettings frameSettings, string shadowPassName)
        {
            TextureHandle atlasTexture;

            using (var builder = renderGraph.AddUnsafePass<RenderShadowMapsPassData>("Render Shadow Maps", out var passData, ProfilingSampler.Get(HDProfileId.RenderShadowMaps)))
            {
                SetCommonRenderPassData(passData, builder, renderGraph, globalCBData);

                passData.clearMaterial = m_ClearMaterial;
                passData.debugClearAtlas = m_LightingDebugSettings.clearShadowAtlas;
                passData.shadowDrawSettings = new ShadowDrawingSettings(cullResults, 0);
                passData.shadowDrawSettings.useRenderingLayerMaskTest = frameSettings.IsEnabled(FrameSettingsField.LightLayers);
                passData.renderContext = renderContext;

                builder.SetRenderFunc(
                    (RenderShadowMapsPassData data, UnsafeGraphContext ctx) =>
                    {
                        var natCmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        natCmd.SetRenderTarget(data.atlasTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

                        // Clear the whole atlas to avoid garbage outside of current request when viewing it.
                        if (data.debugClearAtlas)
                            CoreUtils.DrawFullScreen(natCmd, data.clearMaterial, null, 0);

                        NativeList<HDShadowRequest> requestStorage = HDShadowRequestDatabase.instance.hdShadowRequestStorage;
                        ref UnsafeList<HDShadowRequest> requestStorageUnsafe = ref *requestStorage.GetUnsafeList();
                        NativeList<float4> frustumPlanesStorage = HDShadowRequestDatabase.instance.frustumPlanesStorage;
                        ref UnsafeList<float4> frustumPlanesStorageUnsafe = ref *frustumPlanesStorage.GetUnsafeList();

                        Vector4[] planesScratchpad = frustumPlanesScratchpad;

                        foreach (var shadowRequestHandle in data.shadowRequests)
                        {
                            ref var shadowRequest = ref requestStorageUnsafe.ElementAt(shadowRequestHandle.storageIndexForShadowRequest);
                            var commonState = CommonPerShadowRequestUpdate(natCmd, data, shadowRequest, shadowRequestHandle, ref planesScratchpad, ref frustumPlanesStorageUnsafe);
                            if (commonState.shouldSkipRequest)
                                continue;

    #if UNITY_2021_1_OR_NEWER
                            if (shadowRequest.isMixedCached)
                            {
                                commonState.mixedInDynamicAtlas = !data.isRenderingOnACache;
                                data.shadowDrawSettings.objectsFilter = commonState.mixedInDynamicAtlas ? ShadowObjectsFilter.DynamicOnly : ShadowObjectsFilter.StaticOnly;
                            }
                            else
                            {
                                data.shadowDrawSettings.objectsFilter = ShadowObjectsFilter.AllObjects;
                            }
    #endif

                            if (!commonState.mixedInDynamicAtlas)
                                CoreUtils.DrawFullScreen(natCmd, data.clearMaterial, null, 0);

                            data.shadowDrawSettings.lightIndex = shadowRequest.lightIndex;
                            data.shadowDrawSettings.splitIndex = shadowRequest.cullingSplit.splitIndex;

                            //TODO(ddebaets) as the shadowDrawSettings are modified in this loop, we generate this RL very last minute
                            // We might want to refactor this and create the RL ahead of time (especially if we ever allow AsyncPrepare on them)
                            var rl = data.renderContext.CreateShadowRendererList(ref data.shadowDrawSettings);
                            natCmd.DrawRendererList(rl);
                        }

                        ResetDepthState(natCmd);
                    });

                m_ShadowMapOutput = passData.atlasTexture;
                atlasTexture = passData.atlasTexture;
            }

            return atlasTexture;
        }

        class EVSMBlurMomentsPassData
        {
            public TextureHandle atlasTexture;
            public TextureHandle momentAtlasTexture1;
            public TextureHandle momentAtlasTexture2;

            public ComputeShader evsmShadowBlurMomentsCS;
            public NativeList<HDShadowRequestHandle> shadowRequests;
            public bool isRenderingOnACache;
        }

        unsafe TextureHandle EVSMBlurMoments(RenderGraph renderGraph, TextureHandle inputAtlas)
        {
            using (var builder = renderGraph.AddUnsafePass<EVSMBlurMomentsPassData>("EVSM Blur Moments", out var passData, ProfilingSampler.Get(HDProfileId.RenderEVSMShadowMaps)))
            {
                passData.evsmShadowBlurMomentsCS = m_RenderPipeline.runtimeShaders.evsmBlurCS;
                passData.shadowRequests = m_ShadowRequests;
                passData.isRenderingOnACache = m_IsACacheForShadows;
                passData.atlasTexture = inputAtlas;
                builder.UseTexture(passData.atlasTexture, AccessFlags.Read);
                passData.momentAtlasTexture1 = GetOutputTexture(renderGraph);
                builder.UseTexture(passData.momentAtlasTexture1, AccessFlags.Write);
                passData.momentAtlasTexture2 = renderGraph.CreateTexture(GetMomentAtlasDesc(m_MomentCopyName));
                builder.UseTexture(passData.momentAtlasTexture2, AccessFlags.Write);

                builder.SetRenderFunc(
                    (EVSMBlurMomentsPassData data, UnsafeGraphContext ctx) =>
                    {
                        var natCmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        ComputeShader shadowBlurMomentsCS = data.evsmShadowBlurMomentsCS;
                        RTHandle[] momentAtlasRenderTextures = ctx.renderGraphPool.GetTempArray<RTHandle>(2);
                        momentAtlasRenderTextures[0] = data.momentAtlasTexture1;
                        momentAtlasRenderTextures[1] = data.momentAtlasTexture2;

                        int generateAndBlurMomentsKernel = shadowBlurMomentsCS.FindKernel("ConvertAndBlur");
                        int blurMomentsKernel = shadowBlurMomentsCS.FindKernel("Blur");
                        int copyMomentsKernel = shadowBlurMomentsCS.FindKernel("CopyMoments");

                        RTHandle atlasRenderTexture = data.atlasTexture;

                        natCmd.SetComputeTextureParam(shadowBlurMomentsCS, generateAndBlurMomentsKernel, HDShaderIDs._DepthTexture, atlasRenderTexture);
                        natCmd.SetComputeVectorArrayParam(shadowBlurMomentsCS, HDShaderIDs._BlurWeightsStorage, evsmBlurWeights);

                        // We need to store in which of the two moment texture a request will have its last version stored in for a final patch up at the end.
                        var finalAtlasTexture = stackalloc int[data.shadowRequests.Length];

                        NativeList<HDShadowRequest> requestStorage = HDShadowRequestDatabase.instance.hdShadowRequestStorage;
                        ref UnsafeList<HDShadowRequest> requestStorageUnsafe = ref *requestStorage.GetUnsafeList();

                        int requestIdx = 0;
                        foreach (var shadowRequestHandle in data.shadowRequests)
                        {
                            ref var shadowRequest = ref requestStorageUnsafe.ElementAt(shadowRequestHandle.storageIndexForShadowRequest);
                            bool shouldSkipRequest = shadowRequest.shadowMapType != ShadowMapType.CascadedDirectional ? !shadowRequest.shouldRenderCachedComponent && data.isRenderingOnACache :
                                                                                                                        !shadowRequest.shouldRenderCachedComponent && shadowRequest.shouldUseCachedShadowData;

                            if (shouldSkipRequest)
                                continue;

                            var viewport = data.isRenderingOnACache ? shadowRequest.cachedAtlasViewport : shadowRequest.dynamicAtlasViewport;

                            using (new ProfilingScope(ctx.cmd, ProfilingSampler.Get(HDProfileId.RenderEVSMShadowMapsBlur)))
                            {
                                int downsampledWidth = Mathf.CeilToInt(viewport.width * 0.5f);
                                int downsampledHeight = Mathf.CeilToInt(viewport.height * 0.5f);

                                Vector2 DstRectOffset = new Vector2(viewport.min.x * 0.5f, viewport.min.y * 0.5f);

                                natCmd.SetComputeTextureParam(shadowBlurMomentsCS, generateAndBlurMomentsKernel, HDShaderIDs._OutputTexture, momentAtlasRenderTextures[0]);
                                natCmd.SetComputeVectorParam(shadowBlurMomentsCS, HDShaderIDs._SrcRect, new Vector4(viewport.min.x, viewport.min.y, viewport.width, viewport.height));
                                natCmd.SetComputeVectorParam(shadowBlurMomentsCS, HDShaderIDs._DstRect, new Vector4(DstRectOffset.x, DstRectOffset.y, 1.0f / atlasRenderTexture.rt.width, 1.0f / atlasRenderTexture.rt.height));
                                natCmd.SetComputeFloatParam(shadowBlurMomentsCS, HDShaderIDs._EVSMExponent, shadowRequest.evsmParams.x);

                                int dispatchSizeX = ((int)downsampledWidth + 7) / 8;
                                int dispatchSizeY = ((int)downsampledHeight + 7) / 8;

                                natCmd.DispatchCompute(shadowBlurMomentsCS, generateAndBlurMomentsKernel, dispatchSizeX, dispatchSizeY, 1);

                                int currentAtlasMomentSurface = 0;

                                RTHandle GetMomentRT() { return momentAtlasRenderTextures[currentAtlasMomentSurface]; }
                                RTHandle GetMomentRTCopy() { return momentAtlasRenderTextures[(currentAtlasMomentSurface + 1) & 1]; }

                                natCmd.SetComputeVectorParam(shadowBlurMomentsCS, HDShaderIDs._SrcRect, new Vector4(DstRectOffset.x, DstRectOffset.y, downsampledWidth, downsampledHeight));
                                for (int i = 0; i < shadowRequest.evsmParams.w; ++i)
                                {
                                    currentAtlasMomentSurface = (currentAtlasMomentSurface + 1) & 1;
                                    natCmd.SetComputeTextureParam(shadowBlurMomentsCS, blurMomentsKernel, HDShaderIDs._InputTexture, GetMomentRTCopy());
                                    natCmd.SetComputeTextureParam(shadowBlurMomentsCS, blurMomentsKernel, HDShaderIDs._OutputTexture, GetMomentRT());

                                    natCmd.DispatchCompute(shadowBlurMomentsCS, blurMomentsKernel, dispatchSizeX, dispatchSizeY, 1);
                                }

                                finalAtlasTexture[requestIdx++] = currentAtlasMomentSurface;
                            }
                        }

                        // We patch up the atlas with the requests that, due to different count of blur passes, remained in the copy
                        for (int i = 0; i < data.shadowRequests.Length; ++i)
                        {
                            if (finalAtlasTexture[i] != 0)
                            {
                                using (new ProfilingScope(ctx.cmd, ProfilingSampler.Get(HDProfileId.RenderEVSMShadowMapsCopyToAtlas)))
                                {
                                    var shadowRequestHandle = data.shadowRequests[i];
                                    ref var shadowRequest = ref requestStorageUnsafe.ElementAt(shadowRequestHandle.storageIndexForShadowRequest);
                                    var viewport = data.isRenderingOnACache ? shadowRequest.cachedAtlasViewport : shadowRequest.dynamicAtlasViewport;
                                    int downsampledWidth = Mathf.CeilToInt(viewport.width * 0.5f);
                                    int downsampledHeight = Mathf.CeilToInt(viewport.height * 0.5f);

                                    natCmd.SetComputeVectorParam(shadowBlurMomentsCS, HDShaderIDs._SrcRect, new Vector4(viewport.min.x * 0.5f, viewport.min.y * 0.5f, downsampledWidth, downsampledHeight));
                                    natCmd.SetComputeTextureParam(shadowBlurMomentsCS, copyMomentsKernel, HDShaderIDs._InputTexture, momentAtlasRenderTextures[1]);
                                    natCmd.SetComputeTextureParam(shadowBlurMomentsCS, copyMomentsKernel, HDShaderIDs._OutputTexture, momentAtlasRenderTextures[0]);

                                    int dispatchSizeX = ((int)downsampledWidth + 7) / 8;
                                    int dispatchSizeY = ((int)downsampledHeight + 7) / 8;

                                    natCmd.DispatchCompute(shadowBlurMomentsCS, copyMomentsKernel, dispatchSizeX, dispatchSizeY, 1);
                                }
                            }
                        }
                    });

                return passData.momentAtlasTexture1;
            }
        }

        class IMBlurMomentPassData
        {
            public TextureHandle atlasTexture;
            public TextureHandle momentAtlasTexture;
            public TextureHandle intermediateSummedAreaTexture;
            public TextureHandle summedAreaTexture;

            public NativeList<HDShadowRequestHandle> shadowRequests;
            public ComputeShader imShadowBlurMomentsCS;
            public bool isRenderingOnACache;
        }

        unsafe TextureHandle IMBlurMoment(RenderGraph renderGraph, TextureHandle atlasTexture)
        {
            using (var builder = renderGraph.AddUnsafePass<IMBlurMomentPassData>("EVSM Blur Moments", out var passData, ProfilingSampler.Get(HDProfileId.RenderMomentShadowMaps)))
            {
                passData.shadowRequests = m_ShadowRequests;
                passData.isRenderingOnACache = m_IsACacheForShadows;
                passData.imShadowBlurMomentsCS = m_RenderPipeline.runtimeShaders.momentShadowsCS;
                passData.atlasTexture = atlasTexture;
                builder.UseTexture(passData.atlasTexture, AccessFlags.Read);
                passData.momentAtlasTexture = GetOutputTexture(renderGraph);
                builder.UseTexture(passData.momentAtlasTexture, AccessFlags.Write);
                passData.intermediateSummedAreaTexture = renderGraph.CreateTexture(new TextureDesc(width, height)
                { format = GraphicsFormat.R32G32B32A32_SInt, name = m_IntermediateSummedAreaName, enableRandomWrite = true, clearBuffer = true, clearColor = Color.black });
                builder.UseTexture(passData.intermediateSummedAreaTexture, AccessFlags.Write);
                passData.summedAreaTexture = renderGraph.CreateTexture(new TextureDesc(width, height)
                { format = GraphicsFormat.R32G32B32A32_SInt, name = m_SummedAreaName, enableRandomWrite = true, clearColor = Color.black });
                builder.UseTexture(passData.summedAreaTexture, AccessFlags.Write);

                builder.SetRenderFunc(
                    (IMBlurMomentPassData data, UnsafeGraphContext ctx) =>
                    {
                        var natCmd = CommandBufferHelpers.GetNativeCommandBuffer(ctx.cmd);
                        // If the target kernel is not available
                        ComputeShader momentCS = data.imShadowBlurMomentsCS;
                        if (momentCS == null) return;

                        int computeMomentKernel = momentCS.FindKernel("ComputeMomentShadows");
                        int summedAreaHorizontalKernel = momentCS.FindKernel("MomentSummedAreaTableHorizontal");
                        int summedAreaVerticalKernel = momentCS.FindKernel("MomentSummedAreaTableVertical");

                        RTHandle atlas = data.atlasTexture;
                        RTHandle atlasMoment = data.momentAtlasTexture;
                        RTHandle intermediateSummedAreaTexture = data.intermediateSummedAreaTexture;
                        RTHandle summedAreaTexture = data.summedAreaTexture;

                        NativeList<HDShadowRequest> requestStorage = HDShadowRequestDatabase.instance.hdShadowRequestStorage;
                        ref UnsafeList<HDShadowRequest> requestStorageUnsafe = ref *requestStorage.GetUnsafeList();

                        // Alright, so the thing here is that for every sub-shadow map of the atlas, we need to generate the moment shadow map
                        foreach (var shadowRequestHandle in data.shadowRequests)
                        {
                            ref var shadowRequest = ref requestStorageUnsafe.ElementAt(shadowRequestHandle.storageIndexForShadowRequest);
                            // Let's bind the resources of this
                            natCmd.SetComputeTextureParam(momentCS, computeMomentKernel, HDShaderIDs._ShadowmapAtlas, atlas);
                            natCmd.SetComputeTextureParam(momentCS, computeMomentKernel, HDShaderIDs._MomentShadowAtlas, atlasMoment);
                            natCmd.SetComputeVectorParam(momentCS, HDShaderIDs._MomentShadowmapSlotST, new Vector4(shadowRequest.dynamicAtlasViewport.width, shadowRequest.dynamicAtlasViewport.height, shadowRequest.dynamicAtlasViewport.min.x, shadowRequest.dynamicAtlasViewport.min.y));

                            // First of all we need to compute the moments
                            int numTilesX = Math.Max((int)shadowRequest.dynamicAtlasViewport.width / 8, 1);
                            int numTilesY = Math.Max((int)shadowRequest.dynamicAtlasViewport.height / 8, 1);
                            natCmd.DispatchCompute(momentCS, computeMomentKernel, numTilesX, numTilesY, 1);

                            // Do the horizontal pass of the summed area table
                            natCmd.SetComputeTextureParam(momentCS, summedAreaHorizontalKernel, HDShaderIDs._SummedAreaTableInputFloat, atlasMoment);
                            natCmd.SetComputeTextureParam(momentCS, summedAreaHorizontalKernel, HDShaderIDs._SummedAreaTableOutputInt, intermediateSummedAreaTexture);
                            natCmd.SetComputeFloatParam(momentCS, HDShaderIDs._IMSKernelSize, shadowRequest.kernelSize);
                            natCmd.SetComputeVectorParam(momentCS, HDShaderIDs._MomentShadowmapSize, new Vector2((float)atlasMoment.referenceSize.x, (float)atlasMoment.referenceSize.y));

                            int numLines = Math.Max((int)shadowRequest.dynamicAtlasViewport.width / 64, 1);
                            natCmd.DispatchCompute(momentCS, summedAreaHorizontalKernel, numLines, 1, 1);

                            // Do the horizontal pass of the summed area table
                            natCmd.SetComputeTextureParam(momentCS, summedAreaVerticalKernel, HDShaderIDs._SummedAreaTableInputInt, intermediateSummedAreaTexture);
                            natCmd.SetComputeTextureParam(momentCS, summedAreaVerticalKernel, HDShaderIDs._SummedAreaTableOutputInt, summedAreaTexture);
                            natCmd.SetComputeVectorParam(momentCS, HDShaderIDs._MomentShadowmapSize, new Vector2((float)atlasMoment.referenceSize.x, (float)atlasMoment.referenceSize.y));
                            natCmd.SetComputeFloatParam(momentCS, HDShaderIDs._IMSKernelSize, shadowRequest.kernelSize);

                            int numColumns = Math.Max((int)shadowRequest.dynamicAtlasViewport.height / 64, 1);
                            natCmd.DispatchCompute(momentCS, summedAreaVerticalKernel, numColumns, 1, 1);

                            // Push the global texture
                            natCmd.SetGlobalTexture(HDShaderIDs._SummedAreaTableInputInt, summedAreaTexture);
                        }
                    });

                return passData.momentAtlasTexture;
            }
        }

        internal TextureHandle BlurShadows(RenderGraph renderGraph)
        {
            if (m_ShadowRequests.Length == 0)
            {
                return renderGraph.defaultResources.defaultShadowTexture;
            }

            if (m_BlurAlgorithm == BlurAlgorithm.EVSM)
            {
                return EVSMBlurMoments(renderGraph, m_ShadowMapOutput);
            }
            else if (m_BlurAlgorithm == BlurAlgorithm.IM)
            {
                return IMBlurMoment(renderGraph, m_ShadowMapOutput);
            }
            else // Regular shadow maps.
            {
                return m_ShadowMapOutput;
            }
        }
        internal TextureHandle RenderShadows(RenderGraph renderGraph, ScriptableRenderContext renderContext, CullingResults cullResults, in ShaderVariablesGlobal globalCB, FrameSettings frameSettings, string shadowPassName)
        {
            if (m_ShadowRequests.Length == 0)
            {
                return renderGraph.defaultResources.defaultShadowTexture;
            }

            RenderShadowMaps(renderGraph, renderContext, cullResults, globalCB, frameSettings, shadowPassName);

            if (m_BlurAlgorithm == BlurAlgorithm.EVSM)
            {
                return EVSMBlurMoments(renderGraph, m_ShadowMapOutput);
            }
            else if (m_BlurAlgorithm == BlurAlgorithm.IM)
            {
                return IMBlurMoment(renderGraph, m_ShadowMapOutput);
            }
            else // Regular shadow maps.
            {
                return m_ShadowMapOutput;
            }
        }

        public unsafe void AddBlitRequestsForUpdatedShadows(HDDynamicShadowAtlas dynamicAtlas)
        {
            if (m_IsACacheForShadows)
            {
                NativeList<HDShadowRequest> requestStorage = HDShadowRequestDatabase.instance.hdShadowRequestStorage;
                ref UnsafeList<HDShadowRequest> requestStorageUnsafe = ref *requestStorage.GetUnsafeList();
                foreach (var requestHandle in m_ShadowRequests)
                {
                    ref var request = ref requestStorageUnsafe.ElementAt(requestHandle.storageIndexForShadowRequest);
                    if (request.shouldRenderCachedComponent) // meaning it has been updated this time frame
                    {
                        if (request.isMixedCached)
                        {
                            dynamicAtlas.AddRequestToPendingBlitFromCache(requestHandle);
                        }
                    }
                }
            }
        }
        public virtual void DisplayAtlas(RTHandle atlasTexture, CommandBuffer cmd, Material debugMaterial, Rect atlasViewport, float screenX, float screenY, float screenSizeX, float screenSizeY, float minValue, float maxValue, MaterialPropertyBlock mpb, float scaleFactor = 1)
        {
            if (atlasTexture == null)
                return;

            Vector4 validRange = new Vector4(minValue, 1.0f / (maxValue - minValue));
            float rWidth = 1.0f / width;
            float rHeight = 1.0f / height;
            Vector4 scaleBias = Vector4.Scale(new Vector4(rWidth, rHeight, rWidth, rHeight), new Vector4(atlasViewport.width, atlasViewport.height, atlasViewport.x, atlasViewport.y));

            mpb.SetTexture("_AtlasTexture", atlasTexture);
            mpb.SetVector("_TextureScaleBias", scaleBias);
            mpb.SetVector("_ValidRange", validRange);
            mpb.SetFloat("_RcpGlobalScaleFactor", scaleFactor);
            cmd.SetViewport(new Rect(screenX, screenY, screenSizeX, screenSizeY));
            cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, debugMaterial.FindPass("RegularShadow"), MeshTopology.Triangles, 3, 1, mpb);
        }

        public virtual void Clear()
        {
            m_ShadowRequests.Clear();
        }

        public void Release(RenderGraph renderGraph)
        {
            CleanupPersistentTexture(renderGraph);
        }

        internal virtual void DisposeNativeCollections()
        {
            if (m_ShadowRequests.IsCreated)
            {
                m_ShadowRequests.Dispose();
                m_ShadowRequests = default;
            }
        }
    }
}
