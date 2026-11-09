// "Realm" look: every pixel is ONE flat colour, and the shape of the world is drawn only by outlines.
// It ignores the scene's own colours/lighting completely: it paints the fill colour everywhere and draws the
// line colour where the depth jumps (object silhouettes) or the surface direction changes (corners, creases).
//
// Used by a Full Screen Pass Renderer Feature (set up by Tools > Realm Outline > Install). The colours and
// line settings come from the RealmStyle component through global shader values, so each realm can have
// its own palette without touching this material.
Shader "Custom/RealmOutline"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RealmOutline"
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _RealmFill;     // rgb = flat colour of everything (already in linear space)
            float4 _RealmLine;     // rgb = outline colour (already in linear space)
            float4 _RealmVoid;     // rgb = colour where nothing is drawn: open sky / empty space (linear)
            float4 _RealmParams;   // x = line thickness in pixels (at 1080p), y = depth threshold, z = crease threshold
            float4 _RealmReveal;   // xyz = where the real colours spread out from (world), w = how far they have reached (0 = off)
            float4 _RealmRevealEdge; // rgb = colour of the bright rim at the front of the spreading colours (linear), a = rim width in metres

            // 1 / eye-depth is a straight (affine) function across a flat surface on screen, so its
            // second difference is zero on flat walls/floors and only spikes at real edges.
            float InvDepth(float2 uv)
            {
                return rcp(max(LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams), 1e-4));
            }

            // Nothing was drawn here (open sky / empty space): the depth sits exactly on the far plane
            bool IsVoid(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                return raw <= 1e-6;
            #else
                return raw >= 1.0 - 1e-6;
            #endif
            }

            // Normal difference between two pixels; ignored next to the void (the depth test draws that edge)
            float NormalDiff(float3 nc, float2 uv)
            {
                return IsVoid(uv) ? 0.0 : 1.0 - dot(nc, SampleSceneNormals(uv));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                if (IsVoid(uv)) return half4(_RealmVoid.rgb, 1.0);

                // Distance (in uv) between the centre pixel and each sample; scales with the screen height
                float half_px = max(0.5, _RealmParams.x * _ScreenParams.y / 1080.0) * 0.5;
                float2 o = half_px * (_ScreenParams.zw - 1.0);

                float2 uvA = uv + float2(-o.x, -o.y);
                float2 uvB = uv + float2( o.x,  o.y);
                float2 uvC = uv + float2(-o.x,  o.y);
                float2 uvD = uv + float2( o.x, -o.y);

                // --- Silhouettes: sudden depth change, measured relative to the depth itself ---
                float dc = InvDepth(uv);
                float lap = abs(InvDepth(uvA) + InvDepth(uvB) - 2.0 * dc)
                          + abs(InvDepth(uvC) + InvDepth(uvD) - 2.0 * dc);
                float depthEdge = lap / max(dc, 1e-5);
                float t0 = max(_RealmParams.y, 1e-4);
                float depthLine = smoothstep(t0, t0 * 2.0, depthEdge);

                // --- Creases: the surface direction changes (wall meets floor, box corners ...) ---
                float3 nc = SampleSceneNormals(uv);
                float nsum = NormalDiff(nc, uvA) + NormalDiff(nc, uvB) + NormalDiff(nc, uvC) + NormalDiff(nc, uvD);
                float c0 = max(_RealmParams.z, 1e-4);
                float creaseLine = smoothstep(c0, c0 * 2.0, nsum);

                float edge = saturate(max(depthLine, creaseLine));
                float3 result = lerp(_RealmFill.rgb, _RealmLine.rgb, edge);

                // The world's real colours spreading out from a point (the defeated boss): inside the growing circle you see the scene as it
                // really is (needs "Fetch Color Buffer" on the pass), with a bright rim at the front of the wave.
                if (_RealmReveal.w > 0.0)
                {
                    float3 wp = ComputeWorldSpacePosition(uv, SampleSceneDepth(uv), UNITY_MATRIX_I_VP);
                    float d = distance(wp, _RealmReveal.xyz);
                    float inside = saturate((_RealmReveal.w - d) / 0.25);                       // a soft 25 cm edge
                    float3 sceneCol = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;       // ("real" is a type name in URP's shader library)
                    result = lerp(result, sceneCol, inside);
                    float rim = saturate(1.0 - abs(d - _RealmReveal.w) / max(_RealmRevealEdge.a, 0.01));
                    result = lerp(result, _RealmRevealEdge.rgb, rim * rim);
                }
                return half4(result, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
