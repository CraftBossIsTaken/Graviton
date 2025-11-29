Shader "Custom/URP/RadarVisible"
{
    Properties { _BaseColor("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags{"RenderType"="Opaque" "Queue"="Geometry+1"}
        Pass
        {
            ZWrite On
            ColorMask RGBA
            Stencil
            {
                Ref 1
                Comp equal
                Pass keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; };
            float4 _BaseColor;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return _BaseColor; }
            ENDHLSL
        }
    }
}
