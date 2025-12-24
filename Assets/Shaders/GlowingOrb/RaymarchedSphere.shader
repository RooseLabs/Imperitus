Shader "Custom/RaymarchedSphere"
{
    Properties
    {
        _MainOpacity ("Main Opacity", Range(0.0, 1.0)) = 1.0

        [Header(Glow Settings)]
        _GlowColor ("Glow Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _GlowStrength ("Glow Strength", Range(0, 10)) = 1.0
        _GlowSharpness ("Glow Sharpness", Range(1, 64)) = 32.0
        _EmissionBoost ("Emission Boost", Range(1, 10)) = 2.0

        [Header(Sphere Settings)]
        _SphereSize ("Sphere Size", Range(0.0, 0.35)) = 0.35
        _SphereOpacity ("Sphere Opacity", Range(0.0, 1.0)) = 1.0

        [Header(Light Colors)]
        _Light1Color ("Light 1 Color", Color) = (0.3, 0.65, 1.0, 1.0)
        _Light1Strength ("Light 1 Strength", Range(0, 2)) = 0.75
        _Light2Color ("Light 2 Color", Color) = (0.6, 0.35, 1.0, 1.0)
        _Light2Strength ("Light 2 Strength", Range(0, 2)) = 0.75
        _Light3Color ("Light 3 Color", Color) = (0.4, 0.5, 1.0, 1.0)
        _Light3Strength ("Light 3 Strength", Range(0, 2)) = 0.5

        [Header(Specular Highlights)]
        [Toggle] _Specular1Enabled ("Enable Specular 1", Float) = 1
        _Specular1Color ("Specular 1 Color", Color) = (0.4, 0.625, 1.0, 1.0)
        _Specular1Strength ("Specular 1 Strength", Range(0, 2)) = 1.0
        _Specular1Sharpness ("Specular 1 Sharpness", Range(1, 128)) = 12.0
        _Specular1Position ("Specular 1 Position", Vector) = (600, 800, -500, 0)
        [Toggle] _Specular2Enabled ("Enable Specular 2", Float) = 1
        _Specular2Color ("Specular 2 Color", Color) = (0.6, 0.5625, 1.0, 1.0)
        _Specular2Strength ("Specular 2 Strength", Range(0, 2)) = 0.75
        _Specular2Sharpness ("Specular 2 Sharpness", Range(1, 128)) = 16.0
        _Specular2Position ("Specular 2 Position", Vector) = (-600, -800, 0, 0)
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

            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_RAY_MARCH_STEPS 32
            #define MAX_DISTANCE 4.0
            #define SURFACE_DISTANCE 0.002

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Hit
            {
                float dist;
                float closest_dist;
                float3 p;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _GlowColor;
                float _GlowStrength;
                float _GlowSharpness;
                float _EmissionBoost;
                float _SphereSize;
                float _SphereOpacity;
                float _MainOpacity;
                float4 _Light1Color;
                float _Light1Strength;
                float4 _Light2Color;
                float _Light2Strength;
                float4 _Light3Color;
                float _Light3Strength;
                float _Specular1Enabled;
                float4 _Specular1Color;
                float _Specular1Strength;
                float _Specular1Sharpness;
                float4 _Specular1Position;
                float _Specular2Enabled;
                float4 _Specular2Color;
                float _Specular2Strength;
                float _Specular2Sharpness;
                float4 _Specular2Position;
            CBUFFER_END

            /// <summary>
            /// Vertex shader that creates a camera-facing billboard effect.
            /// Transforms the quad to always face the camera while maintaining its position and scale in world space.
            /// </summary>
            /// <param name="input">Vertex attributes including object-space position and UV coordinates</param>
            /// <returns>Transformed vertex data in clip space with UV coordinates</returns>
            Varyings vert(Attributes input)
            {
                Varyings output;

                // Get the camera position in world space
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 objectPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;

                // Extract scale from the object-to-world matrix
                float3 scaleX = float3(unity_ObjectToWorld[0][0], unity_ObjectToWorld[1][0], unity_ObjectToWorld[2][0]);
                float3 scaleY = float3(unity_ObjectToWorld[0][1], unity_ObjectToWorld[1][1], unity_ObjectToWorld[2][1]);
                float3 scaleZ = float3(unity_ObjectToWorld[0][2], unity_ObjectToWorld[1][2], unity_ObjectToWorld[2][2]);
                float3 scale = float3(length(scaleX), length(scaleY), length(scaleZ));

                // Calculate direction from camera to object (forward for billboard)
                float3 forward = normalize(objectPos - cameraPos);

                // Create a rotation matrix to face the camera
                float3 worldUp = float3(0, 1, 0);
                float3 right = normalize(cross(worldUp, forward));
                float3 up = cross(forward, right);

                // Build billboard matrix with scale (columns are right, up, forward)
                float3x3 billboardMatrix = float3x3(
                    right.x, up.x, forward.x,
                    right.y, up.y, forward.y,
                    right.z, up.z, forward.z
                );

                // Apply scale to the local vertex position, then rotate and translate
                float3 scaledPos = input.positionOS.xyz * scale;
                float3 worldPos = objectPos + mul(billboardMatrix, scaledPos);
                output.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

                output.uv = input.uv;
                return output;
            }

            /// <summary>
            /// Calculates Blinn-Phong specular reflection.
            /// Uses the halfway vector between light and view directions for efficient specular computation.
            /// </summary>
            /// <param name="light_dir">Direction from surface to light source</param>
            /// <param name="ray_dir">Direction from surface to viewer (camera)</param>
            /// <param name="normal">Surface normal at the hit point</param>
            /// <returns>Specular intensity factor (0 to 1)</returns>
            float specularBlinnPhong(float3 light_dir, float3 ray_dir, float3 normal)
            {
                float3 halfway = normalize(light_dir + ray_dir);
                return max(0.0, dot(normal, halfway));
            }

            /// <summary>
            /// Modulo 289 operation for noise generation.
            /// Ensures values wrap around at 289 to maintain noise continuity.
            /// </summary>
            /// <param name="x">Input vector</param>
            /// <returns>Vector with each component modulo 289</returns>
            float4 mod289(float4 x)
            {
                return x - floor(x * (1.0 / 289.0)) * 289.0;
            }

            /// <summary>
            /// Permutation function for procedural noise generation.
            /// Creates pseudo-random values based on input coordinates.
            /// </summary>
            /// <param name="x">Input vector to permute</param>
            /// <returns>Permuted vector for noise calculation</returns>
            float4 perm(float4 x)
            {
                return mod289(((x * 34.0) + 1.0) * x);
            }

            /// <summary>
            /// 3D Perlin-style noise function.
            /// Generates smooth, continuous pseudo-random values for surface displacement.
            /// Uses trilinear interpolation between gradient values at lattice points.
            /// </summary>
            /// <param name="p">3D position to sample noise at</param>
            /// <returns>Noise value typically in range [0, 1]</returns>
            float noise(float3 p)
            {
                float3 a = floor(p);
                float3 d = p - a;
                d = d * d * (3.0 - 2.0 * d);

                float4 b = a.xxyy + float4(0.0, 1.0, 0.0, 1.0);
                float4 k1 = perm(b.xyxy);
                float4 k2 = perm(k1.xyxy + b.zzww);
                float4 c = k2 + a.zzzz;
                float4 k3 = perm(c);
                float4 k4 = perm(c + 1.0);

                float4 o1 = frac(k3 * (1.0 / 41.0));
                float4 o2 = frac(k4 * (1.0 / 41.0));
                float4 o3 = o2 * d.z + o1 * (1.0 - d.z);
                float2 o4 = o3.yw * d.x + o3.xz * (1.0 - d.x);

                return o4.y * d.y + o4.x * (1.0 - d.y);
            }

            /// <summary>
            /// Signed Distance Function (SDF) for an animated, noise-displaced sphere.
            /// Defines the implicit surface by returning the distance to the nearest surface point.
            /// Combines multiple octaves of noise for detailed surface variation and animates over time.
            /// </summary>
            /// <param name="pos">3D position to evaluate the distance field</param>
            /// <returns>Signed distance to surface (negative = inside, positive = outside, 0 = on surface)</returns>
            float SDF(float3 pos)
            {
                float3 p = float3(pos.xy, _Time.y * 0.3 + pos.z);
                float n = (noise(p) + noise(p * 2.0) * 0.5 + noise(p * 4.0) * 0.25) * 0.57;
                return length(pos) - _SphereSize - n * 0.3;
            }

            /// <summary>
            /// Calculates the surface normal using finite differences.
            /// Samples the SDF at nearby points to approximate the gradient (normal direction).
            /// </summary>
            /// <param name="pos">3D position on or near the surface</param>
            /// <returns>Normalized surface normal vector</returns>
            float3 getNormal(float3 pos)
            {
                float2 e = float2(0.002, 0.0);
                float3 n = float3(
                    SDF(pos - e.xyy),
                    SDF(pos - e.yxy),
                    SDF(pos - e.yyx)
                );
                return normalize(SDF(pos) - n);
            }

            /// <summary>
            /// Performs sphere tracing / ray marching through the signed distance field.
            /// Steps along the ray using the SDF value as the safe step distance.
            /// Tracks both the total distance traveled and the closest approach to any surface.
            /// </summary>
            /// <param name="p">Ray origin position</param>
            /// <param name="d">Ray direction (should be normalized)</param>
            /// <returns>Hit structure containing distance traveled, closest approach, and hit position</returns>
            Hit raymarch(float3 p, float3 d)
            {
                Hit hit;
                hit.dist = 0.0;
                hit.closest_dist = MAX_DISTANCE;

                for (int i = 0; i < MAX_RAY_MARCH_STEPS; ++i)
                {
                    float sdf = SDF(p);
                    p += d * sdf;
                    hit.closest_dist = min(hit.closest_dist, sdf);
                    hit.dist += sdf;

                    if (hit.dist >= MAX_DISTANCE || abs(sdf) <= SURFACE_DISTANCE)
                        break;
                }

                hit.p = p;
                return hit;
            }

            /// <summary>
            /// Fragment shader that renders the raymarched sphere with lighting and glow effects.
            /// Casts a ray from the camera through each pixel, marches through the distance field,
            /// and applies multi-directional lighting with specular highlights and atmospheric glow.
            /// </summary>
            /// <param name="input">Interpolated vertex data including UV coordinates</param>
            /// <returns>Final RGBA color for the pixel</returns>
            float4 frag(Varyings input) : SV_Target
            {
                // Convert UV from [0,1] to [-1,1] centered coordinates
                float2 uv = input.uv * 2.0 - 1.0;

                float3 pos = float3(0, 0, -1);
                float3 dir = normalize(float3(uv, 1));

                Hit hit = raymarch(pos, dir);

                // Glow effect based on closest approach to surface
                float glow = pow(max(0.0, 1.0 - hit.closest_dist), _GlowSharpness) * _GlowStrength;
                float3 glowColor = glow * _GlowColor.rgb * (
                    max(0.0, dot(uv, float2(0.707, 0.707))) * _Light1Color.rgb +
                    max(0.0, dot(uv, float2(-0.707, -0.707))) * _Light2Color.rgb +
                    _Light3Color.rgb
                );
                float4 fragColor = float4(glowColor, glow);

                // If ray didn't hit surface, return only glow
                if (hit.closest_dist >= SURFACE_DISTANCE)
                {
                    fragColor.a *= _MainOpacity;
                    return fragColor;
                }

                float3 normal = getNormal(hit.p);
                float3 ray_dir = normalize(pos - hit.p);

                // Light 1 - diagonal from top-right
                float facing = max(0.0, sqrt(dot(normal, float3(0.707, 0.707, 0))) * 1.5 - dot(normal, -dir));
                fragColor = lerp(float4(0, 0, 0, 0), _Light1Color, _Light1Strength * facing * facing * facing);

                // Light 2 - diagonal from bottom-left
                facing = max(0.0, sqrt(dot(normal, float3(-0.707, -0.707, 0))) * 1.5 - dot(normal, -dir));
                fragColor.rgb += lerp(float3(0, 0, 0), _Light2Color.rgb, _Light2Strength * facing * facing * facing);

                // Light 3 - frontal light from camera direction
                facing = max(0.0, sqrt(dot(normal, float3(0.0, 0.0, -1.0))) * 1.5 - dot(normal, -dir));
                fragColor.rgb += lerp(float3(0, 0, 0), _Light3Color.rgb, _Light3Strength * facing * facing * facing);

                // Specular highlight 1 - positioned using _Specular1Position
                if (_Specular1Enabled > 0.5)
                {
                    float spec1 = pow(specularBlinnPhong(
                        normalize(_Specular1Position.xyz - hit.p),
                        ray_dir,
                        normal
                    ), _Specular1Sharpness);
                    fragColor.rgb += lerp(float3(0, 0, 0), _Specular1Color.rgb, spec1 * _Specular1Strength);
                }

                // Specular highlight 2 - positioned using _Specular2Position
                if (_Specular2Enabled > 0.5)
                {
                    float spec2 = pow(specularBlinnPhong(
                        normalize(_Specular2Position.xyz - hit.p),
                        ray_dir,
                        normal
                    ), _Specular2Sharpness);
                    fragColor.rgb += lerp(float3(0, 0, 0), _Specular2Color.rgb, spec2 * _Specular2Strength);
                }

                // Gamma correction for proper color display
                fragColor.rgb = pow(max(fragColor.rgb, 0.0), float3(1.25, 1.25, 1.25));

                // Boost emission to HDR range for bloom effect
                fragColor.rgb *= _EmissionBoost;

                // Apply sphere opacity (only affects the solid sphere, not the glow)
                fragColor.a = _SphereOpacity;

                // Apply main opacity to everything
                fragColor.a *= _MainOpacity;

                return fragColor;
            }
            ENDHLSL
        }
    }
}
