Shader "Hidden/JLChnToZ/VideoBlit" {
    Properties {
        [PerRendererData] [NoScaleOffset]
        _MainTex ("Blit Texture", 2D) = "black" {}
    }
    SubShader {
        Tags {
            "RenderType" = "Opaque"
            "PreviewType" = "Plane"
            "Queue" = "Geometry"
        }
        Pass {
            ZTest Always
            ZWrite Off

            CGPROGRAM
            // Simple shader for blits platform dependent raw texture to else where.
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct v2f {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_base v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target {
                #if UNITY_UV_STARTS_AT_TOP
                i.uv.y = 1 - i.uv.y;
                #endif
                fixed4 col = tex2D(_MainTex, i.uv);
                col.rgb = pow(col.rgb, 2.2);
                col.a = 1;
                return col;
            }
            ENDCG
        }
    }
    FallBack "Unlit/Texture"
}