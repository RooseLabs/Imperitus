Shader "URP/Checkerboard"
{
    Properties
    {
        _Density   ("Density", Range(2,50)) = 30
        _ColorA    ("Color A", Color) = (1,1,1,1)
        _ColorB    ("Color B", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float _Density;
            float4 _ColorA;
            float4 _ColorB;

            Varyings vert (Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                ZERO_INITIALIZE(Varyings, o);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);

                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv * _Density;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 c = floor(i.uv) * 0.5;
                float checker = frac(c.x + c.y) * 2.0;
                return lerp(_ColorA, _ColorB, checker);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
