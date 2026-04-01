#ifndef __VFX_ATTRIBUTE_BUFFER
#define __VFX_ATTRIBUTE_BUFFER

#include "Packages/com.unity.visualeffectgraph/Shaders/Temp/Data/ByteAddressBuffer.hlsl"

struct VFXAttributeBuffer
{
    VFXByteAddressBuffer buffer;

    void Init(VFXByteAddressBuffer buffer)
    {
        this.buffer = buffer;
    }
};

#define VFX_ATTRIBUTE_DECLARE(type, name, value, offset, stride)\
    type Load_##name(uint index)\
    {\
        type name = value;\
        attributeBuffer.buffer.LoadData(name, offset + index * stride);\
        return name;\
    }\
    void Store_##name(type name, uint index)\
    {\
        attributeBuffer.buffer.StoreData(name, offset + index * stride);\
    }\
    type __##name

#define VFX_ATTRIBUTE_IGNORE(type, name, value)\
    type Load_##name(uint index)\
    {\
        return value;\
    }\
    void Store_##name(type name, uint index)\
    {\
    }\
    type __##name

#endif //__VFX_ATTRIBUTE_BUFFER
