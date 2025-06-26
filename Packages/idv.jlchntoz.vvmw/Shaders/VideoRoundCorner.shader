Shader "JLChnToZ/VideoRoundCorner" {
    Properties{
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        [Vector(Left X, Left Y, Right X, Right Y)] _StereoShift ("Stereo Shift", Vector) = (0, 0, 0, 0)
        [Vector(X, Y, Half Mode)] _StereoExtend ("Stereo Extend", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.05
        [Toggle(_)] _IsMirror ("Mirror Flip", Int) = 1
        [Toggle(_HAS_EMISSION_INTENSITY)] _HasEmission ("Enable Emission Intensity", Int) = 0
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        [Toggle(_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Int) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
        [Toggle(_STEREO_DEBUG)] _StereoDebug ("Stereo Debug", Int) = 0
    }
    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "VideoScreenFeatures" = "Brightness,AutoScale,Stereo"
        }
        LOD 100
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "./VideoShaderCommon.cginc"

            #pragma multi_compile_local __ _HAS_EMISSION_INTENSITY
            #pragma multi_compile_local __ _ALPHA_CLIP
            #pragma shader_feature_local __ _STEREO_DEBUG

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 uv : TEXCOORD0; // XY = modified UVs, ZW = original UVs
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
            float _Radius;
            float4 _MainTex_TexelSize;
            float4 _StereoShift;
            float3 _StereoExtend;
            #ifdef _HAS_EMISSION_INTENSITY
            float _EmissionIntensity;
            #endif
            #ifdef _ALPHA_CLIP
            float _AlphaClipThreshold;
            #endif

            void clipRoundCorner(float2 uv, float aspectRatio, float radius) {
                uv = (0.5 - abs(uv - 0.5)) * float2(aspectRatio, 1) - radius;
                if (all(uv < 0)) clip(radius - length(uv));
            }

            v2f vert (appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv.xyxy;
                if (_IsMirror && _VRChatMirrorMode) v.uv.x = 1.0 - v.uv.x;
                o.uv.xy = vert_getVideoUV(v.uv.xy, _MainTex_TexelSize, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
                return o;
            }

            half4 frag (v2f i) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                clipRoundCorner(i.uv.zw, _AspectRatio, _Radius);
                half4 c = frag_getVideoTexture(_MainTex, i.uv.xy, _IsAVProVideo, _StereoShift, _StereoExtend);
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
