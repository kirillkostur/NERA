Shader "Tutorial/VolumetricFog"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _MaxDistance("Max distance", float) = 100
        _StepSize("Step size", Range(0.1, 20)) = 1
        _MaxSteps("Max raymarch steps", Range(8, 128)) = 64
        _TransmittanceCutoff("Early exit threshold", Range(0.001, 0.1)) = 0.01
        _DensityMultiplier("Density multiplier", Range(0, 10)) = 1
        _NoiseOffset("Noise offset", float) = 0

        _FogNoise("Fog noise", 3D) = "white" {}
        _NoiseTiling("Noise tiling", float) = 1
        _NoiseVelocity("Noise velocity (world units/sec)", Vector) = (1, 0, 0, 0)
        _DensityThreshold("Density threshold", Range(0, 1)) = 0.1

        [HDR]_LightContribution("Light contribution", Color) = (1, 1, 1, 1)
        _LightScattering("Light scattering", Range(0, 1)) = 0.2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _Color;
            float _MaxDistance;
            float _DensityMultiplier;
            float _StepSize;
            float _MaxSteps;
            float _TransmittanceCutoff;
            float _NoiseOffset;
            TEXTURE3D(_FogNoise);
            float _DensityThreshold;
            float _NoiseTiling;
            float4 _NoiseVelocity;
            float4 _LightContribution;
            float _LightScattering;

            #define NERA_MAX_FOG_EXCLUSION_VOLUMES 16
            int _FogExclusionCount;
            float4x4 _FogExclusionWorldToLocal[NERA_MAX_FOG_EXCLUSION_VOLUMES];
            float4 _FogExclusionParameters[NERA_MAX_FOG_EXCLUSION_VOLUMES];

            float henyey_greenstein(float cosineAngle, float scattering)
            {
                float scatteringSquared = scattering * scattering;
                float denominatorBase = max(
                    1.0 + scatteringSquared -
                    2.0 * scattering * cosineAngle,
                    0.0001);
                return (1.0 - scatteringSquared) /
                    (4.0 * PI * pow(denominatorBase, 1.5f));
            }

            float get_fog_visibility(float3 worldPos)
            {
                float visibility = 1.0;
                int volumeCount = min(
                    _FogExclusionCount,
                    NERA_MAX_FOG_EXCLUSION_VOLUMES);

                [loop]
                for (int index = 0; index < volumeCount; index++)
                {
                    float3 boxPosition = mul(
                        _FogExclusionWorldToLocal[index],
                        float4(worldPos, 1.0)).xyz;
                    float3 worldSize =
                        _FogExclusionParameters[index].xyz;
                    float3 offset =
                        (abs(boxPosition) - 0.5) * worldSize;
                    float signedDistance =
                        length(max(offset, 0.0)) +
                        min(max(offset.x, max(offset.y, offset.z)), 0.0);
                    float edgeFade = max(
                        _FogExclusionParameters[index].w,
                        0.0001);
                    float volumeVisibility = smoothstep(
                        0.0,
                        edgeFade,
                        signedDistance);
                    visibility = min(visibility, volumeVisibility);

                    if (visibility <= 0.0)
                        break;
                }

                return visibility;
            }

            float get_density(float3 worldPos)
            {
                float fogVisibility = get_fog_visibility(worldPos);
                if (fogVisibility <= 0.0)
                    return 0.0;

                // The generator stores density in a compact single-channel R8
                // Texture3D, so only the red channel is meaningful.
                // Subtracting velocity makes positive XYZ values move the fog
                // in the same positive world-space direction.
                float3 noisePosition =
                    worldPos - _NoiseVelocity.xyz * _Time.y;
                float noise = _FogNoise.SampleLevel(
                    sampler_TrilinearRepeat,
                    noisePosition * 0.01 * _NoiseTiling,
                    0).r;
                float density = noise;
                density = saturate(density - _DensityThreshold) * _DensityMultiplier;
                return density * fogVisibility;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float4 col = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, IN.texcoord);
                float depth = SampleSceneDepth(IN.texcoord);
                float3 worldPos = ComputeWorldSpacePosition(IN.texcoord, depth, UNITY_MATRIX_I_VP);

                float3 entryPoint = _WorldSpaceCameraPos;
                float3 viewDir = worldPos - _WorldSpaceCameraPos;
                float viewLength = length(viewDir);
                float3 rayDir = normalize(viewDir);

                float2 pixelCoords = IN.texcoord * _BlitTexture_TexelSize.zw;
                float distLimit = min(viewLength, _MaxDistance);
                float stepSize = max(_StepSize, 0.01);
                float jitterDistance = min(max(_NoiseOffset, 0.0), stepSize);
                float distTravelled = InterleavedGradientNoise(
                    pixelCoords,
                    (int)(_Time.y / max(HALF_EPS, unity_DeltaTime.x))) *
                    jitterDistance;
                float transmittance = 1;
                float4 fogCol = _Color;

                Light mainLight = GetMainLight();
                float3 fogLighting =
                    mainLight.color.rgb *
                    _LightContribution.rgb *
                    henyey_greenstein(
                        dot(rayDir, mainLight.direction),
                        _LightScattering);
                int maxSteps = max((int)_MaxSteps, 1);

                [loop]
                for (int stepIndex = 0;
                     stepIndex < maxSteps && distTravelled < distLimit;
                     stepIndex++)
                {
                    float3 rayPos = entryPoint + rayDir * distTravelled;
                    float density = get_density(rayPos);
                    if (density > 0)
                    {
                        half shadowAttenuation = MainLightRealtimeShadow(
                            TransformWorldToShadowCoord(rayPos));
                        fogCol.rgb +=
                            fogLighting * density * shadowAttenuation * stepSize;
                        transmittance *= exp(-density * stepSize);

                        if (transmittance <= _TransmittanceCutoff)
                            break;
                    }

                    distTravelled += stepSize;
                }

                return lerp(col, fogCol, 1.0 - saturate(transmittance));
            }
            ENDHLSL
        }
    }
}
