// The look of a portal: a vertical oval vortex with a pulsing rim, spiral arms rushing into a bright core,
// and a soft halo outside the rim. Colours come from the Portal component (Portal Color / Accent Color).
// Drawn on a quad that is a bit bigger than the opening (Halo = quad size / opening size).
Shader "Custom/PortalSwirl"
{
    Properties
    {
        _Color ("Colour", Color) = (0.62, 0.3, 1, 1)
        _Accent ("Accent (rim + arms)", Color) = (0.12, 0.05, 0.3, 1)
        _Speed ("Swirl speed", Float) = 1
        _Halo ("Halo size (quad size / opening size)", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Portal"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _Accent;
                float _Speed;
                float _Halo;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float halo = max(_Halo, 1.01);
                float2 p = (i.uv - 0.5) * 2.0 * halo;      // r = 1 is the edge of the opening
                float r = length(p);
                float t = _Time.y * _Speed;
                float ang = atan2(p.y, p.x);

                // Spiral arms turning and rushing towards the centre (3 arms, so the angle seam is invisible)
                float spiral = ang + r * 3.2 - t * 1.6;
                float arms = sin(spiral * 3.0 + sin(r * 5.0 - t * 2.0) * 0.8) * 0.5 + 0.5;
                arms = smoothstep(0.45, 0.8, arms);

                float3 bright = lerp(_Color.rgb, float3(1, 1, 1), 0.55);
                float3 body = lerp(_Color.rgb, _Accent.rgb, arms * saturate(0.25 + r * 0.9));
                body = lerp(bright, body, saturate(r * 2.2));            // glowing core

                // Rim of the opening, pulsing slightly
                float pulse = 0.85 + 0.15 * sin(t * 3.0);
                float rim = smoothstep(0.80, 0.90, r) * (1.0 - smoothstep(0.96, 1.0, r));
                body = lerp(body, _Accent.rgb * pulse, rim);

                // Inside the opening: solid. Outside: a soft halo that fades away.
                float inside = 1.0 - smoothstep(0.97, 1.0, r);
                float haloFade = pow(saturate(1.0 - (r - 1.0) / (halo - 1.0)), 2.0) * 0.55;
                float alpha = max(inside, (1.0 - inside) * haloFade) * _Color.a;
                float3 col = lerp(_Color.rgb, body, inside);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
