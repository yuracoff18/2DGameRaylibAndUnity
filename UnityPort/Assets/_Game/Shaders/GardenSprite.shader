Shader "Garden/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _FlashAmount ("Impacto", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "CanUseSpriteAtlas"="True" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex GardenVertex
            #pragma fragment GardenFragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct GardenInput
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct GardenOutput
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FlashAmount;
            CBUFFER_END

            GardenOutput GardenVertex(GardenInput input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                GardenOutput output = (GardenOutput)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }
            half4 GardenFragment(GardenOutput input) : SV_Target
            {
                half4 pixel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                // Aclara solo el dibujo, conservando su transparencia y el alfa de la caida.
                pixel.rgb *= 1.0h + 0.45h * saturate(_FlashAmount);
                return pixel;
            }
            ENDHLSL
        }
    }
}
