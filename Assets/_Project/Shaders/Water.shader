// Moving water: waves in the vertex stage, ripples in the fragment stage.
//
// The river was drawn with URP's stock Lit material, tinted and made transparent in
// code. That gives a correct sheet of water and a completely still one — there is no
// texture on it to scroll and no geometry work to bend it, so nothing about it can
// move. Standing water reads as a painted floor however good its colour is.
//
// All of it is on the GPU and none of it is on the mesh. The sheet is built once by
// WaterMeshBuilder and never touched again; the waves are a function of world position
// and time evaluated per vertex, so neighbouring quads that share a corner compute the
// same displacement from the same world position and the surface cannot tear.
//
// **What tells this apart from a blue sheet is the depth.** WaterMeshBuilder writes how
// deep the water is at every corner into the vertex colour, and everything below reads
// it: the shallows are green and see-through, the channel is blue and dark, there is
// foam where the depth runs out, and the swell dies down as the bed comes up. A flat
// colour cannot do any of that, and it is the difference between a river and a strip of
// paint far more than the waves are.
//
// Cheap on purpose. This is a mobile game, the water can cover a good part of the
// screen, and the whole effect is a handful of sines — no texture fetches, no depth
// buffer read, no grab pass.
Shader "TheVeil/Water"
{
    Properties
    {
        // Deep water, and what the C# side sets. Kept under this name because
        // WaterMeshBuilder.Material writes _BaseColor and a rename there would be silent.
        _BaseColor ("Deep colour", Color) = (0.13, 0.28, 0.42, 0.88)

        // And the shallows: greener, and much more transparent, because in a foot of
        // water you can see the gravel. Both halves matter — deep water that you cannot
        // see into is only convincing next to shallow water that you can.
        _ShallowColor ("Shallow colour", Color) = (0.30, 0.47, 0.40, 0.42)

        // Waves. Small on purpose: the camera looks down from 47 m, and a river four to
        // eight metres wide with half-metre swell reads as a storm at sea.
        _WaveHeight ("Wave height", Range(0, 0.6)) = 0.14
        _WaveScale  ("Wave length", Range(1, 40)) = 9.0
        _WaveSpeed  ("Wave speed", Range(0, 4)) = 0.9

        // And the ripples, which are what actually reads at that distance. Movement
        // along the surface tells the eye it is water; the swell alone does not.
        //
        // The length is in metres and it is the number that matters: it decides how many
        // crests a body of water carries, and a count that suits a brook is a gale on a
        // river. See the fragment stage.
        _RippleDepth ("Ripple strength", Range(0, 1)) = 0.18
        _RippleScale ("Ripple length", Range(2, 60)) = 16.0
        _FlowSpeed   ("Flow speed", Range(0, 4)) = 1.1

        // Foam along the bank, measured in depth rather than in metres from the edge.
        // Wide, because the band is in normalised depth and the map's water is shallow:
        // the outermost corners come out around a sixth of the way to full depth, so a
        // narrow band lands entirely outside the mesh and draws nothing at all. This is
        // 0.4 of full depth — about 32 cm of water — which is a rim you can see.
        _FoamColor ("Foam colour", Color) = (0.86, 0.92, 0.94, 1)
        _FoamWidth ("Foam width", Range(0, 1)) = 0.40

        // Sun on the surface. Tight and bright rather than broad and dim: a wide
        // highlight is a plastic sheen, a narrow one that only a few crests catch is
        // glitter.
        _Glitter   ("Sun glitter", Range(0, 3)) = 1.1
        _Tightness ("Glitter tightness", Range(8, 512)) = 120
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Water"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _ShallowColor;
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _RippleDepth;
                float _RippleScale;
                float _FlowSpeed;
                half4 _FoamColor;
                float _FoamWidth;
                float _Glitter;
                float _Tightness;
            CBUFFER_END

            // The vertex colour is the depth, normalised against
            // WaterMeshBuilder.DeepEnough — zero at the waterline, one out in the
            // channel. All three channels carry it; red is as good as any.
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 colour     : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half   depth       : TEXCOORD2;
            };

            // Two crossing swells rather than one, so the surface never shows the ruled
            // parallel bands a single sine gives. The second is finer, slower and turned
            // off-axis, which is enough to break the pattern up.
            float Swell(float2 world, float time)
            {
                float a = sin((world.x + world.y * 0.6) / _WaveScale + time * _WaveSpeed);
                float b = sin((world.y - world.x * 0.4) / (_WaveScale * 0.55)
                              - time * _WaveSpeed * 0.7);
                return a * 0.6 + b * 0.4;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 world = TransformObjectToWorld(IN.positionOS.xyz);
                float time = _Time.y;

                // Swell dies in the shallows. A wave is the water moving up and down,
                // and there is nothing under a hand's depth of it to move — so a full
                // swell at the bank drives the surface through the bed and back out
                // again, which is a hole in the river once a frame. Scaling the height
                // by the depth also happens to be what water does: the shallows go
                // glassy and the channel keeps the chop.
                float shelf = saturate(IN.colour.r * 2.2);
                float height = _WaveHeight * shelf;

                world.y += Swell(world.xz, time) * height;

                // The normal from the slope of the swell, sampled either side rather than
                // differentiated by hand: the surface has to catch the light differently
                // where it tilts, or the waves move and nothing about them shows.
                const float probe = 0.5;
                float dx = Swell(world.xz + float2(probe, 0), time)
                         - Swell(world.xz - float2(probe, 0), time);
                float dz = Swell(world.xz + float2(0, probe), time)
                         - Swell(world.xz - float2(0, probe), time);

                OUT.normalWS = normalize(half3(-dx * height, probe * 2.0, -dz * height));
                OUT.positionWS = world;
                OUT.depth = (half)IN.colour.r;
                OUT.positionHCS = TransformWorldToHClip(world);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light sun = GetMainLight();
                half3 normal = normalize(IN.normalWS);
                half deep = saturate(IN.depth);

                // Shallow to deep. The single strongest thing on this list: a body of
                // water is read as a body by the gradient from its edge to its middle,
                // not by the colour it happens to be.
                half3 body = lerp(_ShallowColor.rgb, _BaseColor.rgb, deep);
                half alpha = lerp(_ShallowColor.a, _BaseColor.a, deep);

                // Half-lambert, like the ground shader: water turned away from the sun is
                // still water, and a hard terminator on a river looks like a seam.
                half ndotl = saturate(dot(normal, sun.direction)) * 0.5 + 0.5;
                half3 colour = body * ndotl * sun.color;

                // Ripples: two long bands drifting at slightly different angles. Only the
                // crests are kept — saturate throws the troughs away — so the surface
                // lightens in moving streaks instead of pulsing as a whole.
                //
                // <b>The length is in metres now, and that was the whole fault.</b> These
                // were two crossing waves of 2.5 and 4 metres multiplied together, which
                // is a lattice with about a two-metre pitch. Across a brook four metres
                // wide that is a single crest and reads as moving water; across a river
                // fifty metres wide it is twelve to twenty-five, and reads as chop on a
                // lake in a gale. Same shader, same numbers — which is exactly why the
                // brooks looked right while the rivers did not.
                //
                // Sixteen metres puts three to five crests on the widest water the
                // generator makes, which is a river with a current and not a sea.
                float time = _Time.y * _FlowSpeed;
                float k = 6.28318 / max(_RippleScale, 0.01);

                // Two directions fifteen degrees apart rather than at right angles, and
                // summed rather than multiplied. Crossing them at ninety degrees builds a
                // chequerboard; a narrow fan builds streaks running with the current,
                // which is what the surface of a river actually does.
                float along  = dot(IN.positionWS.xz, float2(0.94, 0.34));
                float across = dot(IN.positionWS.xz, float2(0.82, 0.57));

                // The drift rates come down with the length rather than staying put. A
                // wave four times longer at the same angular rate travels four times
                // faster, so keeping the old numbers here would have answered too much
                // chop with a river running at walking pace.
                float crest = sin(along * k - time * 0.43) * 0.6
                            + sin(across * k / 0.68 + time * 0.47) * 0.4;
                crest = saturate(crest);

                colour += _RippleDepth * crest * crest * deep;

                // Foam where the depth runs out. The band is set in depth rather than in
                // metres along the ground, so it is wide where the bank shelves gently
                // and narrow where it drops — which is what puts foam around a gravel bar
                // and only a line along a cut bank, without anything here knowing which
                // is which.
                //
                // Its width breathes with the ripple, so the waterline surges instead of
                // sitting there as a painted stripe.
                float edge = _FoamWidth * (0.72 + 0.5 * crest);
                half foam = 1.0h - smoothstep(0.0, max(edge, 0.001), deep);
                foam *= foam;

                colour = lerp(colour, _FoamColor.rgb * sun.color, foam);
                alpha = lerp(alpha, 1.0h, foam * 0.85h);

                // Sun glitter. Blinn-Phong on the wave normal, which the vertex stage has
                // already worked out — so the whole cost of it is a half vector and a
                // power, and it is the thing that makes the surface look wet rather than
                // merely blue.
                half3 view = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half3 halfway = normalize(sun.direction + view);
                half spec = pow(saturate(dot(normal, halfway)), _Tightness) * _Glitter;

                colour += spec * sun.color;

                // A highlight has to be visible through transparency, or the glitter is
                // brightest exactly where the water is thinnest and least opaque.
                alpha = saturate(alpha + spec);

                return half4(colour, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
