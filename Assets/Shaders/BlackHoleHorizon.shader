Shader "Custom/BlackHoleHorizon"
{
    Properties
    {
        _RimColor("Cor do Anel de Fotoes", Color) = (0.6, 0.8, 1.0, 1.0)  // azul-branco
        _RimPower("Intensidade do Rim", Range(0.5, 8.0))  = 3.0
        _RimWidth("Largura do Anel", Range(0.0, 1.0))  = 0.4
        _GlowIntensity("Brilho do Anel", Range(0.0, 5.0))  = 2.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BlackHoleHorizonPass"
            Tags {"LightMode" = "UniversalForward"}

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half  _RimPower;
                half  _RimWidth;
                half  _GlowIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS: POSITION;
                float3 normalOS: NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS: SV_POSITION;
                float3 normalWS: TEXCOORD0;
                float3 viewDirWS: TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Normal e view direction em world space para o rim
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = normalize(GetWorldSpaceViewDir(
                    TransformObjectToWorld(IN.positionOS.xyz)));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                // Rim light — brilha nas bordas da esfera (ângulo entre normal e view direction)
                float rim = 1.0 - saturate(dot(normal, viewDir));

                // Anel de fotões — zona estreita e muito brilhante no limbo da esfera
                float photonRing = pow(rim, _RimPower) * _RimWidth;
                photonRing = saturate(photonRing);

                // Interior completamente negro — o horizonte de eventos não emite luz
                half3 color = _RimColor.rgb * photonRing * _GlowIntensity;

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
