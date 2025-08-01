using System;
using UnityEngine.Serialization;

namespace UnityEngine.Rendering.HighDefinition
{
    partial class ProxyVolume : IVersionable<ProxyVolume.Version>, ISerializationCallbackReceiver
    {
        enum Version
        {
            Initial,
            InfiniteProjectionInShape,
            ForcePositiveSize,
        }

        static readonly MigrationDescription<Version, ProxyVolume> k_Migration = MigrationDescription.New(
            MigrationStep.New(Version.InfiniteProjectionInShape, (ProxyVolume p) =>
            {
#pragma warning disable CS0618
                if (p.shape == ProxyShape.Sphere && p.m_ObsoleteSphereInfiniteProjection
                    || p.shape == ProxyShape.Box && p.m_ObsoleteBoxInfiniteProjection)
#pragma warning restore CS0618
                {
                    p.shape = ProxyShape.Infinite;
                }
            }),
            MigrationStep.New(Version.ForcePositiveSize, (ProxyVolume p) =>
            {
                p.sphereRadius = Mathf.Abs(p.sphereRadius);
                p.boxSize = new Vector3(Mathf.Abs(p.boxSize.x), Mathf.Abs(p.boxSize.y), Mathf.Abs(p.boxSize.z));
            })
        );

        [SerializeField]
        Version m_CSVersion = MigrationDescription.LastVersion<Version>();
        Version IVersionable<Version>.version { get => m_CSVersion; set => m_CSVersion = value; }

        // Obsolete fields
        [SerializeField, FormerlySerializedAs("m_SphereInfiniteProjection"), Obsolete("For data migration. #from(2021.1)")]
        bool m_ObsoleteSphereInfiniteProjection = false;
        [SerializeField, FormerlySerializedAs("m_BoxInfiniteProjection"), Obsolete("Kept only for compatibility. Use m_Shape instead. #from(2021.1)")]
        bool m_ObsoleteBoxInfiniteProjection = false;

        /// <summary>Serialization callback</summary>
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        /// <summary>Serialization callback</summary>
        void ISerializationCallbackReceiver.OnAfterDeserialize() => k_Migration.Migrate(this);
    }
}
