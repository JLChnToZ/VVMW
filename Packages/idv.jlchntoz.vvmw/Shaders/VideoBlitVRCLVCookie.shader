Shader "Hidden/JLChnToZ/VideoBlit (VRCLightVolumes Cookie)" {
    Properties {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [HideInInspector] _MainTex ("(Unused)", 2D) = "black" {}
        _VideoTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        [Vector(Left X, Left Y, Right X, Right Y)] _StereoShift ("Stereo Shift", Vector) = (0, 0, 0, 0)
        [Vector(X, Y, Half Mode)] _StereoExtend ("Stereo Extend", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        [Toggle(_STEREO_DEBUG)] _StereoDebug ("Stereo Debug", Int) = 0
        [Toggle(_HAS_EMISSION_INTENSITY)] _HasEmission ("Enable Emission Intensity", Int) = 1
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
    }
    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "PreviewType" = "Plane"
            "VideoScreenFeatures" = "Brightness,AutoScale,Stereo"
        }
        Pass {
            ZTest Always
            ZWrite Off
            Lighting Off
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local_fragment _ _HAS_EMISSION_INTENSITY
            #pragma shader_feature_local_fragment _ _STEREO_DEBUG
            #include "./VideoShaderCommon.cginc"

            sampler2D _VideoTex;
            float4 _VideoTex_ST;
            float4 _VideoTex_TexelSize;
            float4 _Color;
            int _IsAVProVideo;
            int _ScaleMode;
            float _AspectRatio;
            float4 _StereoShift;
            float3 _StereoExtend;
            #ifdef _HAS_EMISSION_INTENSITY
                float _EmissionIntensity;
            #endif

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = vert_getVideoUV(TRANSFORM_TEX(v.texcoord, _VideoTex).xy, _VideoTex_TexelSize, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
                return o;
            }

            half4 frag(v2f i) : SV_Target {
                float4 color = _Color;
                color *= frag_getVideoTexture(_VideoTex, i.uv, _IsAVProVideo, _StereoShift, _StereoExtend);
                #ifdef _HAS_EMISSION_INTENSITY
                    color.rgb *= _EmissionIntensity;
                #endif
                color.a = 1;
                return color;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}