Shader "Custom/URP/SpotlightCone"
{
    Properties
    {
        _ConeColor ("Cone Color", Color) = (1, 1, 1, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.3
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 1.0
        _FresnelPower ("Fresnel Power", Range(0.1, 5.0)) = 2.0
        [Toggle] _PulseEffect ("Pulse Effect", Float) = 1
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off // Render both sides

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _ConeColor;
                float _Alpha;
                float _RimPower;
                float _RimIntensity;
                float _FresnelPower;
                float _PulseEffect;
                float _PulseSpeed;
                float _PulseIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Fresnel effect (edges glow more)
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Rim lighting effect
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                rim *= _RimIntensity;

                // Height-based fade (fade out at the base)
                float heightFade = input.uv.y; // Y goes from 0 (base) to 1 (tip)
                heightFade = smoothstep(0.0, 0.3, heightFade); // Fade bottom 30%

                // Optional pulse effect
                float pulse = 1.0;
                if (_PulseEffect > 0.5)
                {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;
                }

                // Combine effects
                float alpha = _Alpha * (fresnel + rim) * heightFade * pulse;
                alpha = saturate(alpha);

                // Final color
                half4 color = _ConeColor;
                color.a = alpha;

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }
}