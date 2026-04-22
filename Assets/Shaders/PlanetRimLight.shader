Shader "Custom/PlanetRimLight"
{
    Properties
    {
        _RimColor     ("Rim Color",     Color) = (0.4, 0.65, 1.0, 1.0)
        _RimPower     ("Rim Power",     Float) = 5.0
        _RimIntensity ("Rim Intensity", Float) = 0.7
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Front

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float  _RimPower;
                float  _RimIntensity;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = normalize(GetWorldSpaceViewDir(TransformObjectToWorld(IN.positionOS.xyz)));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 normal  = normalize(IN.normalWS);
                float3 viewDir = normalize(IN.viewDirWS);

                // rim = 1 quando a normal é perpendicular à câmara (bordas)
                //     = 0 quando a normal aponta para a câmara (centro)
                float rim   = 1.0 - saturate(dot(normal, viewDir));
                float alpha = pow(rim, _RimPower) * _RimIntensity;

                return half4(_RimColor.rgb, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
