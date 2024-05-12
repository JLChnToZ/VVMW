// Based on Merlin's MUI and MSDF UI Shader
Shader "UI/Modified" {
    Properties {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1,1,1,1)

        [Header(MSDF Settings)]
        [Toggle(MSDF)] _UseMSDF ("Input is MSDF", Float) = 0
        [Toggle(MSDF_OVERRIDE)] _OverrideMSDF ("Use Override Texture", Float) = 0
        [NoScaleOffset] _MSDFTex("Override Texture", 2D) = "black" {}
		_PixelRange("MSDF Pixel Range", Float) = 4.0

        [Header(Conditional Appearance in VRChat Camera Mirror)]
        [Toggle(_VRC_SUPPORT)] _VRCSupport ("Enable", Int) = 0
        [EnumMask(Direct Look, VR Handheld Camera, Desktop Handheld Camera, Screen Shot, VR Mirror, VR Handheld Camera in Mirror, # INVALID #, VR Screen Shot in Mirror, Desktop Mirror, # INVALID #, Desktop Handheld Camera in Mirror, Desktop Screen Shot in Mirror)]
        _RenderMode ("Visible Modes", Int) = 4095
        [Toggle(_)] _MirrorFlip ("Flip in Mirror", Int) = 0

        [Header(Render Pipeline Settings)]
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [Space]
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Comparison Mode", Int) = 8
        [IntRange] _Stencil ("ID", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Operation", Int) = 0
        [IntRange] _StencilWriteMask ("Write Mask", Range(0, 255)) = 255
        [IntRange] _StencilReadMask ("Read Mask", Range(0, 255)) = 255
        [Space]
        [Enum(UnityEngine.Rendering.CompareFunction)] unity_GUIZTestMode("Z Test Mode", Int) = 4
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Int) = 2
        [Enum(UnityEngine.Rendering.ColorWriteMask)] _ColorMask ("Color Mask", Int) = 15
    }

    SubShader {
        Tags {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_Cull]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local __ MSDF
            #pragma multi_compile_local __ MSDF_OVERRIDE
            #pragma multi_compile_local __ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local __ UNITY_UI_ALPHACLIP
            #pragma shader_feature_local __ _VRC_SUPPORT

            struct appdata_t {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 generatedtexcoord : TEXCOORD2;
                centroid float2 centroidtexcoord : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            #if MSDF_OVERRIDE
                sampler2D _MSDFTex;
                float4 _MSDFTex_TexelSize;
            #else
                float4 _MainTex_TexelSize;
            #endif
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float _PixelRange;

            #ifdef _VRC_SUPPORT
                int _RenderMode;
                int _MirrorFlip;

                int _VRChatCameraMode; // 0 = Normal, 1 = VR Handheld Camera, 2 = Desktop Handheld Camera, 3 = Screenshot
                int _VRChatMirrorMode; // 0 = Normal, 1 = VR Mirror, 2 = Desktop Mirror
            #endif

            float median(float3 col) {
                return max(min(col.r, col.g), min(max(col.r, col.g), col.b));
            }

            v2f vert(appdata_t v, uint vertID : SV_VertexID) {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                #ifdef _VRC_SUPPORT
                    int currentRenderMode = pow(2, _VRChatCameraMode + _VRChatMirrorMode * 3);
                    if (_RenderMode / currentRenderMode % 2 == 0) {
                        OUT.worldPosition = 0;
                        OUT.vertex = 0;
                        OUT.color = 0;
                        OUT.texcoord = 0;
                        OUT.centroidtexcoord = 0;
                        OUT.generatedtexcoord = 0;
                        return OUT;
                    }
                    if (_MirrorFlip != 0 && _VRChatMirrorMode > 0) v.vertex.x *= -1;
                #endif

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = v.texcoord;
                OUT.centroidtexcoord = v.texcoord;

                OUT.color = v.color * _Color;

                const float2 generatedCoords[4] = {
                    float2(0, 0),
                    float2(1, 0),
                    float2(1, 1),
                    float2(0, 1),
                };

                OUT.generatedtexcoord = generatedCoords[vertID % 4];
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target {
                float4 color = IN.color;
                float2 texcoord = IN.texcoord;

                // Use Valve method of falling back to centroid interpolation if you fall out of valid interpolation range
                // Based on slide 44 of http://media.steampowered.com/apps/valve/2015/Alex_Vlachos_Advanced_VR_Rendering_GDC2015.pdf
                if (any(IN.generatedtexcoord > 1) || any(IN.generatedtexcoord < 0))
                    texcoord = IN.centroidtexcoord;

                #ifdef MSDF
                #ifdef MSDF_OVERRIDE
                    float2 msdfUnit = _PixelRange / _MSDFTex_TexelSize.zw;
                    float4 sampleCol = tex2D(_MSDFTex, texcoord);
                #else
                    float2 msdfUnit = _PixelRange / _MainTex_TexelSize.zw;
                    float4 sampleCol = tex2D(_MainTex, texcoord);
                #endif
                    float sigDist = median(sampleCol.xyz) - 0.5;
                    sigDist *= max(dot(msdfUnit, 0.5 / fwidth(texcoord)), 1); // Max to handle fading out to quads in the distance
                    float opacity = clamp(sigDist + 0.5, 0.0, 1.0);
                    color *= float4(1, 1, 1, opacity) + _TextureSampleAdd;
                #else
                    color *= tex2D(_MainTex, texcoord) + _TextureSampleAdd;
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                    color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                    clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}