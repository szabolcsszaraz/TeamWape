#ifndef SURFACE_CACHE_PUNCTUAL_LIGHT_SAMPLE
#define SURFACE_CACHE_PUNCTUAL_LIGHT_SAMPLE

struct PunctualLight
{
    float3 position;
    float3 direction;
    float3 intensity;
    float cosOuterAngle;
    float range;
    // If innerAngle != outerAngle then 1/(cosInnerAngle-cosOuterAngle), otherwise 0.
    float angleAttenuationValue1;
    // If innerAngle != outerAngle then cosOuterAngle/(cosOuterAngle-cosInnerAngle), otherwise 1.
    float angleAttenuationValue2;
};

// This represents a sample ray shot from the light.
struct PunctualLightSample
{
    float3 hitPos;
    float3 hitNormal;
    float3 hitAlbedo;
    float3 rayDirection;
    float distance;
    uint hitInstanceId;
    uint hitPrimitiveIndex;
    uint lightIndex;
    float reciprocalDensity;

    void MarkNoHit()
    {
        hitAlbedo = -1.0f;
    }

    bool HasHit()
    {
        return all(hitAlbedo != -1.0f);
    }
};

#endif
