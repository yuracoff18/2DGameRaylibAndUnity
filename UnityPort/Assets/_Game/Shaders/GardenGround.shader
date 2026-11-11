Shader "Garden/Ground"
{
    Properties { _MainTex ("Cesped", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex GroundVertex
            #pragma fragment GroundFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct GroundInput { float3 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct GroundOutput { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            GroundOutput GroundVertex(GroundInput input)
            {
                GroundOutput output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 GroundFragment(GroundOutput input) : SV_Target
            {
                // La malla calcula el mismo recorte centrado para cada celda.
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }
}
