Shader "BURN_I/Pixel Darkness Light2D"
{
    Properties
    {
        [PerRendererData] _MainTex("Darkness", 2D) = "white" {}
        _CellSize("Cell Size", Float) = 0.125
        _BaseLight("Base Light", Range(0, 1)) = 0.2
        _LightEffect("Light Effect", Range(0, 4)) = 1.25
        _MaximumLight("Maximum Light", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex DarknessVertex
            #pragma fragment DarknessFragment
            #pragma multi_compile USE_SHAPE_LIGHT_TYPE_0 __

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/LightingUtility.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightVariables.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 positionOS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _CellSize;
                float _BaseLight;
                float _LightEffect;
                float _MaximumLight;
            CBUFFER_END

            Varyings DarknessVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.positionOS = input.positionOS.xy;
                return output;
            }

            half4 DarknessFragment(Varyings input) : SV_Target
            {
                half darknessAlpha = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    input.uv).a;

                half unityLight = 0;

                #if USE_SHAPE_LIGHT_TYPE_0
                    //sample once from the centre of this cell
                    //so Unity lights still cut the darkness into pixels
                    float2 cellPosition =
                        (floor(input.positionOS / _CellSize) + 0.5) *
                        _CellSize;

                    float4 cellClip = TransformObjectToHClip(
                        float3(cellPosition, 0));

                    float2 lightUV = ComputeScreenPos(
                        cellClip / cellClip.w).xy;

                    half3 lightColor = SAMPLE_TEXTURE2D(
                        _ShapeLightTexture0,
                        sampler_ShapeLightTexture0,
                        lightUV).rgb;

                    half lightLevel = max(
                        lightColor.r,
                        max(lightColor.g, lightColor.b));

                    unityLight = saturate(
                        (lightLevel - _BaseLight) * _LightEffect);

                    //leave a little darkness for fire to brighten further
                    unityLight *= _MaximumLight;
                #endif

                return half4(
                    0,
                    0,
                    0,
                    darknessAlpha * (1 - unityLight));
            }
            ENDHLSL
        }
    }
}
