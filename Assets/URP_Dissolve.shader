Shader "Custom/URP_Dissolve"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1,1,1,1)
        _EdgeColor ("Edge Color", Color) = (1,0.5,0,1)
        _EdgeWidth ("Edge Width", Range(0.0, 0.2)) = 0.05
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0.0
        _NoiseScale ("Noise Scale", Float) = 5.0
        _EmissionColor ("Emission Color", Color) = (1,0.5,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0,10)) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _DissolveAmount;
            float _NoiseScale;
            float4 _EmissionColor;
            float _EmissionIntensity;

            // Simple noise function (pseudo-random)
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv * _NoiseScale;
                float n = noise(uv);
                float dissolve = n - _DissolveAmount;

                // Smooth dissolve edge
                float edge = smoothstep(0.0, _EdgeWidth, dissolve);
                clip(edge - 0.01); // alpha clip

                float4 mainTex = tex2D(_MainTex, IN.uv);
                float4 baseColor = mainTex * _Color;

                // Edge color blend
                float3 finalColor = lerp(_EdgeColor.rgb, baseColor.rgb, edge);

                // Emission on edge
                float emissionMask = 1.0 - edge;
                float3 emission = _EmissionColor.rgb * emissionMask * _EmissionIntensity;

                // Basic lighting
                Light mainLight = GetMainLight();
                float3 normal = normalize(IN.normalWS);
                float NdotL = saturate(dot(normal, mainLight.direction));
                float3 lighting = finalColor * (mainLight.color * NdotL + 0.2);

                return float4(lighting + emission, baseColor.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
