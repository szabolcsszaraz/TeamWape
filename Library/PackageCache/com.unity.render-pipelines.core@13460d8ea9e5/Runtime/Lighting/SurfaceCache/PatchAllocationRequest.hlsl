#ifndef SURFACE_CACHE_PATCH_ALLOCATION_REQUEST
#define SURFACE_CACHE_PATCH_ALLOCATION_REQUEST

struct PatchAllocationRequest
{
    float3 position;
    float3 normal;
};

static const uint PatchAllocationRequestMax = 1024;

#endif
