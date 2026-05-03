Shader "Custom/NebulaClouds"
{
    Properties
    {
        // Cor base de cada nuvem — define o tom Via Láctea no material
        _ColorA ("Cor Primária (azul-violeta)", Color) = (0.25, 0.15, 0.6, 1)
        _ColorB ("Cor Secundária (laranja quente)", Color) = (0.7, 0.3, 0.1, 1)
        _ColorC ("Cor Terciária (azul frio)", Color) = (0.1, 0.25, 0.8, 1)

        // Índice de cor por quad — passado via MaterialPropertyBlock ou UV2
        // 0.0 → ColorA, 0.5 → ColorB, 1.0 → ColorC
        _ColorIndex ("Índice de Cor", Range(0,1)) = 0.0

        // Intensidade do brilho aditivo
        _Brightness ("Brilho", Range(0.0, 3.0)) = 1.2

        // Ruído — controla o quanto a nuvem parece irregular
        _NoiseScale ("Escala do Ruído", Range(1.0, 20.0)) = 6.0
        _NoiseStrength ("Força do Ruído", Range(0.0, 1.0)) = 0.45

        // Suavidade da queda radial (quanto mais alto, mais nítida a borda)
        _FalloffPower ("Falloff Radial", Range(0.5, 4.0)) = 1.4
    }

    SubShader
    {
        // URP — Transparent + Additive (igual ao teu material atual)
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "NebulaPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One          // Additive — acumula luz, não tapa o fundo
            ZWrite Off
            ZTest LEqual
            Cull Off               // Visível dos dois lados

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Propriedades ──────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half4 _ColorC;
                half  _ColorIndex;
                half  _Brightness;
                half  _NoiseScale;
                half  _NoiseStrength;
                half  _FalloffPower;
            CBUFFER_END

            // ── Estruturas ────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1; // x = colorIndex, y = sizeNorm (0=pequena, 1=grande)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 uv2         : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Ruído hash simples (sem textura) ──────────────────────────
            // Baseado em hash de Wang — rápido e suficiente para nuvens
            float hash(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Value noise 2D — interpola 4 pontos de hash vizinhos
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Cubic smoothstep
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // FBM (Fractal Brownian Motion) — 3 oitavas para aspeto de nuvem
            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    v   += amp * valueNoise(p);
                    p   *= 2.1;
                    amp *= 0.5;
                }
                return v;
            }

            // ── Vertex ────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.uv2         = IN.uv2;
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // Lê colorIndex e sizeNorm do UV2 (passados pelo ProceduralNebula.cs)
                half colorIndex = IN.uv2.x;
                half sizeNorm   = IN.uv2.y; // 0 = nuvem pequena, 1 = nuvem grande

                // UV centrado em (0,0) — varia de -0.5 a 0.5
                float2 centeredUV = IN.uv - 0.5;

                // Distância ao centro do quad (0 = centro, 1 = canto)
                float dist = length(centeredUV) * 2.0;

                // Descarta fragmentos fora do círculo — elimina os cantos do quad
                if (dist > 1.0) discard;

                // FBM sobre as UVs escaladas — cria a forma irregular da nuvem
                float noise = fbm(IN.uv * _NoiseScale);

                // Nuvens grandes têm falloff mais suave (mais difusas)
                // Nuvens pequenas têm falloff mais abrupto (mais nítidas e brilhantes)
                float falloffPower = lerp(_FalloffPower * 1.6, _FalloffPower * 0.7, sizeNorm);
                float falloff = pow(saturate(1.0 - dist), falloffPower);

                // Mistura falloff com ruído — o ruído "deforma" a borda da nuvem
                float alpha = falloff * lerp(1.0, noise, _NoiseStrength);
                alpha = saturate(alpha);

                // Cor Via Láctea — interpola entre as três cores com base em colorIndex (UV2.x)
                // em vez do _ColorIndex global — cada quad tem a sua própria cor
                half3 colAB = lerp(_ColorA.rgb, _ColorB.rgb, saturate(colorIndex * 2.0));
                half3 colBC = lerp(_ColorB.rgb, _ColorC.rgb, saturate(colorIndex * 2.0 - 1.0));
                half3 baseColor = lerp(colAB, colBC, step(0.5, colorIndex));

                // Subtil variação interna: zona central ligeiramente mais quente
                half3 warmCore = baseColor + half3(0.08, 0.04, -0.02) * (1.0 - dist);
                half3 finalColor = lerp(baseColor, warmCore, falloff * 0.6);

                // Modulação pelo ruído — zonas mais densas ficam ligeiramente mais brilhantes
                finalColor *= lerp(0.7, 1.3, noise);

                // Nuvens grandes são mais escuras e difusas — nuvens pequenas mais brilhantes
                // Dá a sensação de profundidade: estruturas próximas mais vivas, fundo mais suave
                half brightnessScale = lerp(1.0, 0.35, sizeNorm);

                return half4(finalColor * _Brightness * brightnessScale * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
