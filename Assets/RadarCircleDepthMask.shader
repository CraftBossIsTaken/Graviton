Shader "Custom/RadarCircleDepthMask"
{
    Properties
    {
        _Radius("Radius (in UV space 0..0.5)", Float) = 0.5
        _Center("UV Center (0..1)", Vector) = (0.5,0.5,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Geometry-1" "RenderType"="Opaque" }
        // We want to write depth but not color, so ColorMask 0 (no color write) and ZWrite On
        Pass
        {
            Cull Off
            ZWrite On
            ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float _Radius;
            float4 _Center;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv - _Center.xy;
                float dist = length(uv);
                // if outside radius, discard (do not write depth)
                if (dist > _Radius) discard;
                // inside radius: write depth (ColorMask 0 prevents color writes)
                return fixed4(0,0,0,0);
            }
            ENDCG
        }
    }
}
