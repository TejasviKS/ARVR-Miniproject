// VERSION 1
// See-through glowing hologram (used by the riddle NPC).
//  - Additive: it adds light on top of whatever is behind it, so it looks transparent and glowing.
//  - Rim glow (fresnel): edges facing away from the camera glow brighter, like a force field.
//  - Horizontal scan lines that slowly move upwards, plus a quick flicker now and then.
// Not affected by scene lights. Lives in a "Resources" folder so it is always included in builds.
Shader "Custom/Hologram"
{
    Properties
    {
        _Color ("Colour", Color) = (0.2, 0.85, 1, 1)
        _Strength ("Brightness", Range(0, 3)) = 1
        _RimPower ("Rim Sharpness", Range(0.5, 8)) = 2.5
        _LineDensity ("Scan Lines Per Metre", Float) = 35
        _LineSpeed ("Scan Line Speed", Float) = 0.5
        _Flicker ("Flicker Amount", Range(0, 1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        Blend One One       // additive
        ZWrite Off          // don't hide things behind it
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Strength, _RimPower, _LineDensity, _LineSpeed, _Flicker;

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float3 normal  : TEXCOORD0;   // surface direction (world)
                float3 viewDir : TEXCOORD1;   // from the surface to the camera (world)
                float  worldY  : TEXCOORD2;   // height, for the scan lines
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = _WorldSpaceCameraPos - worldPos;
                o.worldY = worldPos.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Rim: 0 where the surface faces the camera, 1 at the silhouette edges
                float rim = pow(1 - saturate(dot(normalize(i.normal), normalize(i.viewDir))), _RimPower);

                // Scan lines: a sine wave over height that moves up over time (never fully dark)
                float lines = 0.5 + 0.5 * sin((i.worldY - _Time.y * _LineSpeed) * _LineDensity * 6.2832);
                lines = lerp(0.55, 1.0, lines);

                // Flicker: about 7% of the time (random 1/20 s slots) the hologram dims for a moment
                float slot = floor(_Time.y * 20);
                float rnd = frac(sin(slot * 12.9898) * 43758.5453);
                float flicker = 1 - _Flicker * step(0.93, rnd);

                float glow = (0.12 + rim * 1.3) * lines * flicker * _Strength;
                return fixed4(_Color.rgb * glow, 1);
            }
            ENDCG
        }
    }
}
