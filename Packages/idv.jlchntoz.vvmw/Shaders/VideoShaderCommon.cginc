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

#if defined(ENFORCE_POINT_SAMPLING) && !defined(SHADER_TARGET_SURFACE_ANALYSIS)
    #define VIDEO_TEXTURE_SAMPLE_TYPE Texture2D<float4>
    #define READ_VIDEO_TEXTURE_LOD(tex, uv, lod) readRawTexel(tex, uv, lod)
    #define READ_VIDEO_TEXTURE(tex, uv) readRawTexel(tex, uv, 0)

    half4 readRawTexel(VIDEO_TEXTURE_SAMPLE_TYPE tex, float2 uv, float lod) {
        float2 dim;
        tex.GetDimensions(dim.x, dim.y);
        return tex.Load(int3(floor(uv * dim), lod));
    }
#else
    #undef ENFORCE_POINT_SAMPLING
    #define VIDEO_TEXTURE_SAMPLE_TYPE sampler2D
    #define READ_VIDEO_TEXTURE_LOD(tex, uv, lod) tex2Dlod(tex, float4((uv).xy, 0, lod))
    #define READ_VIDEO_TEXTURE(tex, uv) READ_VIDEO_TEXTURE_LOD(tex, uv, 0)
#endif
#define DECLARE_VIDEO_TEXTURE(tex) VIDEO_TEXTURE_SAMPLE_TYPE tex; float4 tex##_TexelSize;

inline half4 readVideoTextureMip(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float mipLevel) {
    return READ_VIDEO_TEXTURE_LOD(videoTex, uv, mipLevel);
}

inline half4 readAVProTextureMip(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float mipLevel) {
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif
    half4 c = readVideoTextureMip(videoTex, uv, mipLevel);
    #if !UNITY_COLORSPACE_GAMMA
        c.rgb = GammaToLinearSpace(c.rgb);
    #endif
    return c;
}

inline half4 readVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv) {
    return readVideoTextureMip(videoTex, uv, 0);
}

inline half4 readAVProTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv) {
    return readAVProTextureMip(videoTex, uv, 0);
}

