// Configurations
// 2D: stereoShift = float4(0, 0, 0, 0), stereoExtend = float2(1, 1)
// SBS (LR): stereoShift = float4(0, 0, 0.5, 0), stereoExtend = float2(0.5, 1)
// SBS (RL): stereoShift = float4(0.5, 0, 0, 0), stereoExtend = float2(0.5, 1)
// Over-Under (Left above): stereoShift = float4(0, 0.5, 0, 0), stereoExtend = float2(1, 0.5)
// Over-Under (Right above): stereoShift = float4(0, 0, 0, 0.5), stereoExtend = float2(1, 0.5)
// Size Mode: 0 = Stratch, 1 = Contain, 2 = Cover

#ifndef VIDEO_SHADER_COMMON_INCLUDED
#define VIDEO_SHADER_COMMON_INCLUDED

float4 getVideoTexture(sampler2D videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio, float4 stereoShift, float2 stereoExtend) {
    if (sizeMode) {
        float srcAspectRatio = texelSize.y * texelSize.z * stereoExtend.x / stereoExtend.y;
        if (abs(srcAspectRatio - aspectRatio) > 0.001) {
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
    }
    if (any(uv < 0 || uv > 1)) return 0;
    uv = uv * stereoExtend + lerp(stereoShift.xy, stereoShift.zw, unity_StereoEyeIndex);
    #if UNITY_UV_STARTS_AT_TOP
    if (avPro) uv.y = 1 - uv.y;
    #endif
    float4 c = tex2Dlod(videoTex, float4(uv, 0, 0));
    if (avPro) c.rgb = pow(c.rgb, 2.2);
    return c;
}

float4 getVideoTexture(sampler2D videoTex, float2 uv, float4 texelSize, bool avPro, int sizeMode, float aspectRatio) {
    return getVideoTexture(videoTex, uv, texelSize, avPro, sizeMode, aspectRatio, 0, 1);
}

#endif
