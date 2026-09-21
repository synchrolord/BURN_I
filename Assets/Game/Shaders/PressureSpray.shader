Shader "BURN/Pressure Spray"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 world : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv; output.color = input.color;
                output.world = TransformObjectToWorld(input.positionOS.xyz).xy;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float fire = step(input.color.b, input.color.r);
                float2 p = input.uv * 2.0 - 1.0;
                float sway = sin(input.uv.y * 10.0 - _Time.y * 15.0 + input.world.x * 3.0);
                p.x += sway * 0.20 * fire * input.uv.y;
                float taper = lerp(1.0, 1.0 - input.uv.y * 0.75, fire);
                float radius = length(float2(p.x / max(taper, 0.15), p.y));
                float alpha = (1.0 - smoothstep(0.3, 1.0, radius)) * input.color.a;
                float core = (1.0 - smoothstep(0.0, 0.5, radius)) * 0.65;
                float3 color = lerp(input.color.rgb, float3(1.0, 0.98, 0.75), core * fire);
                color = lerp(color, float3(0.65, 0.92, 1.0), core * (1.0 - fire));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
