// VERSION 1
// Used by the "wasted" defeat screen: draws a picture in black and white, darkened, with dark corners.
// Lives in "Resources" so it is always included in builds.
Shader "Custom/WastedGrey"
{
    Properties
    {
        _MainTex ("Picture", 2D) = "white" {}
        _Grey ("Black and white", Range(0, 1)) = 1
        _Dark ("Darken", Range(0, 1)) = 0.25
        _Vignette ("Dark corners", Range(0, 1)) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Opaque" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Grey, _Dark, _Vignette;

            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float l = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, l.xxx, _Grey);
                c.rgb = (c.rgb - 0.5) * 1.15 + 0.5;                       // a little extra contrast, like a faded photo
                float2 d = i.uv - 0.5;
                float v = saturate(1.0 - dot(d, d) * 2.6 * _Vignette);   // dark corners
                c.rgb *= (1.0 - _Dark) * v;
                return fixed4(saturate(c.rgb), 1);
            }
            ENDCG
        }
    }
}
