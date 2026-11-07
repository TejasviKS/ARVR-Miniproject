// VERSION 1
// The "hostile AI" hologram: same family as Custom/Hologram (the Cad_lab Keeper), but made for enemies.
//  - Rim glow (fresnel), moving scan lines and a flicker, like the Keeper.
//  - DIFFERENT from the Keeper: it also has a dark, see-through CORE, so it stays readable on any background
//    (an additive hologram would vanish against a bright flat room), and a GLITCH: thin horizontal slices of the
//    body now and then jump sideways for a moment, like a bad signal.
//  - _Flash lights the whole body up for a moment (used when it is hit).
// Not affected by scene lights. Lives in "Resources" so it is always included in builds.
Shader "Custom/HologramHostile"
{
    Properties
    {
        _Color ("Rim colour", Color) = (1, 0.22, 0.62, 1)
        _CoreColor ("Core colour", Color) = (0.10, 0.02, 0.18, 1)
        _CoreAlpha ("Core opacity", Range(0, 1)) = 0.72
        _Strength ("Rim brightness", Range(0, 3)) = 1.3
        _RimPower ("Rim sharpness", Range(0.5, 8)) = 2
        _LineDensity ("Scan lines per metre", Float) = 45
        _LineSpeed ("Scan line speed", Float) = 0.9
        _Flicker ("Flicker amount", Range(0, 1)) = 0.4
        _Glitch ("Glitch (sideways tearing, 0 = off)", Range(0, 1)) = 0
        _Flash ("Hit flash", Range(0, 2)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One OneMinusSrcAlpha      // premultiplied: the glow is ADDED, the core DARKENS what is behind
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color, _CoreColor;
            float _CoreAlpha, _Strength, _RimPower, _LineDensity, _LineSpeed, _Flicker, _Glitch, _Flash;

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float3 normal  : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float  worldY  : TEXCOORD2;
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // glitch: in 11 cm slices of height, a slice sometimes jumps sideways (relative to the camera) for 1/10 s
                float slot = floor(_Time.y * 10);
                float band = floor(worldPos.y * 9);
                float r = frac(sin(band * 12.9898 + slot * 78.233) * 43758.5453);
                float on = step(0.965, r) * _Glitch;
                float3 right = UNITY_MATRIX_V[0].xyz;
                worldPos += right * on * (frac(r * 57.0) - 0.5) * 0.35;

                o.pos = UnityWorldToClipPos(worldPos);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                o.worldY = worldPos.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float rim = pow(1 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _RimPower);

                float lines = lerp(0.5, 1.0, 0.5 + 0.5 * sin((i.worldY - _Time.y * _LineSpeed) * _LineDensity * 6.2832));

                float slot = floor(_Time.y * 20);
                float rnd = frac(sin(slot * 12.9898) * 43758.5453);
                float flicker = 1 - _Flicker * step(0.92, rnd);

                float glow = (0.10 + rim * 1.5) * lines * flicker * _Strength;
                float a = _CoreAlpha * lerp(0.8, 1.0, lines) * flicker;
                float3 rgb = _Color.rgb * glow + _CoreColor.rgb * a + _Flash * a.xxx;      // premultiplied colour
                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
