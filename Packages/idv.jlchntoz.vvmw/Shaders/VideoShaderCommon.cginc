// Configurations
// 2D: stereoShift = float4(0, 0, 0, 0), stereoExtend = float2(1, 1)
// SBS (LR): stereoShift = float4(0, 0, 0.5, 0), stereoExtend = float2(0.5, 1)
// SBS (RL): stereoShift = float4(0.5, 0, 0, 0), stereoExtend = float2(0.5, 1)
// Over-Under (Left above): stereoShift = float4(0, 0.5, 0, 0), stereoExtend = float2(1, 0.5)
// Over-Under (Right above): stereoShift = float4(0, 0, 0, 0.5), stereoExtend = float2(1, 0.5)
// Size Mode: 0 = Stratch, 1 = Contain, 2 = Cover

#ifndef VIDEO_SHADER_COMMON_INCLUDED
#define VIDEO_SHADER_COMMON_INCLUDED

#include "UnityCG.cginc"

inline half4 readVideoTexture(sampler2D videoTex, float2 uv) {
    return tex2Dlod(videoTex, float4(uv, 0, 0));
}

inline half4 readAVProTexture(sampler2D videoTex, float2 uv) {
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif
    half4 c = readVideoTexture(videoTex, uv);
    #if !UNITY_COLORSPACE_GAMMA
        c.rgb = GammaToLinearSpace(c.rgb);
    #endif
    return c;
}

inline float estimateAspectRatio(float3 p1, float2 uv1, float3 p2, float2 uv2, float3 p3, float2 uv3) {
    float3 deltaPos1 = p2 - p1;
    float3 deltaPos2 = p3 - p1;
    float2 deltaUV1 = uv2 - uv1;
    float2 deltaUV2 = uv3 - uv1;
    float det = deltaUV1.x * deltaUV2.y - deltaUV1.y * deltaUV2.x;
    float aspect = 1;
    UNITY_BRANCH if (abs(det) > 1e-6) {
        float invDet = 1 / det;
        float2x2 invUV = invDet * float2x2(
            deltaUV2.y, -deltaUV2.x,
            -deltaUV1.y, deltaUV1.x
        );
        float3 dPosDU = deltaPos1 * invUV._11 + deltaPos2 * invUV._12;
        float3 dPosDV = deltaPos1 * invUV._21 + deltaPos2 * invUV._22;
        float sqrW = dot(dPosDU, dPosDU), invW = 0;
        float3 tangent = float3(1, 0, 0);
        UNITY_BRANCH if (sqrW > 1e-6) {
            invW = rsqrt(sqrW);
            tangent = dPosDU * invW;
        }
        aspect = length(dPosDV - dot(dPosDV, tangent) * tangent) * invW;
        float2 deltaUV = max(max(uv1, uv2), uv3) - min(min(uv1, uv2), uv3);
        if (deltaUV.x > 1e-6) aspect *= deltaUV.y / deltaUV.x;
    }
    return 1 / max(aspect, 1e-6);
}

float2 getUnstratchedUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio, float2 stereoExtend) {
    float srcAspectRatio = texelSize.y * texelSize.z * stereoExtend.x / stereoExtend.y;
    UNITY_BRANCH if (abs(srcAspectRatio - aspectRatio) > 0.001) {
        float2 scale = float2(aspectRatio / srcAspectRatio, srcAspectRatio / aspectRatio);
        float4 scale2 = 1;
        if (srcAspectRatio > aspectRatio)
            scale2.zy = scale;
        else
            scale2.xw = scale;
        float4 uv2 = (uv.xyxy - 0.5) * scale2 + 0.5;
        switch (sizeMode) {
            case 1: uv = uv2.xy; break;
            case 2: uv = uv2.zw; break;
        }
    }
    return uv;
}

float2 getUnstratchedUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio) {
    return getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, float2(1, 1));
}

float2 getStereoUV(float2 uv, float4 stereoShift, float2 stereoExtend) {
    return uv * stereoExtend + lerp(stereoShift.xy, stereoShift.zw, unity_StereoEyeIndex);
}

half4 getVideoTexture(sampler2D videoTex, float2 uv, bool avPro, float4 stereoShift, float2 stereoExtend) {
    #ifdef _STEREO_DEBUG
        half4 leftTexture = half4(1, 0.5, 0, 0.5), rightTexture = half4(0, 0.5, 1, 0.5);
        uv *= stereoExtend;
        stereoShift += uv.xyxy;
        if (avPro) {
            leftTexture *= readAVProTexture(videoTex, stereoShift.xy);
            rightTexture *= readAVProTexture(videoTex, stereoShift.zw);
        } else {
            leftTexture *= readVideoTexture(videoTex, stereoShift.xy);
            rightTexture *= readVideoTexture(videoTex, stereoShift.zw);
        }
        return leftTexture + rightTexture;
    #else
        uv = getStereoUV(uv, stereoShift, stereoExtend);
        return avPro ? readAVProTexture(videoTex, uv) : readVideoTexture(videoTex, uv);
    #endif
}

half4 getVideoTexture(sampler2D videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio, float4 stereoShift, float2 stereoExtend, bool halfWidth) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, halfWidth ? float2(1, 1) : stereoExtend);
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTexture(videoTex, uv, avPro, stereoShift, stereoExtend);
}

half4 getVideoTexture(sampler2D videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio, float4 stereoShift, float2 stereoExtend) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, stereoExtend);
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTexture(videoTex, uv, avPro, stereoShift, stereoExtend);
}

half4 getVideoTexture(sampler2D videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio) {
    return getVideoTexture(videoTex, uv, texelSize, avPro, sizeMode, aspectRatio, 0, 1);
}

inline float2 vert_getVideoUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio, float3 stereoExtendAndHalfWidth) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, stereoExtendAndHalfWidth.z > 0.0001 ? float2(1, 1) : stereoExtendAndHalfWidth.xy);
    return uv;
}

inline float2 vert_getVideoUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio, float4 stereoShift, float3 stereoExtendAndHalfWidth) {
    return vert_getVideoUV(uv, texelSize, sizeMode, aspectRatio, stereoExtendAndHalfWidth);
}

inline half4 frag_getVideoTexture(sampler2D videoTex, float2 uv, bool avPro, float4 stereoShift, float3 stereoExtend) {
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTexture(videoTex, uv, avPro, stereoShift, stereoExtend.xy);
}
#endif
