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
            if (writeAccess)
            {
                data[0] = asfloat(bufferRW.Load4((offset + index + 0) << 2));
                data[1] = asfloat(bufferRW.Load4((offset + index + 4) << 2));
                data[2] = asfloat(bufferRW.Load4((offset + index + 8) << 2));
                data[3] = asfloat(bufferRW.Load4((offset + index + 12) << 2));
            }
            else
            {
                data[0] = asfloat(buffer.Load4((offset + index + 0) << 2));
                data[1] = asfloat(buffer.Load4((offset + index + 4) << 2));
                data[2] = asfloat(buffer.Load4((offset + index + 8) << 2));
                data[3] = asfloat(buffer.Load4((offset + index + 12) << 2));
            }
        }
        return valid;
    }

    bool StoreData(float4x4 data, uint index)
    {
        bool valid = !rangeCheck || index + 15 < size;
        if (valid && writeAccess)
        {
            for (int i = 0; i < 4; ++i)
            {
                bufferRW.Store4((offset + index + 4 * i) << 2, data[i]);
            }
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
