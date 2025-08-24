#include "UnityCG.cginc"
#include "Packages/idv.jlchntoz.vrcw-foundation/Shaders/VRCMirrorCameraSelector.cginc"
#include "./VideoShaderCommon.cginc"

sampler2D _MainTex;
float4 _Color;
int _IsAVProVideo;
int _ScaleMode;
int _IsMirror;
float4 _MainTex_TexelSize;
float4 _StereoShift;
float3 _StereoExtend;
#ifndef _ESTIMATE_ASPECT_RATIO
    float _AspectRatio;
#endif
#ifdef _HAS_EMISSION_INTENSITY
    float _EmissionIntensity;
#endif
#ifdef _ALPHA_CLIP
    float _AlphaClipThreshold;
#endif
#ifdef _ROUND_CORNER
    float _Radius;
#endif

struct appdata {
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

#ifdef GEOM_SUPPORT
    struct v2g {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 worldPos : TEXCOORD1;
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    struct g2f {
        float4 vertex : SV_POSITION;
        #ifdef _ROUND_CORNER
            float4 uv : TEXCOORD0;
        #else
            float2 uv : TEXCOORD0;
        #endif
        UNITY_FOG_COORDS(1)
        #ifdef _ESTIMATE_ASPECT_RATIO
            float aspectRatio : TEXCOORD2;
        #endif
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };

    [maxvertexcount(3)]
    void geom(triangle v2g IN[3], inout TriangleStream<g2f> triStream) {
        #ifdef _ESTIMATE_ASPECT_RATIO
            float aspectRatio = estimateAspectRatio(IN[0].worldPos, IN[0].uv, IN[1].worldPos, IN[1].uv, IN[2].worldPos, IN[2].uv);
        #else
            float aspectRatio = _AspectRatio;
        #endif
        [unroll(3)]
        for (uint i = 0; i < 3; i++) {
            g2f o;
            UNITY_INITIALIZE_OUTPUT(g2f, o);
            UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(IN[i], o);
            UNITY_TRANSFER_INSTANCE_ID(IN[i], o);
            o.vertex = IN[i].vertex;
            #ifdef _ESTIMATE_ASPECT_RATIO
                o.aspectRatio = aspectRatio;
            #endif
            o.uv.xy = vert_getVideoUV(IN[i].uv, _MainTex_TexelSize, _ScaleMode, aspectRatio, _StereoShift, _StereoExtend);
            #ifdef _ROUND_CORNER
                o.uv.zw = IN[i].uv;
            #endif
            UNITY_TRANSFER_FOG(o, o.vertex);
            triStream.Append(o);
        }
        triStream.RestartStrip();
    }
#else
    #define v2g v2f
    #define g2f v2f

    struct v2f {
        float4 vertex : SV_POSITION;
        #ifdef _ROUND_CORNER
            float4 uv : TEXCOORD0;
        #else
            float2 uv : TEXCOORD0;
        #endif
        UNITY_FOG_COORDS(1)
        UNITY_VERTEX_INPUT_INSTANCE_ID
        UNITY_VERTEX_OUTPUT_STEREO
    };
#endif

#ifdef _ROUND_CORNER
    void clipRoundCorner(float2 uv, float aspectRatio, float radius) {
        uv = (0.5 - abs(uv - 0.5)) * float2(aspectRatio, 1) - radius;
        if (all(uv < 0)) clip(radius - length(uv));
    }
#endif

v2g vert (appdata v) {
    v2g o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2g, o);
    if (!isVisibleInVRC()) return o;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.vertex = UnityObjectToClipPos(v.vertex);
    if (_IsMirror && isInVRCMirror()) v.uv.x = 1.0 - v.uv.x;
    #ifdef GEOM_SUPPORT
        o.uv = v.uv;
        o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    #else
        o.uv.xy = vert_getVideoUV(v.uv, _MainTex_TexelSize, _ScaleMode, _AspectRatio, _StereoShift, _StereoExtend);
        #ifdef _ROUND_CORNER
            o.uv.zw = v.uv;
        #endif
        UNITY_TRANSFER_FOG(o, o.vertex);
    #endif
    return o;
}

half4 frag (g2f i) : SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    #ifdef _ROUND_CORNER
        #ifdef _ESTIMATE_ASPECT_RATIO
            float aspectRatio = i.aspectRatio;
        #else
            float aspectRatio = _AspectRatio;
        #endif
        clipRoundCorner(i.uv.zw, aspectRatio, _Radius);
    #endif
    half4 c = frag_getVideoTexture(_MainTex, i.uv.xy, _IsAVProVideo, _StereoShift, _StereoExtend);
    #ifdef _HAS_EMISSION_INTENSITY
        c.rgb *= _EmissionIntensity;
    #endif
    #ifdef _ALPHA_CLIP
        clip(c.a - _AlphaClipThreshold);
    #endif
    c *= _Color;
    UNITY_APPLY_FOG(i.fogCoord, c);
    return c;
}