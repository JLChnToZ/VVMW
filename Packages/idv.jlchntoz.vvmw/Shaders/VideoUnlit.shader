Shader "JLChnToZ/VideoUnlit" {
    Properties{
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        _StereoShift ("Stereo Shift (XY = Left XY, ZW = Right XY)", Vector) = (0, 0, 0, 0)
        _StereoExtend ("Stereo Extend (XY)", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        [Toggle(_)] _IsMirror ("Mirror Flip", Int) = 1
        [Toggle(_HAS_EMISSION_INTENSITY)] _HasEmission ("Enable Emission Intensity", Int) = 0
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        [Toggle(_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Int) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./VideoShaderCommon.cginc"

            #pragma multi_compile_local __ _HAS_EMISSION_INTENSITY
            #pragma multi_compile_local __ _ALPHA_CLIP

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _Color;
            int _IsAVProVideo;
            int _ScaleMode;
            int _IsMirror;
            int _VRChatMirrorMode;
            float _AspectRatio;
            float4 _MainTex_TexelSize;
            float4 _StereoShift;
            float2 _StereoExtend;
            #ifdef _HAS_EMISSION_INTENSITY
            float _EmissionIntensity;
            #endif
            #ifdef _ALPHA_CLIP
            float _AlphaClipThreshold;
            #endif

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.uv;
                if (_IsMirror && _VRChatMirrorMode) uv.x = 1.0 - uv.x;
                half4 c = getVideoTexture(_MainTex, uv, _MainTex_TexelSize, _IsAVProVideo, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
                #ifdef _HAS_EMISSION_INTENSITY
                c.rgb *= _EmissionIntensity;
                #endif
                #ifdef _ALPHA_CLIP
                clip(c.a - _AlphaClipThreshold);
                #endif
                return c * _Color;
            }
            ENDCG
        }
    }
}
