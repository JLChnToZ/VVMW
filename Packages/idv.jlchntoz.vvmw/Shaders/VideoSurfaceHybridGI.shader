Shader "JLChnToZ/VideoSurface (HybridGI)" {
    Properties {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        [Vector(Left X, Left Y, Right X, Right Y)] _StereoShift ("Stereo Shift", Vector) = (0, 0, 0, 0)
        [Vector(X, Y, Half Mode)] _StereoExtend ("Stereo Extend", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        [Toggle(_SUPER_SAMPLE)] _SuperSample ("Super Sample", Int) = 0
        [Toggle(_)] _IsMirror ("Mirror Flip", Int) = 1
        [EnumMask(Direct Look, VR Handheld Camera, Desktop Handheld Camera, Screenshot, VR Mirror, VR Handheld Camera in Mirror, VR Face Mirror, VR Screenshot in Mirror, Desktop Mirror, Desktop Face Mirror, Desktop Handheld Camera in Mirror, Desktop Screenshot in Mirror)]
        _RenderMode ("Visible Modes", Int) = 4095
        _Glossiness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        [Toggle(_STEREO_DEBUG)] _StereoDebug ("Stereo Debug", Int) = 0

        [Space]
        [Toggle(_LTCGI)] _LTCGI ("Use LTCGI", Int) = 0
        [Toggle(_VRCLV)] _VRCLV ("Use VRC Light Volumes", Int) = 0
        [KeywordEnum(None, SH, RNM, MonoSH)] _Bakery ("Directional Lightmap Mode", Int) = 0
        [Toggle(_BAKERY_SHNONLINEAR)] _SHNonLinear ("Non-Linear SH", Int) = 0
    }
    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "VideoScreenFeatures" = "Brightness,AutoScale,Stereo"
        }
        LOD 200

        CGPROGRAM
        #pragma surface surf StandardHybrid fullforwardshadows vertex:vert

        #pragma target 3.0
        #pragma shader_feature_fragment _EMISSION
        #pragma shader_feature_local_fragment _ _STEREO_DEBUG
        #pragma shader_feature_local_fragment _ _SUPER_SAMPLE
        #ifdef _SUPER_SAMPLE
        #define ENFORCE_POINT_SAMPLING
        #endif
        #include "Packages/idv.jlchntoz.vrchybridgi/Shaders/Includes/StandardHybrid.cginc"
        #include "Packages/idv.jlchntoz.vrcw-foundation/Shaders/VRCMirrorCameraSelector.cginc"
        #include "./VideoShaderCommon.cginc"

        DECLARE_VIDEO_TEXTURE(_MainTex)
        float4 _Color;
        int _IsAVProVideo;
        int _ScaleMode;
        int _IsMirror;
        float _AspectRatio;
        float4 _StereoShift;
        float3 _StereoExtend;
        half _Glossiness;
        half _Metallic;
        half _EmissionIntensity;

        struct Input {
            float2 uv_MainTex;
        };

        void vert (inout appdata_full v) {
            if (!isVisibleInVRC()) {
                v.vertex = float4(0, 0, 0, 1);
                v.texcoord.xy = float2(0, 0);
                return;
            }
            if (_IsMirror && isInVRCMirror()) v.texcoord.x = 1.0 - v.texcoord.x;
            v.texcoord.xy = vert_getVideoUV(v.texcoord.xy, _MainTex_TexelSize, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
        }

        void surf (Input IN, inout SurfaceOutputStandard o) {
            #if _SUPER_SAMPLE
            half3 videoColor = frag_getVideoTextureSuperSample(_MainTex, IN.uv_MainTex, _MainTex_TexelSize, _IsAVProVideo, _StereoShift, _StereoExtend);
            #else
            half3 videoColor = frag_getVideoTexture(_MainTex, IN.uv_MainTex, _IsAVProVideo, _StereoShift, _StereoExtend);
            #endif
            o.Albedo = _Color.rgb + videoColor;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = _Color.a;
            o.Emission = videoColor * _EmissionIntensity;
        }
        ENDCG
    }
    FallBack "JLChnToZ/VideoSurface"
}