inline float luminance(float3 rgb) {
    return dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

inline float4 lerp2d(float4 lb, float4 rb, float4 lt, float4 rt, float2 t) {
    return lerp(lerp(lb, rb, t.x), lerp(lt, rt, t.x), t.y);
}

inline float4 lerpSamples(float4 cuv, float2 uv, float4 lb, float4 rb, float4 lt, float4 rt) {
    float2 t = (uv - cuv.xy) / (cuv.zw - cuv.xy);
    return lerp2d(lb, rb, lt, rt, t);
}

// xy = chroma (hue) vector, z = luminance, w = chroma length squared
inline float4 rgb2Chroma(float3 rgb) {
    const float sqrt3Over2 = sqrt(3) / 2;
    float4 chroma = float4(
        rgb.r - 0.5 * (rgb.g + rgb.b),
        sqrt3Over2 * (rgb.g - rgb.b),
        luminance(rgb), 0
    );
    chroma.w = dot(chroma.xy, chroma.xy);
    UNITY_BRANCH if (chroma.w > 1e-6) chroma.xy *= rsqrt(chroma.w);
    else chroma.xy = 0;
    return chroma;
}

inline float3 chroma2Rgb(float4 chroma) {
    float cLenSqr = dot(chroma.xy, chroma.xy);
    UNITY_BRANCH if (cLenSqr > 1e-6) chroma.xy *= sqrt(chroma.w / cLenSqr);
    else chroma.xy = 0;
    const float3 consts = float3(2, -1, sqrt(3)) / 3;
    float3 result = chroma.xxy * consts;
    result.yz = mad(result.z, float2(1, -1), result.y);
    return result + chroma.z - luminance(result);
}

#ifdef ENFORCE_POINT_SAMPLING
    int4 getCorners(inout float2 uv, VIDEO_TEXTURE_SAMPLE_TYPE tex) {
        int2 dim;
        tex.GetDimensions(dim.x, dim.y);
        uv *= dim;
        float2 cuv = floor(uv);
        int4 uv4 = cuv.xyxy;
        uv4.zw += 1;
        uv4 = clamp(uv4, 0, dim.xyxy);
        return uv4;
    }

    inline float4 sampleChroma(VIDEO_TEXTURE_SAMPLE_TYPE tex, int2 uv) {
        return rgb2Chroma(tex.Load(int3(uv, 0)).rgb);
    }

    inline float4 sampleDirectBufferChroma(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, int2 uv) {
        float3 c = videoTex.Load(int3(uv, 0)).rgb;
        #if !UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
        #endif
        return rgb2Chroma(c);
    }
#else
    float4 getCorners(float2 uv, float4 texelSize) {
        float4 uv4 = uv.xyxy;
        uv4 *= texelSize.zwzw;
        uv4 = floor(uv4);
        uv4.zw += 1;
        return uv4 * texelSize.xyxy;
    }

    inline float4 sampleChroma(VIDEO_TEXTURE_SAMPLE_TYPE tex, float2 uv) {
        return rgb2Chroma(READ_VIDEO_TEXTURE(tex, uv).rgb);
    }

    inline float4 sampleDirectBufferChroma(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv) {
        float3 c = READ_VIDEO_TEXTURE(videoTex, uv).rgb;
        #if !UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
        #endif
        return rgb2Chroma(c);
    }
#endif

float3 readVideoTextureSuperSample(VIDEO_TEXTURE_SAMPLE_TYPE tex, float2 uv, float4 texelSize) {
    #ifdef ENFORCE_POINT_SAMPLING
        int4 cuv = getCorners(uv, tex);
    #else
        float4 cuv = getCorners(uv, texelSize);
    #endif
    return chroma2Rgb(lerpSamples(
        cuv, uv,
        sampleChroma(tex, cuv.xy),
        sampleChroma(tex, cuv.zy),
        sampleChroma(tex, cuv.xw),
        sampleChroma(tex, cuv.zw)
    ));
}

float3 readAVProTextureSuperSample(VIDEO_TEXTURE_SAMPLE_TYPE tex, float2 uv, float4 texelSize) {
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;
    #endif
    #ifdef ENFORCE_POINT_SAMPLING
        int4 cuv = getCorners(uv, tex);
    #else
        float4 cuv = getCorners(uv, texelSize);
    #endif
    return chroma2Rgb(lerpSamples(
        cuv, uv,
        sampleDirectBufferChroma(tex, cuv.xy),
        sampleDirectBufferChroma(tex, cuv.zy),
        sampleDirectBufferChroma(tex, cuv.xw),
        sampleDirectBufferChroma(tex, cuv.zw)
    ));
}

inline float estimateAspectRatio(float3 p1, float2 uv1, float3 p2, float2 uv2, float3 p3, float2 uv3) {
    float4 duv = float4(uv2, uv3) - uv1.xyxy;
    float3x3 r = mul(
        float3x3(duv.w, -duv.y, 0, -duv.z, duv.x, 0, 0, 0, 0),
        float3x3(p2 - p1, p3 - p1, 0, 0, 0)
    );
    float3 c0 = r._11_12_13;
    float3 c1 = r._21_22_23;
    return sqrt(dot(c0, c0) / dot(c1, c1));
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

half4 getVideoTextureMip(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, bool avPro, float4 stereoShift, float2 stereoExtend, float mipLevel) {
    #ifdef _STEREO_DEBUG
        half4 leftTexture = half4(1, 0.5, 0, 0.5), rightTexture = half4(0, 0.5, 1, 0.5);
        uv *= stereoExtend;
        stereoShift += uv.xyxy;
        if (avPro) {
            leftTexture *= readAVProTextureMip(videoTex, stereoShift.xy, mipLevel);
            rightTexture *= readAVProTextureMip(videoTex, stereoShift.zw, mipLevel);
        } else {
            leftTexture *= readVideoTextureMip(videoTex, stereoShift.xy, mipLevel);
            rightTexture *= readVideoTextureMip(videoTex, stereoShift.zw, mipLevel);
        }
        return leftTexture + rightTexture;
    #else
        uv = getStereoUV(uv, stereoShift, stereoExtend);
        return avPro ? readAVProTextureMip(videoTex, uv, mipLevel) : readVideoTextureMip(videoTex, uv, mipLevel);
    #endif
}

half3 getVideoTextureSuperSample(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float4 texelSize, bool avPro, float4 stereoShift, float2 stereoExtend) {
    #ifdef _STEREO_DEBUG
        half3 leftTexture = half3(1, 0.5, 0), rightTexture = half3(0, 0.5, 1);
        uv *= stereoExtend;
        stereoShift += uv.xyxy;
        if (avPro) {
            leftTexture *= readAVProTextureSuperSample(videoTex, stereoShift.xy, texelSize);
            rightTexture *= readAVProTextureSuperSample(videoTex, stereoShift.zw, texelSize);
        } else {
            leftTexture *= readVideoTextureSuperSample(videoTex, stereoShift.xy, texelSize);
            rightTexture *= readVideoTextureSuperSample(videoTex, stereoShift.zw, texelSize);
        }
        return leftTexture + rightTexture;
    #else
        uv = getStereoUV(uv, stereoShift, stereoExtend);
        return avPro ? readAVProTextureSuperSample(videoTex, uv, texelSize) : readVideoTextureSuperSample(videoTex, uv, texelSize);
    #endif
}

half4 getVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, bool avPro, float4 stereoShift, float2 stereoExtend) {
    return getVideoTextureMip(videoTex, uv, avPro, stereoShift, stereoExtend, 0);
}

half4 getVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio, float4 stereoShift, float2 stereoExtend, bool halfWidth) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, halfWidth ? float2(1, 1) : stereoExtend);
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTextureMip(videoTex, uv, avPro, stereoShift, stereoExtend, 0);
}

