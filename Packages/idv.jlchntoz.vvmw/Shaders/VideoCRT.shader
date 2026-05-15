Shader "JLChnToZ/VideoCRT" {
    Properties{
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        [Vector(Left X, Left Y, Right X, Right Y)] _StereoShift ("Stereo Shift", Vector) = (0, 0, 0, 0)
        [Vector(X, Y, Half Mode)] _StereoExtend ("Stereo Extend", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        [Toggle(_STEREO_DEBUG)] _StereoDebug ("Stereo Debug", Int) = 0
    }
    SubShader {
        Tags {
            "VideoScreenFeatures" = "AutoScale,Stereo"
        }
        Lighting Off
        Blend One Zero
        Pass {
            Name "VideoCRT"
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local __ _STEREO_DEBUG

            #include "./VideoShaderCommon.cginc"

            sampler2D _MainTex;
            float4 _Color;
            int _IsAVProVideo;
            int _ScaleMode;
            float _AspectRatio;
            float4 _MainTex_TexelSize;
            float4 _StereoShift;
            float3 _StereoExtend;

            v2f_customrendertexture vert (appdata_customrendertexture IN) {
                v2f_customrendertexture OUT = CustomRenderTextureVertexShader(IN);
                OUT.globalTexcoord.xy = vert_getVideoUV(OUT.globalTexcoord.xy, _MainTex_TexelSize, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
                return OUT;
            }

            half4 frag (v2f_customrendertexture i) : SV_Target {
                return frag_getVideoTexture(_MainTex, i.globalTexcoord.xy, _IsAVProVideo, _StereoShift, _StereoExtend) * _Color;
            }
            ENDCG
        }
    }
}
