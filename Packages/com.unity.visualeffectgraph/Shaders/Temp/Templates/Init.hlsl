
//VFX_DECLARE_BINDING(ParticleData, ParticleDataBinding);
//VFX_DECLARE_BINDING(SpawnerData, SpawnDataBinding);

//VFX_MAP_ATTRIBUTES(Attributes, ParticleDataBinding.particleAttributeBuffer);
//VFX_MAP_ATTRIBUTES(SourceAttributes, SpawnDataBinding.attributes);
void main(ThreadData threadData)
{
    Attributes particleAttributes;
    particleAttributes.Init();

    uint maxSpawnCount;
    SpawnDataBinding.spawner.instancingPrefixSum.LoadData(maxSpawnCount, 0);

    if(threadData.index >= maxSpawnCount)
    {
        return;
    }

    uint systemSeed = ContextData.systemSeed;
    uint initSpawnIndex = ContextData.initSpawnIndex;

    particleAttributes.particleId = initSpawnIndex + threadData.index;
    particleAttributes.seed = WangHash(particleAttributes.particleId ^ systemSeed);

    VFXProcessBlocks(particleAttributes);

    if (particleAttributes.alive)
    {
        uint particleIndex;
        if (ParticleDataBinding.NewParticle(threadData.index, particleIndex))
        {
            ParticleDataBinding.particleAttributeBuffer.StoreData(particleAttributes, particleIndex);
        }
    }
}
