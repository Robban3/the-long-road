// The little signs on the planning map that say what a thing is.
//
// Close kin to TheVail/RangeRing and different in one way: it reads a texture. The symbols
// are painted at run time into one atlas by TheVail.UI.Pixels — a gable for a house, a
// broken wall for a ruin, a bird for a flock of crows — and drawn as camera-facing
// quads, so what this has to do is sample that atlas and blend it over the landscape.
//
// Depth-tested, not drawn on top of everything. A symbol stands twelve metres over the
// ground it marks, which clears the trees, so on open country and hillsides it is
// visible; behind a mountain it is hidden, and that is right. A planning map that shows
// you a house through a mountain is not showing you the country.
//
// Unlit on purpose. A sign is not lit by the sun in the world it is a sign about.
Shader "TheVail/MapSymbol"
{
    Properties
    {
        _BaseMap ("Symbols", 2D) = "white" {}
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
            Name "Symbol"

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * IN.color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
