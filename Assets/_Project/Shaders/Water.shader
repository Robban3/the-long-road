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
// Cheap on purpose. This is a mobile game, the water can cover a good part of the
// screen, and the whole effect is a handful of sines — no texture fetches, no depth
// buffer read, no grab pass.
Shader "TheVeil/Water"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.16, 0.34, 0.46, 0.80)

        // Waves. Small on purpose: the camera looks down from 47 m, and a river four to
        // eight metres wide with half-metre swell reads as a storm at sea.
        _WaveHeight ("Wave height", Range(0, 0.6)) = 0.14
        _WaveScale  ("Wave length", Range(1, 40)) = 9.0
        _WaveSpeed  ("Wave speed", Range(0, 4)) = 0.9

        // And the ripples, which are what actually reads at that distance. Movement
        // along the surface tells the eye it is water; the swell alone does not.
        _RippleDepth ("Ripple strength", Range(0, 1)) = 0.22
        _FlowSpeed   ("Flow speed", Range(0, 4)) = 1.1
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
                float _WaveHeight;
                float _WaveScale;
                float _WaveSpeed;
                float _RippleDepth;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
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

                world.y += Swell(world.xz, time) * _WaveHeight;

                // The normal from the slope of the swell, sampled either side rather than
                // differentiated by hand: the surface has to catch the light differently
                // where it tilts, or the waves move and nothing about them shows.
                const float probe = 0.5;
                float dx = Swell(world.xz + float2(probe, 0), time)
                         - Swell(world.xz - float2(probe, 0), time);
                float dz = Swell(world.xz + float2(0, probe), time)
                         - Swell(world.xz - float2(0, probe), time);

                OUT.normalWS = normalize(half3(-dx * _WaveHeight, probe * 2.0, -dz * _WaveHeight));
                OUT.positionWS = world;
                OUT.positionHCS = TransformWorldToHClip(world);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light main = GetMainLight();

                // Half-lambert, like the ground shader: water turned away from the sun is
                // still water, and a hard terminator on a river looks like a seam.
                half ndotl = saturate(dot(normalize(IN.normalWS), main.direction)) * 0.5 + 0.5;
                half3 colour = _BaseColor.rgb * ndotl * main.color;

                // Ripples: two fine bands drifting at different speeds and angles. Only
                // the crests are kept — saturate throws the troughs away — so the surface
                // lightens in moving streaks instead of pulsing as a whole.
                float time = _Time.y * _FlowSpeed;
                float crest = sin(dot(IN.positionWS.xz, float2(0.9, 0.42)) * 1.6 - time * 2.6)
                            * sin(dot(IN.positionWS.xz, float2(-0.35, 1.0)) * 2.4 + time * 1.7);

                colour += _RippleDepth * saturate(crest) * saturate(crest);

                return half4(colour, _BaseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
