#if SURFACE_CACHE

using System;

namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Quality presets for Surface Cache Global Illumination.
    /// </summary>
    public enum SurfaceCacheGIQuality
    {
        /// <summary>
        /// Low quality preset - minimal samples, maximum performance.
        /// </summary>
        Low,

        /// <summary>
        /// Medium quality preset - balanced quality and performance (default).
        /// </summary>
        Medium,

        /// <summary>
        /// High quality preset - higher quality with increased cost.
        /// </summary>
        High,

        /// <summary>
        /// Ultra quality preset - maximum quality, highest cost.
        /// </summary>
        Ultra,

        /// <summary>
        /// Custom quality - user-defined parameters.
        /// </summary>
        Custom
    }

    /// <summary>
    /// A volume component that holds settings for the Surface Cache Global Illumination feature.
    /// </summary>
    [Serializable, VolumeComponentMenu("Lighting/Surface Cache Global Illumination")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class SurfaceCacheGIVolumeOverride : VolumeComponent
    {
        /// <summary>
        /// Quality preset for Surface Cache GI. Selecting a preset automatically configures all quality-related parameters.
        /// Select Custom to manually adjust individual parameters.
        /// </summary>
        [Tooltip("Quality preset for Surface Cache GI. Select Custom to manually adjust individual parameters.")]
        public SurfaceCacheGIQualityVolumeParameter quality = new SurfaceCacheGIQualityVolumeParameter(SurfaceCacheGIQuality.Medium);

        // ====================
        // Sampling Parameters
        // ====================

        /// <summary>
        /// Enable multi-bounce global illumination.
        /// </summary>
        [Tooltip("Enable multi-bounce global illumination for more accurate light propagation.")]
        public bool multiBounce
        {
            get => m_MultiBounce.value;
            set => m_MultiBounce.value = value;
        }
        [SerializeField]
        private BoolParameter m_MultiBounce = new BoolParameter(true);

        /// <summary>
        /// Number of samples used for GI estimation. Higher values improve quality at performance cost.
        /// </summary>
        public int sampleCount
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_SampleCount.value;
                else
                    return GetPresetSampleCount(quality.value);
            }
            set => m_SampleCount.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_SampleCount = new ClampedIntParameter(2, 1, 32);

        // ============================
        // Patch Filtering Parameters
        // ============================

        /// <summary>
        /// Temporal smoothing factor for patch data. Higher values produce more stable results but slower response to lighting changes.
        /// </summary>
        public float temporalSmoothing
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_TemporalSmoothing.value;
                else
                    return GetPresetTemporalSmoothing(quality.value);
            }
            set => m_TemporalSmoothing.value = value;
        }
        [SerializeField]
        private ClampedFloatParameter m_TemporalSmoothing = new ClampedFloatParameter(0.8f, 0.0f, 1.0f);

        /// <summary>
        /// Enable spatial filtering for patch data.
        /// </summary>
        public bool spatialFilterEnabled
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_SpatialFilterEnabled.value;
                else
                    return GetPresetSpatialFilterEnabled(quality.value);
            }
            set => m_SpatialFilterEnabled.value = value;
        }
        [SerializeField]
        private BoolParameter m_SpatialFilterEnabled = new BoolParameter(true);

        /// <summary>
        /// Number of samples for spatial filtering. Higher values improve quality at performance cost.
        /// </summary>
        public int spatialSampleCount
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_SpatialSampleCount.value;
                else
                    return GetPresetSpatialSampleCount(quality.value);
            }
            set => m_SpatialSampleCount.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_SpatialSampleCount = new ClampedIntParameter(4, 1, 8);

        /// <summary>
        /// Radius for spatial filtering in world units.
        /// </summary>
        public float spatialRadius
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_SpatialRadius.value;
                else
                    return GetPresetSpatialRadius(quality.value);
            }
            set => m_SpatialRadius.value = value;
        }
        [SerializeField]
        private ClampedFloatParameter m_SpatialRadius = new ClampedFloatParameter(1.0f, 0.1f, 4.0f);

        /// <summary>
        /// Enable temporal post-filtering for additional image stability.
        /// </summary>
        public bool temporalPostFilter
        {
            get => m_TemporalPostFilter.value;
            set => m_TemporalPostFilter.value = value;
        }
        [SerializeField]
        private BoolParameter m_TemporalPostFilter = new BoolParameter(true);

        // ============================
        // Screen Filtering Parameters
        // ============================

        /// <summary>
        /// Number of samples for screen-space lookups.
        /// </summary>
        public int lookupSampleCount
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_LookupSampleCount.value;
                else
                    return GetPresetLookupSampleCount(quality.value);
            }
            set => m_LookupSampleCount.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_LookupSampleCount = new ClampedIntParameter(6, 0, 8);

        /// <summary>
        /// Kernel size for upsampling filtering.
        /// </summary>
        public float upsamplingKernelSize
        {
            get => m_UpsamplingKernelSize.value;
            set => m_UpsamplingKernelSize.value = value;
        }
        [SerializeField]
        private ClampedFloatParameter m_UpsamplingKernelSize = new ClampedFloatParameter(5.0f, 0.0f, 8.0f);

        /// <summary>
        /// Number of samples for upsampling. Higher values improve quality at performance cost.
        /// </summary>
        public int upsamplingSampleCount
        {
            get
            {
                if (quality.value == SurfaceCacheGIQuality.Custom)
                    return m_UpsamplingSampleCount.value;
                else
                    return GetPresetUpsamplingSampleCount(quality.value);
            }
            set => m_UpsamplingSampleCount.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_UpsamplingSampleCount = new ClampedIntParameter(2, 1, 16);

        // =======================
        // Volume Configuration
        // =======================

        /// <summary>
        /// Size of the surface cache volume in world units.
        /// This can be changed per-scene without causing a performance hitch.
        /// </summary>
        public float volumeSize
        {
            get => m_VolumeSize.value;
            set => m_VolumeSize.value = value;
        }
        [SerializeField]
        private MinFloatParameter m_VolumeSize = new MinFloatParameter(128.0f, 1.0f);

        /// <summary>
        /// Spatial resolution of the surface cache volume. Higher values improve spatial detail but use more memory.
        /// Changing this at runtime can cause a performance hitch as internal buffers are reallocated (BVH is preserved).
        /// </summary>
        public int volumeResolution
        {
            get => m_VolumeResolution.value;
            set => m_VolumeResolution.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_VolumeResolution = new ClampedIntParameter(32, 16, 128);

        /// <summary>
        /// Number of cascades for the surface cache volume. More cascades extend the volume's reach.
        /// Changing this at runtime can cause a performance hitch as internal buffers are reallocated (BVH is preserved).
        /// </summary>
        public int volumeCascadeCount
        {
            get => m_VolumeCascadeCount.value;
            set => m_VolumeCascadeCount.value = value;
        }
        [SerializeField]
        private ClampedIntParameter m_VolumeCascadeCount = new ClampedIntParameter(4, 1, 8);

        // =======================
        // Volume Behavior
        // =======================

        /// <summary>
        /// Enable volume cascades to follow the camera.
        /// </summary>
        public bool cascadeMovement
        {
            get => m_CascadeMovement.value;
            set => m_CascadeMovement.value = value;
        }
        [SerializeField]
        private BoolParameter m_CascadeMovement = new BoolParameter(true);

        // =======================
        // Advanced Properties
        // =======================

        /// <summary>
        /// Number of surface cache patches to defragment per frame.
        /// </summary>
        public int defragCount
        {
            get => m_DefragCount.value;
            set => m_DefragCount.value = value;
        }
        [AdditionalProperty]
        [SerializeField]
        private ClampedIntParameter m_DefragCount = new ClampedIntParameter(2, 1, 32);

        // =======================
        // Preset Lookup Methods
        // =======================

        private static int GetPresetSampleCount(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 1,
                SurfaceCacheGIQuality.Medium => 2,
                SurfaceCacheGIQuality.High => 4,
                SurfaceCacheGIQuality.Ultra => 8,
                _ => 2
            };
        }

        private static float GetPresetTemporalSmoothing(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 0.9f,
                SurfaceCacheGIQuality.Medium => 0.8f,
                SurfaceCacheGIQuality.High => 0.7f,
                SurfaceCacheGIQuality.Ultra => 0.6f,
                _ => 0.8f
            };
        }

        private static bool GetPresetSpatialFilterEnabled(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => false,
                SurfaceCacheGIQuality.Medium => true,
                SurfaceCacheGIQuality.High => true,
                SurfaceCacheGIQuality.Ultra => true,
                _ => true
            };
        }

        private static int GetPresetSpatialSampleCount(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 4,
                SurfaceCacheGIQuality.Medium => 4,
                SurfaceCacheGIQuality.High => 6,
                SurfaceCacheGIQuality.Ultra => 8,
                _ => 4
            };
        }

        private static float GetPresetSpatialRadius(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 1.0f,
                SurfaceCacheGIQuality.Medium => 1.0f,
                SurfaceCacheGIQuality.High => 1.5f,
                SurfaceCacheGIQuality.Ultra => 2.0f,
                _ => 1.0f
            };
        }

        private static int GetPresetLookupSampleCount(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 4,
                SurfaceCacheGIQuality.Medium => 6,
                SurfaceCacheGIQuality.High => 8,
                SurfaceCacheGIQuality.Ultra => 8,
                _ => 6
            };
        }

        private static int GetPresetUpsamplingSampleCount(SurfaceCacheGIQuality quality)
        {
            return quality switch
            {
                SurfaceCacheGIQuality.Low => 1,
                SurfaceCacheGIQuality.Medium => 2,
                SurfaceCacheGIQuality.High => 4,
                SurfaceCacheGIQuality.Ultra => 8,
                _ => 2
            };
        }

        /// <summary>
        /// Query if the effect is active and should be rendered.
        /// </summary>
        /// <returns><c>true</c> if the effect should be rendered, <c>false</c> otherwise.</returns>
        public bool IsActive()
        {
            return true;
        }
    }

    /// <summary>
    /// A <see cref="VolumeParameter"/> that holds a <see cref="SurfaceCacheGIQuality"/> value.
    /// </summary>
    [Serializable]
    public sealed class SurfaceCacheGIQualityVolumeParameter : VolumeParameter<SurfaceCacheGIQuality>
    {
        /// <summary>
        /// Creates a new <see cref="SurfaceCacheGIQualityVolumeParameter"/> instance.
        /// </summary>
        /// <param name="value">The initial value to store in the parameter.</param>
        /// <param name="overrideState">The initial override state for the parameter.</param>
        public SurfaceCacheGIQualityVolumeParameter(SurfaceCacheGIQuality value, bool overrideState = false) : base(value, overrideState) { }
    }
}

#endif
