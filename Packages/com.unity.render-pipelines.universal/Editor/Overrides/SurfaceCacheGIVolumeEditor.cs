#if SURFACE_CACHE

using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityEditor.Rendering.Universal
{
    [CustomEditor(typeof(SurfaceCacheGIVolumeOverride))]
    internal class SurfaceCacheGIVolumeEditor : VolumeComponentEditor
    {
        protected SerializedDataParameter m_Quality;

        protected SerializedDataParameter m_MultiBounce;
        protected SerializedDataParameter m_BouncePatchAllocation;
        protected SerializedDataParameter m_SampleCount;

        protected SerializedDataParameter m_TemporalSmoothing;
        protected SerializedDataParameter m_SpatialFilterEnabled;
        protected SerializedDataParameter m_SpatialSampleCount;
        protected SerializedDataParameter m_SpatialRadius;
        protected SerializedDataParameter m_TemporalPostFilter;

        protected SerializedDataParameter m_LookupSampleCount;
        protected SerializedDataParameter m_UpsamplingKernelSize;
        protected SerializedDataParameter m_UpsamplingSampleCount;

        protected SerializedDataParameter m_VolumeSize;
        protected SerializedDataParameter m_VolumeResolution;
        protected SerializedDataParameter m_VolumeCascadeCount;

        protected SerializedDataParameter m_CascadeMovement;

        protected SerializedDataParameter m_DefragCount;

        struct LightTransportSetting
        {
            public bool multiBounce;
            public bool bouncePatchAllocation;
            public int sampleCount;
        }

        struct PatchFilteringSetting
        {
            public float temporalSmoothing;
            public bool spatialFilterEnabled;
            public int spatialSampleCount;
            public float spatialRadius;
            public bool temporalPostFilter;
        }

        struct ScreenFilteringSetting
        {
            public int lookupSampleCount;
            public float upsamplingKernelSize;
            public int upsamplingSampleCount;
        }

        struct QualitySetting
        {
            public LightTransportSetting lightTransport;
            public PatchFilteringSetting patchFiltering;
            public ScreenFilteringSetting screenFiltering;
        }

        // Quality preset definitions; indexed by SurfaceCacheGIQuality (Low=0, Medium=1, High=2, Ultra=3).
        static readonly QualitySetting[] k_QualityPresets =
        {
            // Low
            new QualitySetting
            {
                lightTransport  = new LightTransportSetting  { multiBounce = false, bouncePatchAllocation = false, sampleCount = 1 },
                patchFiltering  = new PatchFilteringSetting  { temporalSmoothing = 0.9f, spatialFilterEnabled = false, spatialSampleCount = 4, spatialRadius = 1.0f, temporalPostFilter = false },
                screenFiltering = new ScreenFilteringSetting { lookupSampleCount = 4, upsamplingKernelSize = 2.0f, upsamplingSampleCount = 1 },
            },
            // Medium
            new QualitySetting
            {
                lightTransport  = new LightTransportSetting  { multiBounce = true, bouncePatchAllocation = true, sampleCount = 2 },
                patchFiltering  = new PatchFilteringSetting  { temporalSmoothing = 0.8f, spatialFilterEnabled = true, spatialSampleCount = 4, spatialRadius = 1.0f, temporalPostFilter = true },
                screenFiltering = new ScreenFilteringSetting { lookupSampleCount = 6, upsamplingKernelSize = 4.0f, upsamplingSampleCount = 2 },
            },
            // High
            new QualitySetting
            {
                lightTransport  = new LightTransportSetting  { multiBounce = true, bouncePatchAllocation = true, sampleCount = 4 },
                patchFiltering  = new PatchFilteringSetting  { temporalSmoothing = 0.7f, spatialFilterEnabled = true, spatialSampleCount = 6, spatialRadius = 1.5f, temporalPostFilter = true },
                screenFiltering = new ScreenFilteringSetting { lookupSampleCount = 8, upsamplingKernelSize = 5.0f, upsamplingSampleCount = 4 },
            },
            // Ultra
            new QualitySetting
            {
                lightTransport  = new LightTransportSetting  { multiBounce = true, bouncePatchAllocation = true, sampleCount = 8 },
                patchFiltering  = new PatchFilteringSetting  { temporalSmoothing = 0.6f, spatialFilterEnabled = true, spatialSampleCount = 8, spatialRadius = 2.0f, temporalPostFilter = true },
                screenFiltering = new ScreenFilteringSetting { lookupSampleCount = 8, upsamplingKernelSize = 7.0f, upsamplingSampleCount = 8 },
            },
        };

        static GUIContent s_Quality = EditorGUIUtility.TrTextContent("Quality", "Quality preset for Surface Cache GI. Select Custom to manually adjust individual parameters.");
        static GUIContent s_MultiBounce = EditorGUIUtility.TrTextContent("Multi Bounce", "Enable multi-bounce global illumination for more accurate light propagation.");
        static GUIContent s_BouncePatchAllocation = EditorGUIUtility.TrTextContent("Bounce Patch Allocation", "When enabled, new patches are allocated at ray hit locations when multi-bounce cache lookups fail");
        static GUIContent s_SampleCount = EditorGUIUtility.TrTextContent("Sample Count", "Number of samples used for GI estimation. Higher values improve quality at performance cost.");
        static GUIContent s_TemporalSmoothing = EditorGUIUtility.TrTextContent("Temporal Smoothing", "Temporal smoothing for patch data. Higher values produce more stable results but slower response to lighting changes.");
        static GUIContent s_SpatialFilterEnabled = EditorGUIUtility.TrTextContent("Spatial Filter", "Enables spatial filtering across patches. This reduces noise but may also increase leaking.");
        static GUIContent s_SpatialSampleCount = EditorGUIUtility.TrTextContent("Spatial Sample Count", "Number of samples for spatial filtering. Higher values improve quality at performance cost.");
        static GUIContent s_SpatialRadius = EditorGUIUtility.TrTextContent("Spatial Radius", "Radius used for the spatial filtering kernel. Larger values reduces noise but may cause over-blurring.");
        static GUIContent s_TemporalPostFilter = EditorGUIUtility.TrTextContent("Temporal Post Filter", "Enable temporal post-filtering for additional stability.");
        static GUIContent s_LookupSampleCount = EditorGUIUtility.TrTextContent("Lookup Sample Count", "Number of samples for screen-space lookups.");
        static GUIContent s_UpsamplingKernelSize = EditorGUIUtility.TrTextContent("Upsampling Kernel Size", "Kernel size for upsampling filtering.");
        static GUIContent s_UpsamplingSampleCount = EditorGUIUtility.TrTextContent("Upsampling Sample Count", "Number of samples for upsampling. Higher values improve quality at performance cost.");
        static GUIContent s_VolumeSize = EditorGUIUtility.TrTextContent("Size", "Size of the surface cache volume in world units. Can be changed at runtime without a performance hitch.");
        static GUIContent s_VolumeResolution = EditorGUIUtility.TrTextContent("Resolution", "Spatial resolution of the volume grid. Higher values improve spatial detail but use more memory. Changing at runtime can cause a performance hitch if internal buffers are reallocated.");
        static GUIContent s_VolumeCascadeCount = EditorGUIUtility.TrTextContent("Cascade Count", "Number of volume cascades. More cascades extend the volume's effective range. Changing at runtime can cause a performance hitch if internal buffers are reallocated.");
        static GUIContent s_CascadeMovement = EditorGUIUtility.TrTextContent("Cascade Movement", "Enable volume cascades to follow the camera.");

        // Section header labels with tooltips
        static GUIContent s_LightTransportHeader = EditorGUIUtility.TrTextContent("Light Transport", "Controls how many rays are cast per patch to estimate indirect lighting. More samples reduce variance but increase GPU cost per frame.");
        static GUIContent s_PatchFilteringHeader = EditorGUIUtility.TrTextContent("Patch Filtering", "Controls how patch irradiance data is filtered over time and space. These settings trade temporal stability and spatial smoothness against responsiveness to lighting changes and light leaking.");
        static GUIContent s_ScreenFilteringHeader = EditorGUIUtility.TrTextContent("Screen Filtering", "Controls how the low-resolution patch irradiance is resolved and upsampled to full screen resolution. These settings affect the final image quality and sharpness of the GI contribution.");
        static GUIContent s_VolumeConfigurationHeader = EditorGUIUtility.TrTextContent("Volume Configuration", "Defines the spatial extent and grid density of the surface cache volume. Size can be changed freely at runtime. Resolution and cascade count changes reallocate internal buffers, which may cause a brief hitch.");
        static GUIContent s_VolumeBehaviorHeader = EditorGUIUtility.TrTextContent("Volume Behavior", "Controls how the volume cascades move relative to the camera during gameplay.");
        static GUIContent s_DefragCount = EditorGUIUtility.TrTextContent("Defrag Count", "Number of surface cache patches to defragment per frame. Higher values reduce memory fragmentation at a small per-frame cost.");

        public override void OnEnable()
        {
            var o = new PropertyFetcher<SurfaceCacheGIVolumeOverride>(serializedObject);

            m_Quality = Unpack(o.Find(x => x.quality));
            m_MultiBounce = Unpack(o.Find("m_MultiBounce"));
            m_BouncePatchAllocation = Unpack(o.Find("m_BouncePatchAllocation"));
            m_SampleCount = Unpack(o.Find("m_SampleCount"));
            m_TemporalSmoothing = Unpack(o.Find("m_TemporalSmoothing"));
            m_SpatialFilterEnabled = Unpack(o.Find("m_SpatialFilterEnabled"));
            m_SpatialSampleCount = Unpack(o.Find("m_SpatialSampleCount"));
            m_SpatialRadius = Unpack(o.Find("m_SpatialRadius"));
            m_TemporalPostFilter = Unpack(o.Find("m_TemporalPostFilter"));
            m_LookupSampleCount = Unpack(o.Find("m_LookupSampleCount"));
            m_UpsamplingKernelSize = Unpack(o.Find("m_UpsamplingKernelSize"));
            m_UpsamplingSampleCount = Unpack(o.Find("m_UpsamplingSampleCount"));
            m_VolumeSize = Unpack(o.Find("m_VolumeSize"));
            m_VolumeResolution = Unpack(o.Find("m_VolumeResolution"));
            m_VolumeCascadeCount = Unpack(o.Find("m_VolumeCascadeCount"));
            m_CascadeMovement = Unpack(o.Find("m_CascadeMovement"));
            m_DefragCount = Unpack(o.Find("m_DefragCount"));
        }

        public override void OnInspectorGUI()
        {
            // Quality preset dropdown. Switching to a named preset writes its values into the
            // backing fields so users can see the preset values and make fine adjustments from there.
            EditorGUI.BeginChangeCheck();
            PropertyField(m_Quality, s_Quality);
            if (EditorGUI.EndChangeCheck())
            {
                var quality = (SurfaceCacheGIQuality)m_Quality.value.enumValueIndex;
                if (quality != SurfaceCacheGIQuality.Custom)
                    ApplyQualityPreset(quality);
            }

            // Light Transport section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_LightTransportHeader, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            PropertyField(m_MultiBounce, s_MultiBounce);
            PropertyField(m_BouncePatchAllocation, s_BouncePatchAllocation);
            PropertyField(m_SampleCount, s_SampleCount);
            if (EditorGUI.EndChangeCheck())
                SwitchToCustomIfNeeded();

            // Patch Filtering section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_PatchFilteringHeader, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            PropertyField(m_TemporalSmoothing, s_TemporalSmoothing);
            PropertyField(m_SpatialFilterEnabled, s_SpatialFilterEnabled);
            using (new IndentLevelScope())
            {
                PropertyField(m_SpatialSampleCount, s_SpatialSampleCount);
                PropertyField(m_SpatialRadius, s_SpatialRadius);
            }
            PropertyField(m_TemporalPostFilter, s_TemporalPostFilter);
            if (EditorGUI.EndChangeCheck())
                SwitchToCustomIfNeeded();

            // Screen Filtering section
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_ScreenFilteringHeader, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            PropertyField(m_LookupSampleCount, s_LookupSampleCount);
            PropertyField(m_UpsamplingKernelSize, s_UpsamplingKernelSize);
            PropertyField(m_UpsamplingSampleCount, s_UpsamplingSampleCount);
            if (EditorGUI.EndChangeCheck())
                SwitchToCustomIfNeeded();

            // Volume Configuration section (always editable, not affected by quality preset)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_VolumeConfigurationHeader, EditorStyles.boldLabel);
            PropertyField(m_VolumeSize, s_VolumeSize);
            PropertyField(m_VolumeResolution, s_VolumeResolution);
            PropertyField(m_VolumeCascadeCount, s_VolumeCascadeCount);

            // Volume Behavior (always editable, not affected by quality preset)
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(s_VolumeBehaviorHeader, EditorStyles.boldLabel);
            PropertyField(m_CascadeMovement, s_CascadeMovement);

            if (showAdditionalProperties)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
                PropertyField(m_DefragCount, s_DefragCount);
            }
        }

        // Switches quality to Custom when the user edits a preset-controlled field while a preset is active.
        void SwitchToCustomIfNeeded()
        {
            if ((SurfaceCacheGIQuality)m_Quality.value.enumValueIndex != SurfaceCacheGIQuality.Custom)
            {
                m_Quality.value.enumValueIndex = (int)SurfaceCacheGIQuality.Custom;
                m_Quality.overrideState.boolValue = true;
            }
        }

        // Writes preset values into the backing serialized fields so users can inspect them
        // and use them as a starting point when switching to Custom.
        void ApplyQualityPreset(SurfaceCacheGIQuality quality)
        {
            var settings = k_QualityPresets[(int)quality];

            m_MultiBounce.value.boolValue = settings.lightTransport.multiBounce;
            m_MultiBounce.overrideState.boolValue = true;
            m_BouncePatchAllocation.value.boolValue = settings.lightTransport.bouncePatchAllocation;
            m_BouncePatchAllocation.overrideState.boolValue = true;
            m_SampleCount.value.intValue = settings.lightTransport.sampleCount;
            m_SampleCount.overrideState.boolValue = true;
            m_TemporalSmoothing.value.floatValue = settings.patchFiltering.temporalSmoothing;
            m_TemporalSmoothing.overrideState.boolValue = true;
            m_SpatialFilterEnabled.value.boolValue = settings.patchFiltering.spatialFilterEnabled;
            m_SpatialFilterEnabled.overrideState.boolValue = true;
            m_SpatialSampleCount.value.intValue = settings.patchFiltering.spatialSampleCount;
            m_SpatialSampleCount.overrideState.boolValue = true;
            m_SpatialRadius.value.floatValue = settings.patchFiltering.spatialRadius;
            m_SpatialRadius.overrideState.boolValue = true;
            m_TemporalPostFilter.value.boolValue = settings.patchFiltering.temporalPostFilter;
            m_TemporalPostFilter.overrideState.boolValue = true;
            m_LookupSampleCount.value.intValue = settings.screenFiltering.lookupSampleCount;
            m_LookupSampleCount.overrideState.boolValue = true;
            m_UpsamplingKernelSize.value.floatValue = settings.screenFiltering.upsamplingKernelSize;
            m_UpsamplingKernelSize.overrideState.boolValue = true;
            m_UpsamplingSampleCount.value.intValue = settings.screenFiltering.upsamplingSampleCount;
            m_UpsamplingSampleCount.overrideState.boolValue = true;
        }
    }
}

#endif
