Shader "RooseLabs/2D/Sprite-Unlit-Glow"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        [Header(Glow Properties)]
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowWidth ("Glow Width", Range(0, 100)) = 10
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 1
        _GlowSamples ("Glow Samples (per ring)", Range(4, 32)) = 16
        _GlowRings ("Glow Rings", Range(1, 24)) = 8
        [PerRendererData] _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)

        // Legacy properties for graceful fallback
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                half4 _Color;
                float4 _GlowColor;
                float _GlowWidth;
                float _GlowIntensity;
                float _GlowSamples;
                float _GlowRings;
                float4 _UVRect;
            CBUFFER_END

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color * unity_SpriteColor;

                #if defined(DEBUG_DISPLAY)
                    o.positionWS = TransformObjectToWorld(input.positionOS);
                    o.normalWS = TransformObjectToWorldNormal(input.normal);
                #endif

                return o;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // Sample the main texture (the sprite)
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Store original alpha before tinting
                float originalAlpha = texColor.a;

                // Apply color tint
                float4 color = texColor * IN.color;

                // Get sprite UV bounds
                float2 uvMin = _UVRect.xy;
                float2 uvMax = _UVRect.zw;
                float2 uvSpan = uvMax - uvMin;

                // Avoid division by zero for degenerate UV rects
                uvSpan.x = uvSpan.x <= 0.0 ? 1.0 : uvSpan.x;
                uvSpan.y = uvSpan.y <= 0.0 ? 1.0 : uvSpan.y;

                // Compute sprite-local UV (0..1 within the sprite's rect)
                float2 localUV = (IN.uv - uvMin) / uvSpan;

                // Convert glow width from pixels to local UV space
                // Scale by the UV span to keep glow consistent regardless of atlas packing
                float2 texelSize = _MainTex_TexelSize.xy;
                float texelSizeLocal = max(texelSize.x, texelSize.y);
                float glowWidthUV = (_GlowWidth * texelSizeLocal) / max(uvSpan.x, uvSpan.y);

                // Calculate glow effect
                float glowAlpha = 0.0;
                float totalWeight = 0.0;
                int numSamples = (int)_GlowSamples;
                int numRings = (int)_GlowRings;

                for (int ring = 0; ring < numRings; ++ring)
                {
                    // Distribute rings exponentially for better quality
                    float t = (float(ring) + 1.0) / float(numRings);
                    float ringDist = glowWidthUV * t * t;

                    // Gaussian-like falloff based on distance
                    float ringWeight = exp(-3.0 * t);

                    for (int i = 0; i < numSamples; ++i)
                    {
                        float angle = (float(i) / float(numSamples)) * 6.28318530718; // 2*PI
                        float2 offset = float2(cos(angle), sin(angle)) * ringDist;

                        // Sample in local UV space then map back to texture/atlas UVs
                        float2 sampleLocal = localUV + offset;

                        // Check if the sample is within the sprite's UV bounds (0-1 in local space)
                        // This prevents bleeding from neighboring sprites in the atlas
                        float2 sampleCheck = step(0.0, sampleLocal) * step(sampleLocal, 1.0);
                        float sampleInBounds = sampleCheck.x * sampleCheck.y;

                        float2 sampleUV = uvMin + sampleLocal * uvSpan;

                        // Sample the texture and mask by bounds check to prevent atlas bleeding
                        float sampleAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV).a * sampleInBounds;

                        // Only accumulate glow from pixels that are more opaque than the current pixel
                        // This prevents the sprite from glowing over itself
                        float alphaContribution = max(0.0, sampleAlpha - originalAlpha);
                        glowAlpha += alphaContribution * ringWeight;
                        totalWeight += ringWeight;
                    }
                }

                // Normalize
                glowAlpha /= totalWeight;

                // Calculate glow intensity - only show glow where sprite isn't already solid
                float glowMask = saturate(glowAlpha * _GlowIntensity) * (1.0 - originalAlpha);

                // Apply glow color with HDR intensity (alpha channel acts as intensity multiplier),
                // modulated by glow mask and sprite alpha from renderer
                float3 glowContribution = _GlowColor.rgb * _GlowColor.a * glowMask * IN.color.a;

                // Premultiplied alpha compositing:
                // Sprite color (already tinted) is premultiplied by its alpha
                float3 spriteColorPremul = color.rgb * color.a;

                // Final alpha is the combination of sprite and glow, modulated by renderer alpha
                float finalAlpha = saturate((originalAlpha + glowMask) * IN.color.a);

                // Combine: sprite (premultiplied) + glow (already premultiplied by glowMask)
                float3 finalColor = spriteColorPremul + glowContribution;

                return float4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}
