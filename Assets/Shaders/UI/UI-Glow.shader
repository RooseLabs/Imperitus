Shader "RooseLabs/UI/UI-Glow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Glow Properties)]
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowWidth ("Glow Width", Range(0, 100)) = 10
        _GlowIntensity ("Glow Intensity", Range(0, 10)) = 1
        [Toggle] _UseColorAlphaForGlow ("Use Main Color Alpha for Glow Opacity", Integer) = 0
        [PerRendererData] _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4  mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            // Glow properties
            float4 _GlowColor;
            float _GlowWidth;
            float _GlowIntensity;
            int _UseColorAlphaForGlow;
            float4 _UVRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.mask = float4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace)
                {
                    if(!IsGammaSpace())
                    {
                        v.color.rgb = UIGammaToLinear(v.color.rgb);
                    }
                }

                OUT.color = v.color * _Color;
                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
                //The incoming alpha could have numerical instability, which makes it very sensible to
                //HDR color transparency blend, when it blends with the world's texture.
                const half alphaPrecision = half(0xff);
                const half invAlphaPrecision = half(1.0/alphaPrecision);
                IN.color.a = round(IN.color.a * alphaPrecision)*invAlphaPrecision;

                half4 color = IN.color * (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd);

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // === GLOW EFFECT ===
                // Get sprite UV bounds (set by script)
                float2 uvMin = _UVRect.xy;
                float2 uvMax = _UVRect.zw;

                // Compute sprite-local UV (0..1) and UV span so we can sample in local space
                float2 uvSpan = uvMax - uvMin;
                // Avoid division by zero for degenerate UV rects
                uvSpan.x = uvSpan.x <= 0.0 ? 1.0 : uvSpan.x;
                uvSpan.y = uvSpan.y <= 0.0 ? 1.0 : uvSpan.y;
                float2 localUV = (IN.texcoord - uvMin) / uvSpan;

                // Convert glow width (in pixels) to local UV space so offsets are applied relative
                // to the sprite rather than the full atlas.
                float2 texelSize = _MainTex_TexelSize.xy;
                float texelSizeLocal = max(texelSize.x, texelSize.y);
                float glowWidthLocal = (_GlowWidth * texelSizeLocal) / max(uvSpan.x, uvSpan.y);

                // Calculate glow effect
                float glowAlpha = 0.0;
                float totalWeight = 0.0;
                const int numSamples = 16;
                const int numRings = 8;

                for (int ring = 0; ring < numRings; ++ring)
                {
                    // Distribute rings exponentially for better quality
                    float t = (float(ring) + 1.0) / float(numRings);
                    float ringDist = glowWidthLocal * t * t;

                    // Gaussian-like falloff based on distance
                    float ringWeight = exp(-3.0 * t);

                    for (int i = 0; i < numSamples; ++i)
                    {
                        float angle = (float(i) / float(numSamples)) * 6.28318530718; // 2*PI
                        float2 offset = float2(cos(angle), sin(angle)) * ringDist;
                        float2 sampleLocal = localUV + offset;

                        // Only sample if UV is within valid bounds to prevent texture wrapping artifacts
                        float2 sampleCheck = step(0.0, sampleLocal) * step(sampleLocal, 1.0);
                        float sampleInBounds = sampleCheck.x * sampleCheck.y;

                        // Map back to atlas UVs for sampling
                        float2 sampleAtlasUV = uvMin + sampleLocal * uvSpan;

                        float sampleAlpha = tex2D(_MainTex, sampleAtlasUV).a * sampleInBounds;
                        glowAlpha += sampleAlpha * ringWeight;
                        totalWeight += ringWeight;
                    }
                }

                // Normalize
                glowAlpha /= totalWeight;

                // Calculate glow intensity - only show glow where sprite isn't already solid
                float glowMask = saturate(glowAlpha * _GlowIntensity) * (1.0 - color.a);

                // Modulate glow by the main color's alpha if enabled
                if (_UseColorAlphaForGlow == 1)
                {
                    glowMask *= IN.color.a;
                }

                // Apply glow color with HDR intensity (alpha channel acts as intensity multiplier)
                float3 glowContribution = _GlowColor.rgb * _GlowColor.a * glowMask;

                // Premultiplied alpha compositing:
                // Sprite color is premultiplied by its alpha
                float3 spriteColorPremul = color.rgb * color.a;

                // Final alpha is the combination of sprite and glow
                float finalAlpha = saturate(color.a + glowMask);

                // Combine: sprite (premultiplied) + glow (already premultiplied by glowMask)
                float3 finalColor = spriteColorPremul + glowContribution;

                return float4(finalColor, finalAlpha);
            }
        ENDCG
        }
    }

    Fallback "UI/Default"
}
