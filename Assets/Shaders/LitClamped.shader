Shader "Custom/LitClamped"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0

        [Header(Light Intensity Clamping)]
        _MinLightIntensity ("Min Light Intensity", Range(0, 1)) = 0
        _MaxLightIntensity ("Max Light Intensity", Range(0.5, 5)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            // Main light shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            // Additional lights
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            // Shadows
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK

            // Other features
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct appdata_t
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float2 lightmapUV : TEXCOORD1;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogCoord : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Smoothness;
                float _Metallic;
                float _MinLightIntensity;
                float _MaxLightIntensity;
            CBUFFER_END

            v2f vert(appdata_t v)
            {
                v2f OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                OUT.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);

                // Baked GI/Lightmap support
                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);

                return OUT;
            }

            half4 frag(v2f i) : SV_Target
            {
                // Sample base texture
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                baseColor *= _BaseColor;

                // Setup lighting
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = i.positionWS;
                lightingInput.positionCS = i.positionCS;
                lightingInput.normalWS = normalize(i.normalWS);
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // Calculate shadow coordinates
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lightingInput.shadowCoord = float4(lightingInput.normalizedScreenSpaceUV, 0, 0);
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lightingInput.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #else
                    lightingInput.shadowCoord = float4(0, 0, 0, 0);
                #endif

                lightingInput.fogCoord = i.fogCoord;
                lightingInput.bakedGI = SAMPLE_GI(v.lightmapUV, i.vertexSH, lightingInput.normalWS);
                lightingInput.shadowMask = SAMPLE_SHADOWMASK(v.lightmapUV);

                // Setup surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor.rgb;
                surfaceData.alpha = baseColor.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.emission = half3(0, 0, 0);

                // Calculate final lit color
                half4 color = UniversalFragmentPBR(lightingInput, surfaceData);

                // Clamp lighting intensity while preserving texture detail
                // Extract the effective light multiplier by comparing lit result to albedo
                half3 albedoSafe = max(baseColor.rgb, half3(0.001, 0.001, 0.001)); // Avoid division by zero
                half3 lightMultiplier = color.rgb / albedoSafe;

                // Clamp the light multiplier to prevent under/over-exposure while keeping texture detail
                lightMultiplier = clamp(lightMultiplier,
                    half3(_MinLightIntensity, _MinLightIntensity, _MinLightIntensity),
                    half3(_MaxLightIntensity, _MaxLightIntensity, _MaxLightIntensity));

                // Reconstruct color with clamped lighting but preserved texture detail
                color.rgb = baseColor.rgb * lightMultiplier;

                return color;
            }
            ENDHLSL
        }

        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // Depth pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        // DepthNormals pass
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
