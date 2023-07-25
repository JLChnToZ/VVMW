Shader "JLChnToZ/VideoSurface" {
    Properties {
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        [NoScaleOffset] _MainTex ("Video Texture", 2D) = "black" {}
        [Toggle(_)] _IsAVProVideo ("AVPro Video", Int) = 0
        [Enum(Stretch, 0, Contain, 1, Cover, 2)]
        _ScaleMode ("Scale Mode", Int) = 2
        _StereoShift ("Stereo Shift (XY = Left XY, ZW = Right XY)", Vector) = (0, 0, 0, 0)
        _StereoExtend ("Stereo Extend (XY)", Vector) = (1, 1, 0, 0)
        _AspectRatio ("Target Aspect Ratio", Float) = 1.777778
        [Toggle(_)] _IsMirror ("Mirror Flip", Int) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0
        #include "./VideoShaderCommon.cginc"

        sampler2D _MainTex;
        float4 _Color;
        int _IsAVProVideo;
        int _ScaleMode;
        int _IsMirror;
        float _AspectRatio;
        float4 _MainTex_TexelSize;
        float4 _StereoShift;
        float2 _StereoExtend;
        half _Glossiness;
        half _Metallic;

        struct Input {
            float2 uv_MainTex;
        };

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o) {
            float3 videoColor = getVideoTexture(_MainTex, IN.uv_MainTex, _MainTex_TexelSize, _IsAVProVideo, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
            o.Albedo = _Color.rgb + videoColor;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = _Color.a;
            o.Emission = videoColor;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
