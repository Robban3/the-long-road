// The circle on the ground showing how far a troop group can hit.
//
// Close kin to TheVail/RouteOverlay and different in the one way that matters: this is
// depth-tested. The drawn route is annotation and belongs on top of the picture; a
// reach ring is a mark on the ground *in* it, so a tree standing in front of the far
// side of the circle has to hide that part of it. Drawn with ZTest Always instead, the
// ring paints itself over trunks, wagons and the troops themselves, and stops reading
// as being on the ground at all.
//
// A polygon offset pulls it a hair toward the camera. The ring's vertices are sampled
// from the same height field the terrain mesh is built from and then lifted, which is
// nearly the same surface — near enough to z-fight on a shallow slope without this.
Shader "TheVail/RangeRing"
{
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
            Name "Ring"

            Cull Off
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4  color       : COLOR;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return IN.color;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
