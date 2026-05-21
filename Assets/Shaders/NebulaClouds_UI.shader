Shader "Custom/NebulaClouds_UI"
{
    Properties
    {
        // Palette restrita a frios — sem laranja/vermelho que explodem em Additive blend
        _ColorA        ("Cor A (azul-violeta)",  Color)        = (0.15, 0.10, 0.55, 1)
        _ColorB        ("Cor B (ciano-azul)",    Color)        = (0.05, 0.25, 0.65, 1)
        _ColorC        ("Cor C (violeta frio)",  Color)        = (0.28, 0.08, 0.50, 1)
        _ColorIndex    ("Índice de Cor",  Range(0,1))          = 0.0
        _Brightness    ("Brilho",         Range(0.0, 2.0))     = 0.9
        _NoiseScale    ("Escala do Ruído",Range(1.0, 20.0))    = 6.0
        _NoiseStrength ("Força do Ruído", Range(0.0, 1.0))     = 0.45
        _FalloffPower  ("Falloff Radial", Range(0.5, 4.0))     = 1.6
        _SizeNorm      ("Size Norm",      Range(0,1))          = 0.5
        _AnimSpeed     ("Vel. Animação",  Range(0.0, 2.0))     = 0.18
        _FilamentStr   ("Filamentos",     Range(0.0, 1.0))     = 0.40
        _VortexStr     ("Vórtice",        Range(0.0, 1.0))     = 0.22
        _PulseSpeed    ("Vel. Pulso",     Range(0.0, 2.0))     = 0.45

        [HideInInspector] _MainTex         ("Texture", 2D)            = "white" {}
        [HideInInspector] _StencilComp     ("Stencil Comparison",  Float) = 8
        [HideInInspector] _Stencil         ("Stencil ID",          Float) = 0
        [HideInInspector] _StencilOp       ("Stencil Operation",   Float) = 0
        [HideInInspector] _StencilWriteMask("Stencil Write Mask",  Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask",   Float) = 255
        [HideInInspector] _ColorMask       ("Color Mask",          Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest  [unity_GUIZTestMode]
        // SrcAlpha OneMinusSrcAlpha em vez de One One —
        // evita a acumulação explosiva de cor em Additive
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f    { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 worldPos:TEXCOORD1; };

            half4  _ColorA, _ColorB, _ColorC;
            half   _ColorIndex, _Brightness, _NoiseScale, _NoiseStrength;
            half   _FalloffPower, _SizeNorm;
            half   _AnimSpeed, _FilamentStr, _VortexStr, _PulseSpeed;
            float4 _ClipRect;

            float hash(float2 p)
            {
                p  = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i),             hash(i+float2(1,0)), u.x),
                            lerp(hash(i+float2(0,1)), hash(i+float2(1,1)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0; float amp = 0.5;
                for (int k = 0; k < 4; k++) { v += amp*valueNoise(p); p *= 2.1; amp *= 0.5; }
                return v;
            }

            float warpedFbm(float2 p)
            {
                float2 q = float2(fbm(p), fbm(p + float2(5.2, 1.3)));
                return fbm(p + 2.2 * q);
            }

            v2f vert(appdata IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.uv       = IN.uv;
                OUT.worldPos = IN.vertex;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                if (!UnityGet2DClipping(IN.worldPos.xy, _ClipRect)) discard;

                float2 centeredUV = IN.uv - 0.5;
                float  dist       = length(centeredUV) * 2.0;
                if (dist > 1.0) discard;

                float time = _Time.y;

                // Vórtice
                float angle = dist * _VortexStr * 1.8 + time * _AnimSpeed * 0.3;
                float s = sin(angle), c2 = cos(angle);
                float2 rotUV = float2(centeredUV.x*c2 - centeredUV.y*s,
                                      centeredUV.x*s  + centeredUV.y*c2) + 0.5;

                // Ruído animado
                float2 animOff = float2(time*_AnimSpeed*0.07, time*_AnimSpeed*0.05);
                float2 noiseUV = rotUV * _NoiseScale + animOff;

                float noiseBase = fbm(noiseUV);
                float noiseWarp = warpedFbm(noiseUV*0.75 + float2(time*_AnimSpeed*0.04, 0));
                float noise     = lerp(noiseBase, noiseWarp, _FilamentStr);

                // Falloff
                float falloffPower = lerp(_FalloffPower*1.6, _FalloffPower*0.7, _SizeNorm);
                float falloff      = pow(saturate(1.0 - dist), falloffPower);

                // Pulso
                float pulse = 1.0 + 0.07*sin(time*_PulseSpeed + dist*3.14);

                float alpha = saturate(falloff * lerp(1.0, noise, _NoiseStrength)) * pulse;

                // Cor — só frios
                half3 colAB    = lerp(_ColorA.rgb, _ColorB.rgb, saturate(_ColorIndex*2.0));
                half3 colBC    = lerp(_ColorB.rgb, _ColorC.rgb, saturate(_ColorIndex*2.0-1.0));
                half3 baseColor= lerp(colAB, colBC, step(0.5, _ColorIndex));

                half3 hueShift = lerp(_ColorA.rgb, _ColorC.rgb, noiseWarp);
                baseColor      = lerp(baseColor, hueShift, 0.18*_FilamentStr);

                half3 warmCore = baseColor + half3(0.02, 0.02, 0.04)*(1.0-dist);
                half3 col      = lerp(baseColor, warmCore, falloff*0.6);
                col           *= lerp(0.65, 1.35, noise);

                half brightnessScale = lerp(1.0, 0.45, _SizeNorm);
                // Alpha explícito no canal A para blend correcto
                return half4(col * _Brightness * brightnessScale, alpha);
            }
            ENDCG
        }
    }
}
