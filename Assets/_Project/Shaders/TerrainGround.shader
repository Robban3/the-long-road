// Lit vertex colour for the ground in the play view.
//
// The planning map and the play view draw the same mesh, but they are not the same
// picture. A map wants flat colour with no shading, which is what TheVeil/TerrainVertexColor
// gives it. Ground wants light: without a lit surface nothing can cast a shadow onto
// it, and a tree with no shadow under it reads as pasted onto the picture rather than
// standing in it.
//
// Colour still comes from the vertex, so terrain type stays readable and no texture
// is fetched. Lighting comes from the scene, so slopes shade themselves and the mesh
// no longer needs relief baked into its colours.
Shader "TheVeil/TerrainGround"
{
    Properties
    {
        // Full-strength shadow on a stylised palette goes to near-black and swallows
        // the terrain colour that the whole design asks the player to read.
        _ShadowStrength ("Shadow strength", Range(0, 1)) = 0.7
        _AmbientBoost   ("Ambient boost",   Range(0, 2)) = 1.0

        // Grain over the whole map, tinted by the terrain colour underneath rather
        // than replacing it. A single flat colour per square metre is what makes a
        // stylised landscape read as a diagram; ground in the real world is never one
        // value twice.
        _DetailMap      ("Ground detail", 2D) = "grey" {}
        _DetailTiling   ("Detail tiling (metres per repeat)", Float) = 6
        _DetailStrength ("Detail strength", Range(0, 1)) = 0.55

        // The same texture again at a much larger repeat. One tiling scale alone
        // reappears as a visible grid from a distance, because the eye finds the
        // period; two coprime scales multiplied together do not.
        _MacroTiling    ("Macro tiling (metres per repeat)", Float) = 41
        _MacroStrength  ("Macro strength", Range(0, 1)) = 0.35

        // Diagnostic, driven by -veilDebugShadow on a headless capture.
        //   1  raw shadow attenuation, or solid red if this shader was compiled without
        //      URP's main-light shadow keyword and so could never sample a shadow
        //   2  the same after _ShadowStrength is applied
        //   3  the sun's contribution alone
        //   4  the material's own parameters, one per colour channel
        // Every one of those failures looks identical in a finished frame: no shadow.
        _DebugShadow ("Debug: shadow diagnostic mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogFactor   : TEXCOORD2;
                float2 uv          : TEXCOORD3;
                half4  color       : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float _ShadowStrength;
                float _AmbientBoost;
                float _DebugShadow;
                float4 _DetailMap_ST;
                float _DetailTiling;
                float _DetailStrength;
                float _MacroTiling;
                float _MacroStrength;
            CBUFFER_END

            TEXTURE2D(_DetailMap);
            SAMPLER(sampler_DetailMap);

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = positions.positionCS;
                OUT.positionWS  = positions.positionWS;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor   = ComputeFogFactor(positions.positionCS.z);
                OUT.uv          = IN.uv;
                OUT.color       = IN.color;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                if (_DebugShadow > 0.5 && _DebugShadow < 1.5)
                {
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE) || defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    return half4(mainLight.shadowAttenuation.xxx, 1.0h);
                #else
                    return half4(1.0h, 0.0h, 0.0h, 1.0h);
                #endif
                }

                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                half ndotl  = saturate(dot(normalWS, mainLight.direction));

                // 2 shows the shadow term after _ShadowStrength is folded in, 3 shows
                // the sun's own contribution. Between them they say whether the shadow
                // is being lost on the way in or drowned by everything else.
                if (_DebugShadow > 1.5 && _DebugShadow < 2.5) return half4(shadow.xxx, 1.0h);
                if (_DebugShadow > 2.5 && _DebugShadow < 3.5)
                    return half4(mainLight.color * (ndotl * 0.75h + 0.25h), 1.0h);

                // 4 paints the material's own parameters: red is _ShadowStrength, green
                // is _AmbientBoost, blue is _DebugShadow scaled down. The CPU says all
                // three are set; this says which of them the GPU actually received.
                if (_DebugShadow > 3.5)
                    return half4(_ShadowStrength, _AmbientBoost, _DebugShadow * 0.25, 1.0);

                // Half-lambert rather than straight lambert: ground turned away from
                // the sun still has to show which terrain it is.
                half diffuse = ndotl * 0.75h + 0.25h;

                // Detail modulates brightness around 1 rather than replacing colour, so
                // grass stays green and rock stays grey while both gain grain. Sampling
                // it as albedo instead would drag every terrain type towards whatever
                // colour the photograph happened to be.
                half fine  = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap,
                                              IN.uv / max(_DetailTiling, 0.01)).g;
                half macro = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap,
                                              IN.uv / max(_MacroTiling, 0.01)).g;

                half detail = 1.0h + (fine - 0.5h) * _DetailStrength * 2.0h
                                   + (macro - 0.5h) * _MacroStrength * 2.0h;

                half3 ambient = SampleSH(normalWS) * _AmbientBoost;
                half3 lit = IN.color.rgb * detail * (ambient + mainLight.color * diffuse * shadow);

                lit = MixFog(lit, IN.fogFactor);
                return half4(lit, 1.0h);
            }
            ENDHLSL
        }

        // Lets hills shade the valleys behind them, not only props shade the ground.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            float4 shadowVert(ShadowAttributes IN) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);

                float4 positionCS = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                return positionCS;
            }

            half4 shadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // Required by anything that reads depth — ambient occlusion, depth of field,
        // soft particles. Cheap to provide, awkward to add later.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 depthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half4 depthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
