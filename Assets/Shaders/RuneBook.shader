Shader "Custom/RuneBook"
{
    Properties
    {
        // Base textures
        _BaseTextureArray ("Base Texture Array", 2DArray) = "" {}
        _BaseTextureIndex ("Base Texture Index", Integer) = 0
        _RuneTexture ("Rune Texture", 2D) = "black" {}

        // Rune properties
        [Toggle(_HAS_RUNE)] _HAS_RUNE ("Has Rune", Integer) = 0
        _RunePosition ("Rune Position", Vector) = (0.5, 0.5, 0, 0)
        _RuneScale ("Rune Scale", Range(0.01, 1)) = 0.5
        [HDR] _RuneColor ("Rune Color", Color) = (1, 1, 1, 1)
        _RuneOpacity ("Rune Opacity", Range(0, 1)) = 1
        [Toggle(_PRESERVE_RUNE_ASPECT_RATIO)] _PRESERVE_RUNE_ASPECT_RATIO ("Preserve Rune Aspect Ratio", Integer) = 1

        [Header(Rune Glow Properties)]
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowWidth ("Glow Width", Range(0, 0.2)) = 0.1

        [Header(PBR Properties)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _Metallic ("Metallic", Range(0, 1)) = 0
        _MinLightIntensity ("Min Light Intensity", Range(0, 1)) = 0
        _MaxLightIntensity ("Max Light Intensity", Range(0.5, 5)) = 1.5

        [Header(Rune PBR Properties)]
        [Toggle(_RUNE_LIT)] _RUNE_LIT ("Rune Is Lit", Integer) = 0
        _RuneSmoothness ("Rune Smoothness", Range(0, 1)) = 0.5
        _RuneMetallic ("Rune Metallic", Range(0, 1)) = 0
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
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
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

            #pragma multi_compile_local_fragment __ _HAS_RUNE
            #pragma shader_feature_local_fragment _PRESERVE_RUNE_ASPECT_RATIO
            #pragma shader_feature_local_fragment _RUNE_LIT

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
                float4 shadowCoord : TEXCOORD5;
                float3 objectScale : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
            };

            TEXTURE2D_ARRAY(_BaseTextureArray);
            SAMPLER(sampler_BaseTextureArray);

            TEXTURE2D(_RuneTexture);
            SAMPLER(sampler_RuneTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseTextureArray_ST;
                int _BaseTextureIndex;
                float4 _RuneTexture_ST;
                float2 _RunePosition;
                float _RuneScale;
                half4 _RuneColor;
                float _RuneOpacity;
                half4 _GlowColor;
                float _GlowWidth;
                float _Smoothness;
                float _Metallic;
                float _MinLightIntensity;
                float _MaxLightIntensity;
                float _RuneSmoothness;
                float _RuneMetallic;
            CBUFFER_END

            v2f vert(appdata_t v)
            {
                v2f OUT;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(v.normalOS);

                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.normalWS = normalInputs.normalWS;
                OUT.uv = TRANSFORM_TEX(v.uv, _BaseTextureArray);
                OUT.fogCoord = ComputeFogFactor(positionInputs.positionCS.z);
                OUT.shadowCoord = GetShadowCoord(positionInputs);

                // Calculate object scale from transform matrix
                OUT.objectScale = float3(
                    length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                    length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)),
                    length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))
                );

                // Baked GI/Lightmap support
                OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
                OUTPUT_SH(OUT.normalWS.xyz, OUT.vertexSH);

                return OUT;
            }

            half4 frag(v2f i) : SV_Target
            {
                int slice = clamp(_BaseTextureIndex, 0, 254);
                half4 baseColor = SAMPLE_TEXTURE2D_ARRAY(_BaseTextureArray, sampler_BaseTextureArray, i.uv, slice);

                // Early out if no rune is present
                half3 finalColor = baseColor.rgb;
                float finalMetallic = _Metallic;
                float finalSmoothness = _Smoothness;
                half3 unlitEmission = half3(0, 0, 0);

                #if _HAS_RUNE
                    // Transform UVs for rune placement
                    float2 runeUV = i.uv;

                    // Center at rune position
                    runeUV -= _RunePosition;

                    // Apply aspect ratio correction if enabled
                    #if _PRESERVE_RUNE_ASPECT_RATIO
                        // Calculate the aspect ratio of the object's scale
                        float scaleX = i.objectScale.x;
                        float scaleY = i.objectScale.y;
                        float aspectRatio = scaleY / scaleX;

                        // Correct UV to maintain aspect ratio
                        runeUV.y *= aspectRatio;
                    #endif

                    // Apply scale
                    runeUV /= _RuneScale;

                    // Re-center the texture coordinates
                    runeUV += 0.5;

                    // Check if UV is within bounds for rune rendering (prevents clamped edge pixels)
                    float2 uvCheck = step(0.0, runeUV) * step(runeUV, 1.0);
                    float inBounds = uvCheck.x * uvCheck.y;

                    // Sample rune texture at normal scale
                    float2 runeAtlasUV = TRANSFORM_TEX(runeUV, _RuneTexture);
                    half4 runeColor = SAMPLE_TEXTURE2D(_RuneTexture, sampler_RuneTexture, runeAtlasUV);

                    // Apply bounds mask - make pixels outside bounds fully transparent
                    runeColor.a *= inBounds;

                    // Apply rune opacity
                    runeColor.a *= _RuneOpacity;

                    // Apply rune color tint
                    runeColor.rgb *= _RuneColor.rgb;

                    half glowAlpha = 0;
                    float totalWeight = 0;
                    const int numSamples = 16;
                    const int numRings = 8;

                    for (int ring = 0; ring < numRings; ++ring)
                    {
                        // Distribute rings exponentially for better quality
                        float t = (float(ring) + 1.0) / float(numRings);
                        float ringDist = _GlowWidth * t * t; // Quadratic distribution

                        // Gaussian-like falloff based on distance
                        float ringWeight = exp(-3.0 * t); // Exponential falloff

                        for (int i = 0; i < numSamples; ++i)
                        {
                            float angle = (float(i) / float(numSamples)) * 6.28318530718; // 2*PI
                            float2 offset = float2(cos(angle), sin(angle)) * ringDist;
                            float2 sampleUV = runeUV + offset;

                            // Only sample if UV is within valid bounds to prevent texture wrapping artifacts
                            float2 sampleCheck = step(0.0, sampleUV) * step(sampleUV, 1.0);
                            float sampleInBounds = sampleCheck.x * sampleCheck.y;

                            float2 sampleAtlasUV = TRANSFORM_TEX(sampleUV, _RuneTexture);
                            half sampleAlpha = SAMPLE_TEXTURE2D(_RuneTexture, sampler_RuneTexture, sampleAtlasUV).a * sampleInBounds * _RuneOpacity;
                            glowAlpha += sampleAlpha * ringWeight;
                            totalWeight += ringWeight;
                        }
                    }

                    // Normalize
                    glowAlpha /= totalWeight;

                    // Calculate glow - only show glow where rune isn't already solid
                    half glowMask = glowAlpha * (1.0 - runeColor.a);
                    half3 glowContribution = _GlowColor.rgb * glowMask;

                    #if _RUNE_LIT
                        // Lit mode: rune is affected by lighting with its own PBR properties
                        // Blend rune over base using alpha
                        finalColor = lerp(baseColor.rgb, runeColor.rgb, runeColor.a);

                        // Blend PBR properties based on rune alpha
                        finalMetallic = lerp(_Metallic, _RuneMetallic, runeColor.a);
                        finalSmoothness = lerp(_Smoothness, _RuneSmoothness, runeColor.a);

                        // Add glow on top (glow is always emissive)
                        finalColor += glowContribution;
                    #else
                        // Unlit mode: rune and glow are rendered as unlit emission
                        unlitEmission = runeColor.rgb * runeColor.a + glowContribution;

                        // For the base color under the rune, we keep the base texture but mask it out where rune is
                        // The rune itself will be added as unlit emission
                        finalColor = baseColor.rgb * (1.0 - runeColor.a) * (1.0 - glowMask);

                        // Blend PBR properties based on rune alpha (rune area becomes non-metallic/non-smooth)
                        finalMetallic = lerp(_Metallic, 0, runeColor.a);
                        finalSmoothness = lerp(_Smoothness, 0, runeColor.a);
                    #endif
                #endif

                // Setup lighting
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = i.positionWS;
                lightingInput.positionCS = i.positionCS;
                lightingInput.normalWS = normalize(i.normalWS);
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);

                // Calculate shadow coordinates - for screen-space shadows, use screen position
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    lightingInput.shadowCoord = float4(lightingInput.normalizedScreenSpaceUV, 0, 0);
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    lightingInput.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #else
                    lightingInput.shadowCoord = float4(0, 0, 0, 0);
                #endif

                lightingInput.fogCoord = i.fogCoord;
                lightingInput.bakedGI = SAMPLE_GI(input.lightmapUV, i.vertexSH, lightingInput.normalWS);
                lightingInput.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUV);

                // Setup surface data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.alpha = 1.0;
                surfaceData.metallic = finalMetallic;
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1.0;
                surfaceData.emission = unlitEmission; // Rune and glow are unlit emission

                // Calculate final lit color
                half4 color = UniversalFragmentPBR(lightingInput, surfaceData);

                // Clamp lighting intensity while preserving texture detail
                // Extract the effective light multiplier by comparing lit result to albedo
                half3 albedoSafe = max(finalColor, half3(0.001, 0.001, 0.001)); // Avoid division by zero
                half3 lightMultiplier = color.rgb / albedoSafe;

                // Clamp the light multiplier to prevent under/over-exposure while keeping texture detail
                lightMultiplier = clamp(lightMultiplier, half3(_MinLightIntensity, _MinLightIntensity, _MinLightIntensity), half3(_MaxLightIntensity, _MaxLightIntensity, _MaxLightIntensity));

                // Reconstruct color with clamped lighting but preserved texture detail
                color.rgb = finalColor * lightMultiplier + unlitEmission;

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
