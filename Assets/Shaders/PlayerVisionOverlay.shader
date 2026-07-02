Shader "AlphaProject/PlayerVisionOverlay"
{
    Properties
    {
        _DarkColor ("Dark Color", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "PlayerVisionOverlay"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _DarkColor;
                float4 _PlayerPosition;
                float4 _CameraWorldPosition;
                float4x4 _InverseViewProjection;
                float2 _ViewForwardXZ;
                float _ViewDistance;
                float _ViewAngle;
                float _NearVisionRadius;
                float _FlashlightEnabled;
                float _FlashlightDistance;
                float _FlashlightAngle;
                float _DarknessAlpha;
                float _Softness;
                float _GroundY;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionHCS);
                return output;
            }

            float3 GetGroundPosition(float2 screenUV)
            {
                float4 farClip = float4(screenUV * 2.0 - 1.0, 1.0, 1.0);
                float4 farWorld = mul(_InverseViewProjection, farClip);
                farWorld.xyz /= max(farWorld.w, 0.00001);

                float3 cameraPosition = _CameraWorldPosition.xyz;
                float3 rayDirection = farWorld.xyz - cameraPosition;
                float denominator = abs(rayDirection.y) < 0.00001 ? -0.00001 : rayDirection.y;
                float rayDistance = (_GroundY - cameraPosition.y) / denominator;
                return cameraPosition + rayDirection * rayDistance;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 groundPosition = GetGroundPosition(screenUV);
                float2 toPoint = groundPosition.xz - _PlayerPosition.xz;
                float distanceToPoint = length(toPoint);

                float nearVisible = 1.0 - smoothstep(
                    max(0.0, _NearVisionRadius - _Softness),
                    _NearVisionRadius,
                    distanceToPoint);

                float2 directionToPoint = distanceToPoint <= 0.00001
                    ? normalize(_ViewForwardXZ)
                    : toPoint / distanceToPoint;

                float2 viewForward = normalize(_ViewForwardXZ);
                float angleDot = dot(viewForward, directionToPoint);
                float baseHalfAngleCos = cos(radians(_ViewAngle * 0.5));
                float baseAngleSoftness = max(0.02, _Softness / max(_ViewDistance, 0.01));
                float baseAngleVisible = smoothstep(baseHalfAngleCos - baseAngleSoftness, baseHalfAngleCos, angleDot);
                float baseRangeVisible = 1.0 - smoothstep(
                    max(0.0, _ViewDistance - _Softness),
                    _ViewDistance,
                    distanceToPoint);

                float flashlightHalfAngleCos = cos(radians(_FlashlightAngle * 0.5));
                float flashlightAngleSoftness = max(0.02, _Softness / max(_FlashlightDistance, 0.01));
                float flashlightAngleVisible = smoothstep(
                    flashlightHalfAngleCos - flashlightAngleSoftness,
                    flashlightHalfAngleCos,
                    angleDot);
                float flashlightRangeVisible = 1.0 - smoothstep(
                    max(0.0, _FlashlightDistance - _Softness),
                    _FlashlightDistance,
                    distanceToPoint);
                float flashlightVisible = _FlashlightEnabled * flashlightAngleVisible * flashlightRangeVisible;

                float visible = saturate(max(max(nearVisible, baseAngleVisible * baseRangeVisible), flashlightVisible));
                float alpha = saturate(_DarknessAlpha * (1.0 - visible));
                return half4(_DarkColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
