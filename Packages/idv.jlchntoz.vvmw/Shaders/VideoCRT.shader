Shader "JLChnToZ/VideoCRT" {
    Properties{
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        _StereoShift ("Stereo Shift (XY = Left XY, ZW = Right XY)", Vector) = (0, 0, 0, 0)
        _StereoExtend ("Stereo Extend (XY)", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
    }
    SubShader {
        Lighting Off
        Blend One Zero
        Pass {
            Name "VideoCRT"
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            #include "./VideoShaderCommon.cginc"

            sampler2D _MainTex;
            float4 _Color;
            int _IsAVProVideo;
            int _ScaleMode;
            float _AspectRatio;
            float4 _MainTex_TexelSize;
            float4 _StereoShift;
            float2 _StereoExtend;

            float4 frag (v2f_customrendertexture i) : SV_Target {
                return getVideoTexture(_MainTex, i.globalTexcoord.xy, _MainTex_TexelSize, _IsAVProVideo, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
            }
            ENDCG
        }
    }
}
