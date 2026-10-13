Shader "Custom/IceBlock"
{
    Properties
    {
        _IceColor ("Ice Color", Color) = (0.55, 0.88, 1.0, 0.18)
        _BaseColor ("Base Color", Color) = (0.55, 0.88, 1.0, 0.18)
        _Color ("Legacy Color", Color) = (0.55, 0.88, 1.0, 0.18)
        _RimColor ("Rim Color", Color) = (0.92, 0.98, 1.0, 0.85)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.5
        _FrostGlow ("Frost Glow", Range(0.0, 1.0)) = 0.25
        _CrackIntensity ("Crack Intensity", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+60" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        Pass
        {
            Name "IceForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _IceColor;
                float4 _BaseColor;
                float4 _Color;
                float4 _RimColor;
                float _RimPower;
                float _FrostGlow;
                float _CrackIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normWS = normalize(input.normalWS);

                // Fresnel / Rim highlight for crystalline ice edges
                float NdotV = saturate(dot(normWS, viewDirWS));
                float rim = pow(1.0 - NdotV, _RimPower);

                // Procedural internal crystalline frost shimmer based on world coordinates
                float3 p = input.positionWS * 8.0;
                float facet = sin(p.x + p.y * 1.4) * cos(p.z + p.x * 1.2);
                float frostFacet = saturate(facet * 0.5 + 0.5) * _FrostGlow * 0.22;

                // Multi-scale progressive procedural ice fracture lines (Stages 1, 2, 3)
                float3 cp = input.positionWS * 6.5;
                float c1 = abs(sin(cp.x * 2.8 + cp.z * 1.9) * cos(cp.y * 3.5 + cp.x * 1.5));
                float c2 = abs(cos(cp.x * 4.2 - cp.z * 3.7 + cp.y * 2.1));
                float c3 = abs(sin((cp.x + cp.z) * 5.3 + cp.y * 4.0));

                // Stage 1: Primary fissure lines (gentle initial micro-cracks)
                float f1 = smoothstep(0.10, 0.01, c1) * 0.85;
                // Stage 2: Secondary branching fracture web (unlocked as intensity rises)
                float f2 = smoothstep(0.08, 0.01, c2) * saturate((_CrackIntensity - 0.22) * 2.0);
                // Stage 3: Deep shattered micro-fissures (unlocked on low health / final hit)
                float f3 = smoothstep(0.07, 0.01, c3) * saturate((_CrackIntensity - 0.55) * 2.5);

                float totalCracks = saturate((f1 * 0.5 + f2 * 0.65 + f3 * 0.85) * _CrackIntensity * 1.8);

                // Color blend: crystal cyan core, luminous frosty white on rims and progressive cracks
                half4 iceCol = _IceColor;
                if (iceCol.a < 0.001) iceCol = _BaseColor;
                if (iceCol.a < 0.001) iceCol = _Color;
                if (iceCol.a < 0.001) iceCol = half4(0.55, 0.88, 1.0, 0.18);

                half3 rgb = lerp(iceCol.rgb, _RimColor.rgb, saturate(rim + frostFacet + totalCracks));
                half alpha = saturate(iceCol.a + rim * _RimColor.a + frostFacet * 0.30 + totalCracks * 0.60);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "IceForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _IceColor;
                float4 _BaseColor;
                float4 _Color;
                float4 _RimColor;
                float _RimPower;
                float _FrostGlow;
                float _CrackIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normWS = normalize(input.normalWS);

                float NdotV = saturate(dot(normWS, viewDirWS));
                float rim = pow(1.0 - NdotV, _RimPower);

                float3 p = input.positionWS * 8.0;
                float facet = sin(p.x + p.y * 1.4) * cos(p.z + p.x * 1.2);
                float frostFacet = saturate(facet * 0.5 + 0.5) * _FrostGlow * 0.22;

                float3 cp = input.positionWS * 6.5;
                float c1 = abs(sin(cp.x * 2.8 + cp.z * 1.9) * cos(cp.y * 3.5 + cp.x * 1.5));
                float c2 = abs(cos(cp.x * 4.2 - cp.z * 3.7 + cp.y * 2.1));
                float c3 = abs(sin((cp.x + cp.z) * 5.3 + cp.y * 4.0));

                float f1 = smoothstep(0.10, 0.01, c1) * 0.85;
                float f2 = smoothstep(0.08, 0.01, c2) * saturate((_CrackIntensity - 0.22) * 2.0);
                float f3 = smoothstep(0.07, 0.01, c3) * saturate((_CrackIntensity - 0.55) * 2.5);

                float totalCracks = saturate((f1 * 0.5 + f2 * 0.65 + f3 * 0.85) * _CrackIntensity * 1.8);

                half4 iceCol = _IceColor;
                if (iceCol.a < 0.001) iceCol = _BaseColor;
                if (iceCol.a < 0.001) iceCol = _Color;
                if (iceCol.a < 0.001) iceCol = half4(0.55, 0.88, 1.0, 0.18);

                half3 rgb = lerp(iceCol.rgb, _RimColor.rgb, saturate(rim + frostFacet + totalCracks));
                half alpha = saturate(iceCol.a + rim * _RimColor.a + frostFacet * 0.30 + totalCracks * 0.60);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }

        Pass
        {
            Name "IceSRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _IceColor;
                float4 _BaseColor;
                float4 _Color;
                float4 _RimColor;
                float _RimPower;
                float _FrostGlow;
                float _CrackIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float3 normWS = normalize(input.normalWS);

                float NdotV = saturate(dot(normWS, viewDirWS));
                float rim = pow(1.0 - NdotV, _RimPower);

                float3 p = input.positionWS * 8.0;
                float facet = sin(p.x + p.y * 1.4) * cos(p.z + p.x * 1.2);
                float frostFacet = saturate(facet * 0.5 + 0.5) * _FrostGlow * 0.22;

                float3 cp = input.positionWS * 6.5;
                float c1 = abs(sin(cp.x * 2.8 + cp.z * 1.9) * cos(cp.y * 3.5 + cp.x * 1.5));
                float c2 = abs(cos(cp.x * 4.2 - cp.z * 3.7 + cp.y * 2.1));
                float c3 = abs(sin((cp.x + cp.z) * 5.3 + cp.y * 4.0));

                float f1 = smoothstep(0.10, 0.01, c1) * 0.85;
                float f2 = smoothstep(0.08, 0.01, c2) * saturate((_CrackIntensity - 0.22) * 2.0);
                float f3 = smoothstep(0.07, 0.01, c3) * saturate((_CrackIntensity - 0.55) * 2.5);

                float totalCracks = saturate((f1 * 0.5 + f2 * 0.65 + f3 * 0.85) * _CrackIntensity * 1.8);

                half4 iceCol = _IceColor;
                if (iceCol.a < 0.001) iceCol = _BaseColor;
                if (iceCol.a < 0.001) iceCol = _Color;
                if (iceCol.a < 0.001) iceCol = half4(0.55, 0.88, 1.0, 0.18);

                half3 rgb = lerp(iceCol.rgb, _RimColor.rgb, saturate(rim + frostFacet + totalCracks));
                half alpha = saturate(iceCol.a + rim * _RimColor.a + frostFacet * 0.30 + totalCracks * 0.60);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+60" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            float4 _IceColor;
            float4 _BaseColor;
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _FrostGlow;
            float _CrackIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);
                float NdotV = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower);

                float3 p = i.worldPos * 8.0;
                float facet = sin(p.x + p.y * 1.4) * cos(p.z + p.x * 1.2);
                float frostFacet = saturate(facet * 0.5 + 0.5) * _FrostGlow * 0.22;

                float3 cp = i.worldPos * 6.5;
                float c1 = abs(sin(cp.x * 2.8 + cp.z * 1.9) * cos(cp.y * 3.5 + cp.x * 1.5));
                float c2 = abs(cos(cp.x * 4.2 - cp.z * 3.7 + cp.y * 2.1));
                float c3 = abs(sin((cp.x + cp.z) * 5.3 + cp.y * 4.0));

                float f1 = smoothstep(0.10, 0.01, c1) * 0.85;
                float f2 = smoothstep(0.08, 0.01, c2) * saturate((_CrackIntensity - 0.22) * 2.0);
                float f3 = smoothstep(0.07, 0.01, c3) * saturate((_CrackIntensity - 0.55) * 2.5);

                float totalCracks = saturate((f1 * 0.5 + f2 * 0.65 + f3 * 0.85) * _CrackIntensity * 1.8);

                fixed4 iceCol = _IceColor;
                if (iceCol.a < 0.001) iceCol = _BaseColor;
                if (iceCol.a < 0.001) iceCol = _Color;
                if (iceCol.a < 0.001) iceCol = fixed4(0.55, 0.88, 1.0, 0.18);

                fixed3 rgb = lerp(iceCol.rgb, _RimColor.rgb, saturate(rim + frostFacet + totalCracks));
                fixed alpha = saturate(iceCol.a + rim * _RimColor.a + frostFacet * 0.30 + totalCracks * 0.60);

                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }
}

