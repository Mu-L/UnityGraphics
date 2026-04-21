#ifndef __VFX_BYTE_ADDRESS_BUFFER
#define __VFX_BYTE_ADDRESS_BUFFER

ByteAddressBuffer __VFXEmptyByteAddressBuffer;
RWByteAddressBuffer __VFXEmptyRWByteAddressBuffer;

struct VFXByteAddressBuffer
{
    ByteAddressBuffer buffer;
    RWByteAddressBuffer bufferRW;
    uint offset;
    uint size;
    bool readAccess;
    bool writeAccess;
    bool rangeCheck;

    void Init(ByteAddressBuffer buffer, uint offset, uint size)
    {
        this.buffer = buffer;
        this.bufferRW = __VFXEmptyRWByteAddressBuffer;
        this.offset = offset;
        this.size = size;
        this.readAccess = true;
        this.writeAccess = false;
        this.rangeCheck = false;
    }

    void Init(RWByteAddressBuffer buffer, uint offset, uint size)
    {
        this.buffer = __VFXEmptyByteAddressBuffer;
        this.bufferRW = buffer;
        this.offset = offset;
        this.size = size;
        this.readAccess = true;
        this.writeAccess = true;
        this.rangeCheck = false;
    }

    bool LoadData(out uint data, uint index)
    {
        bool valid = !rangeCheck || index < size;
        data = 0u;
        if (valid && readAccess)
        {
            if (writeAccess)
            {
                data = bufferRW.Load((offset + index) << 2);
            }
            else
            {
                data = buffer.Load((offset + index) << 2);
            }
        }
        return valid;
    }

    bool StoreData(uint data, uint index)
    {
        bool valid = !rangeCheck || index < size;
        if (valid && writeAccess)
        {
            bufferRW.Store((offset + index) << 2, data);
        }
        return valid;
    }

    bool LoadData(out uint2 data, uint index)
    {
        bool valid = !rangeCheck || index + 1 < size;
        data = 0u;
        if (valid && readAccess)
        {
            if (writeAccess)
            {
                data = bufferRW.Load2((offset + index) << 2);
            }
            else
            {
                data = buffer.Load2((offset + index) << 2);
            }
        }
        return valid;
    }

    bool StoreData(uint2 data, uint index)
    {
        bool valid = !rangeCheck || index + 1 < size;
        if (valid && writeAccess)
        {
            bufferRW.Store2((offset + index) << 2, data);
        }
        return valid;
    }

    bool LoadData(out uint3 data, uint index)
    {
        bool valid = !rangeCheck || index + 2 < size;
        data = 0u;
        if (valid && readAccess)
        {
            if (writeAccess)
            {
                data = bufferRW.Load3((offset + index) << 2);
            }
            else
            {
                data = buffer.Load3((offset + index) << 2);
            }
        }
        return valid;
    }

    bool StoreData(uint3 data, uint index)
    {
        bool valid = !rangeCheck || index + 2 < size;
        if (valid && writeAccess)
        {
            bufferRW.Store3((offset + index) << 2, data);
        }
        return valid;
    }

    bool LoadData(out uint4 data, uint index)
    {
        bool valid = !rangeCheck || index + 3 < size;
        data = 0u;
        if (valid && readAccess)
        {
            if (writeAccess)
            {
                data = bufferRW.Load4((offset + index) << 2);
            }
            else
            {
                data = buffer.Load4((offset + index) << 2);
            }
        }
        return valid;
    }

    bool StoreData(uint4 data, uint index)
    {
        bool valid = !rangeCheck || index + 3 < size;
        if (valid && writeAccess)
        {
            bufferRW.Store4((offset + index) << 2, data);
        }
        return valid;
    }

    bool LoadData(out float data, uint index)
    {
        uint rawData = 0u;
        data = 0.0f;
        bool valid = LoadData(rawData, index);
        if (valid)
        {
            data = asfloat(rawData);
        }
        return valid;
    }

    bool StoreData(float data, uint index)
    {
        uint rawData = asuint(data);
        return StoreData(rawData, index);
    }

    bool LoadData(out float2 data, uint index)
    {
        uint2 rawData = 0u;
        data = 0.0f;
        bool valid = LoadData(rawData, index);
        if (valid)
        {
            data = asfloat(rawData);
        }
        return valid;
    }

    bool StoreData(float2 data, uint index)
    {
        uint2 rawData = asuint(data);
        return StoreData(rawData, index);
    }

    bool LoadData(out float3 data, uint index)
    {
        uint3 rawData = 0u;
        data = 0.0f;
        bool valid = LoadData(rawData, index);
        if (valid)
        {
            data = asfloat(rawData);
        }
        return valid;
    }

    bool StoreData(float3 data, uint index)
    {
        uint3 rawData = asuint(data);
        return StoreData(rawData, index);
    }

    bool LoadData(out float4 data, uint index)
    {
        uint4 rawData = 0u;
        bool valid = LoadData(rawData, index);
        if (valid)
        {
            data = asfloat(rawData);
        }
        return valid;
    }

    bool StoreData(float4 data, uint index)
    {
        uint4 rawData = asuint(data);
        return StoreData(rawData, index);
    }

    bool LoadData(out float4x4 data, uint index)
    {
        bool valid = !rangeCheck || index + 15 < size;
        data = 0u;
        if (valid && readAccess)
        {
            float4 c0, c1, c2, c3;
            if (writeAccess)
            {
                c0 = asfloat(bufferRW.Load4((offset + index + 0) << 2));
                c1 = asfloat(bufferRW.Load4((offset + index + 4) << 2));
                c2 = asfloat(bufferRW.Load4((offset + index + 8) << 2));
                c3 = asfloat(bufferRW.Load4((offset + index + 12) << 2));
            }
            else
            {
                c0 = asfloat(buffer.Load4((offset + index + 0) << 2));
                c1 = asfloat(buffer.Load4((offset + index + 4) << 2));
                c2 = asfloat(buffer.Load4((offset + index + 8) << 2));
                c3 = asfloat(buffer.Load4((offset + index + 12) << 2));
            }
            data = float4x4(
                c0.x, c1.x, c2.x, c3.x,
                c0.y, c1.y, c2.y, c3.y,
                c0.z, c1.z, c2.z, c3.z,
                c0.w, c1.w, c2.w, c3.w
            );
        }
        return valid;
    }

    bool StoreData(float4x4 data, uint index)
    {
        bool valid = !rangeCheck || index + 15 < size;
        if (valid && writeAccess)
        {
            bufferRW.Store4((offset + index + 0) << 2, asuint(float4(data[0].x, data[1].x, data[2].x, data[3].x)));
            bufferRW.Store4((offset + index + 4) << 2, asuint(float4(data[0].y, data[1].y, data[2].y, data[3].y)));
            bufferRW.Store4((offset + index + 8) << 2, asuint(float4(data[0].z, data[1].z, data[2].z, data[3].z)));
            bufferRW.Store4((offset + index + 12) << 2, asuint(float4(data[0].w, data[1].w, data[2].w, data[3].w)));
        }
        return valid;
    }

    bool LoadData(out bool data, uint index)
    {
        uint rawData = 0u;
        data = false;
        bool valid = LoadData(rawData, index);
        if (valid)
        {
            data = rawData != 0;
        }
        return valid;
    }

    bool StoreData(bool data, uint index)
    {
        uint rawData = data ? 1u : 0u;
        return StoreData(rawData, index);
    }
};

#endif //__VFX_BYTE_ADDRESS_BUFFER