half4 getVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio, float4 stereoShift, float2 stereoExtend) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, stereoExtend);
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTextureMip(videoTex, uv, avPro, stereoShift, stereoExtend, 0);
}

half4 getVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio) {
    return getVideoTexture(videoTex, uv, texelSize, avPro, sizeMode, aspectRatio, 0, 1);
}

inline float2 vert_getVideoUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio, float3 stereoExtendAndHalfWidth) {
    if (sizeMode) uv = getUnstratchedUV(uv, texelSize, sizeMode, aspectRatio, stereoExtendAndHalfWidth.z > 0.0001 ? float2(1, 1) : stereoExtendAndHalfWidth.xy);
    return uv;
}

inline float2 vert_getVideoUV(float2 uv, float4 texelSize, int sizeMode, float aspectRatio, float4 stereoShift, float3 stereoExtendAndHalfWidth) {
    return vert_getVideoUV(uv, texelSize, sizeMode, aspectRatio, stereoExtendAndHalfWidth);
}

inline half4 frag_getVideoTexture(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, bool avPro, float4 stereoShift, float3 stereoExtend) {
    if (any(uv < 0 || uv > 1)) return 0;
    return getVideoTextureMip(videoTex, uv, avPro, stereoShift, stereoExtend.xy, 0);
}

inline half4 frag_getVideoTextureSuperSample(VIDEO_TEXTURE_SAMPLE_TYPE videoTex, float2 uv, float4 texelSize, bool avPro, float4 stereoShift, float2 stereoExtend) {
    if (any(uv < 0 || uv > 1)) return 0;
    return half4(getVideoTextureSuperSample(videoTex, uv, texelSize, avPro, stereoShift, stereoExtend), 1);
}
#endif
