#ifndef __VFX_THREAD_DATA
#define __VFX_THREAD_DATA

struct ThreadData
{
    uint index;

    void Init(uint index)
    {
        this.index = index;
    }
};

#endif //__VFX_THREAD_DATA
