using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;

using RenderQueueType = UnityEngine.Rendering.HighDefinition.HDRenderQueue.RenderQueueType;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    class SystemData : HDTargetData
    {
        [SerializeField]
        int m_MaterialNeedsUpdateHash;
        public int materialNeedsUpdateHash
        {
            get => m_MaterialNeedsUpdateHash;
            set => m_MaterialNeedsUpdateHash = value;
        }

        [SerializeField]
        SurfaceType m_SurfaceType = SurfaceType.Opaque;
        public SurfaceType surfaceType
        {
            get => m_SurfaceType;
            set => m_SurfaceType = value;
        }

        [SerializeField]
        RenderQueueType m_RenderingPass = RenderQueueType.Opaque;
        public RenderQueueType renderQueueType
        {
            get => m_RenderingPass;
            set => m_RenderingPass = value;
        }

        [SerializeField]
        BlendingMode m_BlendMode = BlendingMode.Alpha;
        public BlendingMode blendingMode
        {
            get => m_BlendMode;
            set => m_BlendMode = value;
        }

        [SerializeField]
        CompareFunction m_ZTest = CompareFunction.LessEqual;
        public CompareFunction zTest
        {
            get => m_ZTest;
            set => m_ZTest = value;
        }

        [SerializeField]
        bool m_ZWrite = false;
        public bool transparentZWrite
        {
            get => m_ZWrite;
            set => m_ZWrite = value;
        }

        [SerializeField]
        TransparentCullMode m_TransparentCullMode = TransparentCullMode.Back;
        public TransparentCullMode transparentCullMode
        {
            get => m_TransparentCullMode;
            set => m_TransparentCullMode = value;
        }

        [SerializeField]
        OpaqueCullMode m_OpaqueCullMode = OpaqueCullMode.Back;
        public OpaqueCullMode opaqueCullMode
        {
            get => m_OpaqueCullMode;
            set => m_OpaqueCullMode = value;
        }

        [SerializeField]
        int m_SortPriority;
        public int sortPriority
        {
            get => m_SortPriority;
            set => m_SortPriority = value;
        }

        [SerializeField]
        bool m_AlphaTest;
        public bool alphaTest
        {
            get => m_AlphaTest;
            set => m_AlphaTest = value;
        }

        [SerializeField]
        bool m_ExcludeFromTUAndAA = false;
        public bool excludeFromTUAndAA
        {
            get => m_ExcludeFromTUAndAA;
            set => m_ExcludeFromTUAndAA = value;
        }

        [SerializeField, Obsolete("Keep for migration. #from(2021.1)")]
        internal bool m_TransparentDepthPrepass;

        [SerializeField, Obsolete("Keep for migration. #from(2021.1)")]
        internal bool m_TransparentDepthPostpass;

        [SerializeField, Obsolete("Keep for migration. #from(2021.1)")]
        internal bool m_SupportLodCrossFade;

        [SerializeField]
        DoubleSidedMode m_DoubleSidedMode;
        public DoubleSidedMode doubleSidedMode
        {
            get => m_DoubleSidedMode;
            set => m_DoubleSidedMode = value;
        }

        // TODO: This was on HDUnlitMaster but not used anywhere
        // TODO: On HDLit it adds the field `HDFields.DotsInstancing`
        // TODO: Should this be added properly to HDUnlit?
        [SerializeField]
        bool m_DOTSInstancing = false;
        public bool dotsInstancing
        {
            get => m_DOTSInstancing;
            set => m_DOTSInstancing = value;
        }

        // Custom motion vector
        [SerializeField]
        bool m_CustomVelocity = false;
        public bool customVelocity
        {
            get => m_CustomVelocity;
            set => m_CustomVelocity = value;
        }


        // Tessellation properties
        [SerializeField]
        bool m_Tessellation;
        public bool tessellation
        {
            get => m_Tessellation;
            set => m_Tessellation = value;
        }

        [SerializeField]
        TessellationMode m_TessellationMode;
        public TessellationMode tessellationMode
        {
            get => m_TessellationMode;
            set => m_TessellationMode = value;
        }

        [SerializeField]
        float m_TessellationFactorMinDistance = 20.0f;
        public float tessellationFactorMinDistance
        {
            get => m_TessellationFactorMinDistance;
            set => m_TessellationFactorMinDistance = value;
        }

        [SerializeField]
        float m_TessellationFactorMaxDistance = 50.0f;
        public float tessellationFactorMaxDistance
        {
            get => m_TessellationFactorMaxDistance;
            set => m_TessellationFactorMaxDistance = value;
        }

        [SerializeField]
        float m_TessellationFactorTriangleSize = 100.0f;
        public float tessellationFactorTriangleSize
        {
            get => m_TessellationFactorTriangleSize;
            set => m_TessellationFactorTriangleSize = value;
        }

        [SerializeField]
        float m_TessellationShapeFactor = 0.75f;
        public float tessellationShapeFactor
        {
            get => m_TessellationShapeFactor;
            set => m_TessellationShapeFactor = value;
        }

        [SerializeField]
        float m_TessellationBackFaceCullEpsilon = -0.25f;
        public float tessellationBackFaceCullEpsilon
        {
            get => m_TessellationBackFaceCullEpsilon;
            set => m_TessellationBackFaceCullEpsilon = value;
        }

        [SerializeField]
        float m_TessellationMaxDisplacement = 0.01f;
        public float tessellationMaxDisplacement
        {
            get => m_TessellationMaxDisplacement;
            set => m_TessellationMaxDisplacement = value;
        }

        // End Tessellation

        [SerializeField]
        bool m_DebugSymbols = false;
        public bool debugSymbols
        {
            get => m_DebugSymbols;
            set => m_DebugSymbols = value;
        }

        [SerializeField]
        ShaderGraphVersion m_Version = MigrationDescription.LastVersion<ShaderGraphVersion>();
        public ShaderGraphVersion version
        {
            get => m_Version;
            set => m_Version = value;
        }

        [SerializeField]
        internal int inspectorFoldoutMask;

        public override void OnBeforeDeserialize()
        {
            // If we have created a target and its objects but intend to deserialize into it, then we overwrite the default
            // version initializer (which is set equal to the latest version) to the never migrated version, such that we can
            // effectively detect a shadergraph that never had that version field in the SystemData data chunk while also
            // defaulting new graphs to the latest version.
            // The master node migration (see *SubTarget.Migration.cs files) doesn't do that (forcing version to never migrated)
            // for now, as we assume that the code there will handle the full conversion to the latest version.
            base.OnBeforeDeserialize();
            m_Version = ShaderGraphVersion.NeverMigrated;
        }
    }

    static class HDSystemDataExtensions
    {
        public static bool TryChangeRenderingPass(this SystemData systemData, HDRenderQueue.RenderQueueType value)
        {
            // Catch invalid rendering pass
            switch (value)
            {
                case HDRenderQueue.RenderQueueType.Overlay:
                case HDRenderQueue.RenderQueueType.Unknown:
                case HDRenderQueue.RenderQueueType.Background:
                    throw new ArgumentException("Unexpected kind of RenderQueue, was " + value);
            }
            ;

            // Update for SurfaceType
            switch (systemData.surfaceType)
            {
                case SurfaceType.Opaque:
                    value = HDRenderQueue.GetOpaqueEquivalent(value);
                    break;
                case SurfaceType.Transparent:
                    value = HDRenderQueue.GetTransparentEquivalent(value);
                    break;
                default:
                    throw new ArgumentException("Unknown SurfaceType");
            }

            if (Equals(systemData.renderQueueType, value))
                return false;

            systemData.renderQueueType = value;
            return true;
        }
    }
}
