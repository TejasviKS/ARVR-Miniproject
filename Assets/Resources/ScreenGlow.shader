// VERSION 13
// Simple glowing colour for computer screens (not affected by lights, like a real screen).
// Lives in a "Resources" folder so it is always included in builds.
Shader "Custom/ScreenGlow"
{
    Properties
    {
        _Color ("Colour", Color) = (0.1, 0.3, 0.8, 1)
    }
    SubShader
    {
        // Drawn just before transparent things (still writes depth like a solid screen). This way the flat
        // "realm" look (RealmStyle), which paints over all solid geometry, doesn't wipe the screens out.
        Tags { "RenderType"="Opaque" "Queue"="Transparent-1" }
        Cull Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            float4 vert (float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag () : SV_Target { return _Color; }
            ENDCG
        }
    }
}
