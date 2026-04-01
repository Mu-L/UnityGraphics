#ifndef __VFX_SPAWNER_DATA
#define __VFX_SPAWNER_DATA

#include "Packages/com.unity.visualeffectgraph/Shaders/Temp/Data/StructuredBufferUint.hlsl"

struct SpawnerData
{
    VFXStructuredBuffer_uint instancingPrefixSum;
    void Init(VFXStructuredBuffer_uint _instancingPrefixSum)
    {
        instancingPrefixSum = _instancingPrefixSum;
    }
};

#endif //__VFX_SPAWNER_DATA
