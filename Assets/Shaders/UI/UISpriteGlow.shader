Shader "RooseLabs/UI/SpriteGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _UVRect ("UV Rect", Vector) = (0, 0, 1, 1)

        [Header(Glow Properties)]
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _GlowWidth ("Glow Width", Range(0, 100)) = 10

        [Header(UI Settings)]
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
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _ClipRect;
            float4 _UVRect;

            float4 _GlowColor;
            float _GlowWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;

                return OUT;
            }

            float4 frag(v2f IN) : SV_Target
            {
                // Sample the main texture (the sprite)
                float4 color = tex2D(_MainTex, IN.texcoord);

                // Apply UI color tint (from Image component)
                color *= IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                // Get sprite UV bounds (set by script)
                float2 uvMin = _UVRect.xy;
                float2 uvMax = _UVRect.zw;

                // Compute sprite-local UV (0..1) and UV span so we can sample in local space
                float2 uvSpan = uvMax - uvMin;
                // Avoid division by zero for degenerate UV rects
                if (uvSpan.x <= 0.0) uvSpan.x = 1.0;
                if (uvSpan.y <= 0.0) uvSpan.y = 1.0;
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

                // Calculate glow - only show glow where sprite isn't already solid
                float glowMask = glowAlpha * (1.0 - color.a);

                // Premultiply the sprite color (we are using premultiplied blending)
                float3 spriteColor = color.rgb * color.a;

                // Add glow
                float3 glowColor = _GlowColor.rgb * glowMask;

                // Combine (premultiplied color + alpha)
                float3 finalColor = spriteColor + glowColor;
                float finalAlpha = saturate(color.a + glowMask);

                #ifdef UNITY_UI_ALPHACLIP
                clip (finalAlpha - 0.001);
                #endif

                return float4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
