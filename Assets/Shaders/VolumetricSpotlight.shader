Shader "Custom/URP/VolumetricSpotlight"
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
        
        // New properties for volumetric look
        _CenterBrightness ("Center Brightness", Range(0, 2)) = 0.8
        _NoiseScale ("Noise Scale", Range(1, 50)) = 10.0
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        _NoiseSpeed ("Noise Speed", Range(0, 5)) = 1.0
        _DistanceFade ("Distance Fade Power", Range(0.1, 5.0)) = 1.5
        _BaseFadeDistance ("Base Fade Distance", Range(0, 1)) = 0.3
        _BaseFadeSmoothness ("Base Fade Smoothness", Range(0.01, 0.5)) = 0.1
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
            Cull Off

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
                float3 positionOS : TEXCOORD5;
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
                float _CenterBrightness;
                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseSpeed;
                float _DistanceFade;
                float _BaseFadeDistance;
                float _BaseFadeSmoothness;
            CBUFFER_END

            // Simple 3D noise function
            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(lerp(hash(i + float3(0, 0, 0)), 
                                      hash(i + float3(1, 0, 0)), f.x),
                                 lerp(hash(i + float3(0, 1, 0)), 
                                      hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(hash(i + float3(0, 0, 1)), 
                                      hash(i + float3(1, 0, 1)), f.x),
                                 lerp(hash(i + float3(0, 1, 1)), 
                                      hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

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
                output.positionOS = input.positionOS.xyz;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Normalize vectors
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                // Distance from center axis (in UV space, x goes from 0 at edge to 0.5 at center)
                float distFromCenter = abs(input.uv.x - 0.5) * 2.0; // 0 at center, 1 at edge
                
                // Center is brighter (inverse of distance)
                float centerGlow = 1.0 - distFromCenter;
                centerGlow = pow(centerGlow, 2.0) * _CenterBrightness;

                // Fresnel effect (edges glow)
                float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _FresnelPower);

                // Rim lighting effect
                float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                rim *= _RimIntensity;

                // Height-based fade at the base
                // UV.y: 1 = top (source), 0 = bottom (ground)
                // We want to fade near the ground (when UV.y is close to 0)
                float distanceFromBottom = input.uv.y; // 0 at bottom, 1 at top
                
                // Calculate fade: starts fading at _BaseFadeDistance from bottom
                // _BaseFadeSmoothness controls how gradual the transition is
                float fadeStart = _BaseFadeDistance - _BaseFadeSmoothness;
                float fadeEnd = _BaseFadeDistance + _BaseFadeSmoothness;
                float heightFade = smoothstep(fadeStart, fadeEnd, distanceFromBottom);
                
                // Distance fade - gets thinner/fainter towards the tip
                float distanceFade = 1.0 - pow(input.uv.y, _DistanceFade);

                // Volumetric noise for "dust particles" effect
                float3 noiseCoord = input.positionWS * _NoiseScale;
                noiseCoord.y += _Time.y * _NoiseSpeed;
                float noiseValue = noise(noiseCoord);
                
                // Layer multiple noise octaves for detail
                noiseValue += noise(noiseCoord * 2.0) * 0.5;
                noiseValue += noise(noiseCoord * 4.0) * 0.25;
                noiseValue /= 1.75; // Normalize
                
                // Apply noise as variation
                float volumetricNoise = lerp(1.0, noiseValue, _NoiseStrength);

                // Optional pulse effect
                float pulse = 1.0;
                if (_PulseEffect > 0.5)
                {
                    pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseIntensity;
                }

                // Combine effects - center glow + edge definition + volumetric variation
                float alpha = _Alpha * (centerGlow + fresnel * 0.5 + rim) * heightFade * distanceFade * volumetricNoise * pulse;
                alpha = saturate(alpha);

                // Final color - brighter in center
                half4 color = _ConeColor;
                color.rgb *= (1.0 + centerGlow * 0.5); // Brighten center
                color.a = alpha;

                // Apply fog
                color.rgb = MixFog(color.rgb, input.fogFactor);

                return color;
            }
            ENDHLSL
        }
    }
}