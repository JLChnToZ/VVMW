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
        [Toggle(_ESTIMATE_ASPECT_RATIO)] _EstimateAspectRatio ("Auto Estimate (PC Only, Experimental)", Int) = 0
        _Radius ("Corner Radius", Range(0, 0.5)) = 0.05
        [Toggle(_)] _IsMirror ("Mirror Flip", Int) = 1
        [EnumMask(Direct Look, VR Handheld Camera, Desktop Handheld Camera, Screenshot, VR Mirror, VR Handheld Camera in Mirror, _, VR Screenshot in Mirror, Desktop Mirror, _, Desktop Handheld Camera in Mirror, Desktop Screenshot in Mirror)]
        _RenderMode ("Visible Modes", Int) = 4095
        [Toggle(_HAS_EMISSION_INTENSITY)] _HasEmission ("Enable Emission Intensity", Int) = 0
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        [Toggle(_ALPHA_CLIP)] _AlphaClip ("Alpha Clip", Int) = 0
        _AlphaClipThreshold ("Alpha Clip Threshold", Range(0, 1)) = 0.5
        [Toggle(_STEREO_DEBUG)] _StereoDebug ("Stereo Debug", Int) = 0
    }
    SubShader {
        Name "Full"
        Tags {
            "RenderType" = "Opaque"
            "VideoScreenFeatures" = "Brightness,AutoScale,Stereo"
        }
        LOD 200
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma geometry geom
            #pragma target 4.0
            #pragma exclude_renderers gles gles3 glcore metal

            #pragma multi_compile_local_fragment _ _HAS_EMISSION_INTENSITY
            #pragma multi_compile_local_fragment _ _ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _STEREO_DEBUG
            #pragma shader_feature_local _ _ESTIMATE_ASPECT_RATIO
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define GEOM_SUPPORT
            #define _ROUND_CORNER

            #include "./VideoUnlit.cginc"
            ENDCG
        }
    }
    SubShader {
        Name "Fallback"
        Tags {
            "RenderType" = "Opaque"
            "VideoScreenFeatures" = "Brightness,AutoScale,Stereo"
        }
        LOD 100
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_local_fragment _ _HAS_EMISSION_INTENSITY
            #pragma multi_compile_local_fragment _ _ALPHA_CLIP
            #pragma shader_feature_local_fragment _ _STEREO_DEBUG
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #define _ROUND_CORNER

            #include "./VideoUnlit.cginc"
            ENDCG
        }
    }
    FallBack "JLChnToZ/VideoUnlit"
}
