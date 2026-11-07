// Full-screen "iris" used when walking through a portal. _Cover 0 = clear, 1 = the whole screen is _Fill.
// In between, a round hole shrinks (or opens) around the middle of the screen with a slightly wobbling,
// swirling edge and a ring in the accent colour. Meant for a UI RawImage that fills the screen.
Shader "Custom/PortalWipe"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cover ("Cover", Range(0, 1)) = 0
        _Fill ("Fill colour", Color) = (1, 0.5, 0.1, 1)
        _Accent ("Ring colour", Color) = (0.1, 0.08, 0.2, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Wipe"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float _Cover;
            half4 _Fill;
            half4 _Accent;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 p = (i.uv - 0.5) * 2.0;
                p.x *= aspect;
                float r = length(p);
                float ang = atan2(p.y, p.x);

                float c = saturate(_Cover);
                float maxR = length(float2(aspect, 1.0)) + 0.15;      // big enough to clear the screen corners
                float mid = sin(c * 3.14159265);                       // 0 when fully open or fully closed

                // Hole radius, with a wobble that swirls while it moves
                float edge = (1.0 - c) * maxR * (1.0 + 0.12 * sin(ang * 3.0 + c * 9.0) * mid);
                float cover = smoothstep(edge, edge + 0.015, r);       // 1 outside the hole

                // Ring just outside the hole (fades out as the screen closes completely)
                float ring = (1.0 - smoothstep(0.0, 0.06, abs(r - edge - 0.03))) * (1.0 - smoothstep(0.8, 1.0, c)) * step(0.002, c);

                // Faint spiral in the covered area, only while moving
                float arms = sin(ang * 3.0 + r * 4.0 - c * 10.0) * 0.5 + 0.5;
                float3 fill = lerp(_Fill.rgb, _Accent.rgb, arms * 0.10 * mid * smoothstep(edge, edge + 1.5, r));

                float3 col = lerp(fill, _Accent.rgb, ring);
                float alpha = saturate(cover + ring) * i.color.a;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
