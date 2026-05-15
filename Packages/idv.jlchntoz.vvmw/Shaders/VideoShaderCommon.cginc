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
    float4 duv = float4(uv2, uv3) - uv1.xyxy;
    float3x3 r = mul(
        float3x3(duv.w, -duv.y, 0, -duv.z, duv.x, 0, 0, 0, 0),
        float3x3(p2 - p1, p3 - p1, 0, 0, 0)
    );
    return length(r._11_12_13) / length(r._21_22_23);
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
