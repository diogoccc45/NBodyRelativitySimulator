Shader "Custom/AccretionDisk"
{
    Properties
    {
        _ColorInner     ("Cor Interior (mais quente)",  Color) = (1.0, 0.98, 0.90, 1.0)
        _ColorMid       ("Cor Media",                   Color) = (1.0, 0.55, 0.05, 1.0)
        _ColorOuter     ("Cor Exterior (mais frio)",    Color) = (0.4, 0.04, 0.0,  1.0)

        _InnerRadius    ("Raio Interior (UV)",    Range(0.0, 0.5))  = 0.14
        _OuterRadius    ("Raio Exterior (UV)",    Range(0.0, 0.5))  = 0.48
        _PhotonRing     ("Raio Anel de Fotoes",   Range(0.0, 0.5))  = 0.155
        _PhotonWidth    ("Largura Anel Fotoes",   Range(0.001, 0.05)) = 0.012
        _PhotonBright   ("Brilho Anel Fotoes",    Range(0.0, 10.0)) = 6.0

        _RotationSpeed  ("Velocidade de Rotacao", Range(0.0, 3.0))  = 0.5
        _FilamentCount  ("Numero de Filamentos",  Range(3.0, 30.0)) = 12.0
        _FilamentStr    ("Intensidade Filamentos",Range(0.0, 1.0))  = 0.45

        _DopplerStr     ("Forca do Efeito Doppler", Range(0.0, 2.0)) = 1.2
        _Brightness     ("Brilho Geral",          Range(0.1, 10.0)) = 1.8
        _EdgeSoftness   ("Suavidade das Bordas",  Range(0.001, 0.1)) = 0.025

        [Header(Tidal 3PM Deformation)]
        _TidalStretch   ("Estiramento Radial",  Range(0.0, 1.0))  = 0.0
        _TidalSquish    ("Achatamento Axial",   Range(0.0, 1.0))  = 0.0
        _TidalAngle     ("Angulo da Forca de Mare", Range(-3.14159, 3.14159)) = 0.0
        _TidalInnerPush ("Contracao Raio Interior", Range(0.0, 0.3)) = 0.0
        _HotSpotAngle   ("Angulo Hot Spot",     Range(-3.14159, 3.14159)) = 0.0
        _HotSpotStr     ("Intensidade Hot Spot",Range(0.0, 3.0))  = 0.0
        _DopplerBias    ("Assimetria Doppler",  Range(-1.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "Queue"          = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "AccretionDiskPass"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorInner, _ColorMid, _ColorOuter;
                half  _InnerRadius, _OuterRadius;
                half  _PhotonRing, _PhotonWidth, _PhotonBright;
                half  _RotationSpeed, _FilamentCount, _FilamentStr;
                half  _DopplerStr, _Brightness, _EdgeSoftness;
                half  _TidalStretch, _TidalSquish, _TidalAngle, _TidalInnerPush;
                half  _HotSpotAngle, _HotSpotStr, _DopplerBias;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 centered = IN.uv - 0.5;

                // ── Raio e ângulo originais (sem deformação) ───────────────
                // Usados para o discard — assim o disco não perde área
                float r_orig  = length(centered);
                float dynInner = max(_InnerRadius - _TidalInnerPush, 0.02);

                if (r_orig < dynInner || r_orig > _OuterRadius) discard;

                // ── Deformação tidal — aplicada DEPOIS do discard ──────────
                // Roda para o referencial tidal, distorce, volta.
                // Como o discard já passou, os limites do anel são preservados
                // e a distorção move o gás dentro do anel sem cortar píxeis.
                float cosA = cos(-_TidalAngle);
                float sinA = sin(-_TidalAngle);

                // Para o referencial tidal
                float2 rot;
                rot.x = centered.x * cosA - centered.y * sinA;
                rot.y = centered.x * sinA + centered.y * cosA;

                // Distorção quadrupolar: estica no eixo de separação, comprime no perp.
                // Normaliza depois para preservar o raio médio
                float stretchFactor = 1.0 + _TidalStretch * 0.8;
                float squishFactor  = 1.0 - _TidalSquish  * 0.6;
                rot.x *= stretchFactor;
                rot.y *= squishFactor;

                // Volta ao referencial original
                float2 deformed;
                deformed.x = rot.x *  cosA + rot.y * sinA;
                deformed.y = rot.x * -sinA + rot.y * cosA;

                // Raio e ângulo no espaço deformado — usados para cor, filamentos, etc.
                float r     = length(deformed);
                float angle = atan2(deformed.y, deformed.x);

                // Bordas suaves com inner dinâmico — usa r_orig para não cortar
                float innerFade = smoothstep(dynInner, dynInner + _EdgeSoftness, r_orig);
                float outerFade = smoothstep(_OuterRadius, _OuterRadius - _EdgeSoftness * 2.0, r_orig);
                float edgeFade  = innerFade * outerFade;

                // Gradiente radial no espaço deformado — cria a aparência elíptica
                float tRadial = saturate((r - dynInner) / (_OuterRadius - dynInner));

                // Rotação kepleriana
                float keplerSpeed = _RotationSpeed / sqrt(max(r * 2.0, 0.05));
                float animAngle   = angle + _Time.y * keplerSpeed;

                // Filamentos
                float f1 = pow(sin(animAngle * _FilamentCount) * 0.5 + 0.5, 3.0);
                float f2 = pow(sin(animAngle * _FilamentCount * 2.3 + 1.1) * 0.5 + 0.5, 4.0);
                float filamentsTotal = lerp(1.0, f1 * 0.7 + f2 * 0.3, _FilamentStr);

                // Doppler com viés tidal
                float dopplerAngle = animAngle + _DopplerBias * 3.14159;
                float doppler = 1.0 + _DopplerStr * (cos(dopplerAngle) * 0.5 + 0.5);

                // Gradiente de temperatura
                half3 color;
                if (tRadial < 0.3)
                    color = lerp(_ColorInner.rgb, _ColorMid.rgb, tRadial / 0.3);
                else
                    color = lerp(_ColorMid.rgb, _ColorOuter.rgb, (tRadial - 0.3) / 0.7);

                float innerGlow = 1.0 + pow(1.0 - tRadial, 2.5) * 2.0;

                // Hot spot tidal
                float adiff = angle - _HotSpotAngle;
                adiff = adiff - 6.28318 * floor((adiff + 3.14159) / 6.28318);
                float hotSpot  = exp(-adiff * adiff / 0.6) * _HotSpotStr * (1.0 - tRadial);
                half3 hotColor = lerp(color, _ColorInner.rgb * 2.0, saturate(hotSpot * 0.5));

                // Anel de fotões
                float dynPhoton  = _PhotonRing - _TidalInnerPush * 0.5;
                float photonRing = smoothstep(_PhotonWidth, 0.0, abs(r_orig - dynPhoton)) * _PhotonBright;
                half3 photonColor = _ColorInner.rgb * photonRing;

                half3 finalColor = hotColor * innerGlow * filamentsTotal * doppler * _Brightness;
                finalColor *= 1.0 + hotSpot;
                finalColor += photonColor;

                half alpha = edgeFade * filamentsTotal * 0.9;
                return half4(finalColor * saturate(alpha), saturate(alpha));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
