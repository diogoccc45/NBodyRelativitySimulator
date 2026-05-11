Shader "Custom/AccretionDisk"
{
    Properties
    {
        _ColorInner("Cor Interior (mais quente)",  Color) = (1.0, 0.98, 0.90, 1.0)// branco quente
        _ColorMid("Cor Media", Color) = (1.0, 0.55, 0.05, 1.0)// laranja
        _ColorOuter("Cor Exterior (mais frio)", Color) = (0.4, 0.04, 0.0,  1.0)// vermelho escuro

        _InnerRadius("Raio Interior (UV)", Range(0.0, 0.5)) = 0.14
        _OuterRadius("Raio Exterior (UV)", Range(0.0, 0.5)) = 0.48
        _PhotonRing("Raio Anel de Fotoes", Range(0.0, 0.5)) = 0.155 // mesmo que inner + pequena margem
        _PhotonWidth("Largura Anel Fotoes", Range(0.001, 0.05)) = 0.012
        _PhotonBright("Brilho Anel Fotoes", Range(0.0, 10.0)) = 6.0

        _RotationSpeed("Velocidade de Rotacao", Range(0.0, 3.0))  = 0.5
        _FilamentCount("Numero de Filamentos", Range(3.0, 30.0)) = 12.0
        _FilamentStr("Intensidade Filamentos",Range(0.0, 1.0))  = 0.45

        _DopplerStr("Forca do Efeito Doppler", Range(0.0, 2.0)) = 1.2
        _Brightness("Brilho Geral", Range(0.1, 5.0))  = 1.8
        _EdgeSoftness("Suavidade das Bordas",  Range(0.001, 0.1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AccretionDiskPass"
            Tags {"LightMode" = "UniversalForward"}

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorInner;
                half4 _ColorMid;
                half4 _ColorOuter;
                half  _InnerRadius;
                half  _OuterRadius;
                half  _PhotonRing;
                half  _PhotonWidth;
                half  _PhotonBright;
                half  _RotationSpeed;
                half  _FilamentCount;
                half  _FilamentStr;
                half  _DopplerStr;
                half  _Brightness;
                half  _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;
                float  r        = length(centered);
                float  angle    = atan2(centered.y, centered.x);

                // Descarta fora do anel
                if (r < _InnerRadius || r > _OuterRadius) discard;

                // Bordas suaves
                float innerFade = smoothstep(_InnerRadius, _InnerRadius + _EdgeSoftness, r);
                float outerFade = smoothstep(_OuterRadius, _OuterRadius - _EdgeSoftness * 2.0, r);
                float edgeFade  = innerFade * outerFade;

                // Gradiente radial normalizado
                float tRadial = saturate((r - _InnerRadius) / (_OuterRadius - _InnerRadius));

                // Rotação kepleriana — interior mais rápido
                float keplerSpeed = _RotationSpeed / sqrt(max(r * 2.0, 0.05));
                float animAngle   = angle + _Time.y * keplerSpeed;

                // ── Filamentos radiais suaves ──────────────────────────────
                // Seno do ângulo multiplicado pelo número de filamentos
                // cria linhas radiais suaves que rodam — sem padrões blocosos
                float filaments = sin(animAngle * _FilamentCount);
                filaments = filaments * 0.5 + 0.5; // normaliza 0-1
                // Eleva a uma potência para filamentos mais finos e definidos
                filaments = pow(filaments, 3.0);

                // Segunda camada de filamentos mais finos — cria subtextura
                float filaments2 = sin(animAngle * _FilamentCount * 2.3 + 1.1);
                filaments2 = pow(filaments2 * 0.5 + 0.5, 4.0);

                float filamentsTotal = lerp(1.0, filaments * 0.7 + filaments2 * 0.3, _FilamentStr);

                // ── Efeito Doppler ─────────────────────────────────────────
                // O lado que se aproxima do observador brilha mais
                float doppler = 1.0 + _DopplerStr * (cos(animAngle) * 0.5 + 0.5);

                // ── Gradiente de temperatura ───────────────────────────────
                half3 color;
                if (tRadial < 0.3)
                    color = lerp(_ColorInner.rgb, _ColorMid.rgb, tRadial / 0.3);
                else
                    color = lerp(_ColorMid.rgb, _ColorOuter.rgb, (tRadial - 0.3) / 0.7);

                // Brilho extra no interior
                float innerGlow = 1.0 + pow(1.0 - tRadial, 2.5) * 2.0;

                // ── Anel de fotões ─────────────────────────────────────────
                // Linha brilhante muito fina mesmo à borda do horizonte de eventos
                float photonDist  = abs(r - _PhotonRing);
                float photonRing  = smoothstep(_PhotonWidth, 0.0, photonDist) * _PhotonBright;
                half3 photonColor = _ColorInner.rgb * photonRing;

                // ── Cor final ──────────────────────────────────────────────
                half3 finalColor = color * innerGlow * filamentsTotal * doppler * _Brightness;
                finalColor += photonColor; // adiciona o anel de fotões por cima

                half alpha = edgeFade * filamentsTotal * 0.9;
                alpha = saturate(alpha);

                return half4(finalColor * alpha, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
