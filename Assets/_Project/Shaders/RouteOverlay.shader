// A line drawn on the map, not an object lying on the ground.
//
// Laid over the terrain as geometry it kept breaking into dashes: the ribbon samples
// the surface at tile centres and rides a fixed height above them, so on a slope the
// ground bulges through between samples, and every tree it passed behind cut it in
// half. Lifting it further only made it hover.
//
// Depth testing is the wrong idea for it. The route is annotation — the player draws
// it before the caravan exists — so it belongs on top of the picture rather than in
// it, and drawing it last with the depth test off says exactly that.
Shader "Arna/RouteOverlay"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
        }

        Pass
        {
            Name "Route"

            Cull Off
            ZWrite Off
            ZTest Always
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
