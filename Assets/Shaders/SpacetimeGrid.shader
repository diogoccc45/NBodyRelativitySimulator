Shader "Custom/SpacetimeGrid"
{
    Properties
    {
        _GridColor("Cor da Grid (plana)", Color) = (0.2, 0.6, 1.0, 1.0)
        _GlowColor("Cor do Glow (plana)", Color) = (0.4, 0.8, 1.0, 1.0)
        _MidGridColor("Cor da Grid (media)", Color) = (0.8, 0.9, 1.0, 1.0)
        _MidGlowColor("Cor do Glow (media)", Color) = (0.9, 1.0, 1.0, 1.0)
        _DeepGridColor("Cor da Grid (deformada)", Color) = (1.0, 0.6, 0.1, 1.0)
        _DeepGlowColor("Cor do Glow (deformada)", Color) = (1.0, 1.0, 0.8, 1.0)
        _LineWidth("Espessura das Linhas", Range(0.01, 0.15)) = 0.04
        _GlowWidth("Largura do Glow", Range(0.0, 0.3)) = 0.12
        _Brightness("Brilho", Range(0.1,  3.0)) = 1.4
        _FadeEdge("Fade nas Bordas", Range(0.0,  0.5)) = 0.12
        _MaxDeform("Deformacao Maxima", Range(1.0, 50.0)) = 15.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "SpacetimeGridPass"
            Tags {"LightMode" = "UniversalForward"}

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GridColor;
                half4 _GlowColor;
                half4 _MidGridColor;
                half4 _MidGlowColor;
                half4 _DeepGridColor;
                half4 _DeepGlowColor;
                half  _LineWidth;
                half  _GlowWidth;
                half  _Brightness;
                half  _FadeEdge;
                half  _MaxDeform;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS: POSITION;
                float2 uv: TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS: SV_POSITION;
                float2 uv: TEXCOORD0;
                float  deformDepth: TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                // Y negativo = deformado — normaliza 0 (plano) a 1 (máximo deformado)
                OUT.deformDepth = saturate(-IN.positionOS.y / _MaxDeform);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 cell = frac(IN.uv * 30.0);
                float2 lineDist = min(cell, 1.0 - cell);
                float minDist  = min(lineDist.x, lineDist.y);

                float lineMask = 1.0 - smoothstep(_LineWidth * 0.5, _LineWidth, minDist);

                float glow = 1.0 - smoothstep(0.0, _GlowWidth, minDist);
                glow = glow * glow;

                float2 edgeDist = min(IN.uv, 1.0 - IN.uv);
                float edgeFade = smoothstep(0.0, _FadeEdge, min(edgeDist.x, edgeDist.y));

                // Gradiente suave de três cores:
                // 0.0-0.5 - azul ciano para branco frio
                // 0.5-1.0 - branco frio para laranja quente
                float d = IN.deformDepth;
                half3 gridCol = d < 0.5
                    ? lerp(_GridColor.rgb, _MidGridColor.rgb,  d * 2.0)
                    : lerp(_MidGridColor.rgb, _DeepGridColor.rgb, (d - 0.5) * 2.0);
                half3 glowCol = d < 0.5
                    ? lerp(_GlowColor.rgb, _MidGlowColor.rgb, d * 2.0)
                    : lerp(_MidGlowColor.rgb, _DeepGlowColor.rgb, (d - 0.5) * 2.0);

                // Brilho extra nas zonas mais deformadas
                half brightBoost = 1.0 + d * 1.2;

                half3 color = lerp(glowCol * glow, gridCol, lineMask);
                half alpha = saturate((lineMask + glow * 0.4) * edgeFade) * _Brightness * brightBoost;

                return half4(color * _Brightness * brightBoost, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

