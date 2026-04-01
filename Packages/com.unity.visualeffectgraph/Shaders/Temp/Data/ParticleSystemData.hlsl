#ifndef __VFX_PARTICLE_SYSTEM_DATA
#define __VFX_PARTICLE_SYSTEM_DATA

#include "Packages/com.unity.visualeffectgraph/Shaders/Temp/Data/AttributeBuffer.hlsl"

struct VFXParticleSystemData
{
    uint capacity;
    //VFXSpace space;
    //VFXBounds bounds;

    void Init(uint capacity)
    {
        this.capacity = capacity;
    }
};

#endif //__VFX_PARTICLE_SYSTEM_DATA
