Shader "Custom/BlockOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 1, 1, 0.95)
        _OutlineWidth ("Outline Width", Range(0.005, 0.2)) = 0.045
    }
    SubShader
    {
        Tags 
        { 
            "RenderType"="Transparent" 
            "Queue"="Transparent+50" 
            "RenderPipeline"="UniversalPipeline" 
        }
        
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
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
                float4 tangentOS  : TANGENT; // Averaged smoothed normal for seamless hard edges
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            float3 OutlineNormal(float3 normalOS, float4 tangentOS)
            {
                float3 norm = length(tangentOS.xyz) > 0.1 ? tangentOS.xyz : normalOS;
                if (dot(norm, norm) < 0.001) norm = normalOS;
                return normalize(norm);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 norm = OutlineNormal(input.normalOS, input.tangentOS);
                float3 extrudedPosOS = input.positionOS.xyz + norm * _OutlineWidth;
                float3 positionWS = TransformObjectToWorld(extrudedPosOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(norm);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Грані, розвернуті до камери, закривають блок білим. Лишаємо лише край силуету.
                // Світова норма враховує від’ємний масштаб, тож дзеркало не ламає перевірку.
                float facing = dot(normalize(input.normalWS), normalize(input.viewDirWS));
                if (facing > 0.05)
                    discard;

                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "OutlineForwardOnly"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Cull Off
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
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            float3 OutlineNormal(float3 normalOS, float4 tangentOS)
            {
                float3 norm = length(tangentOS.xyz) > 0.1 ? tangentOS.xyz : normalOS;
                if (dot(norm, norm) < 0.001) norm = normalOS;
                return normalize(norm);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 norm = OutlineNormal(input.normalOS, input.tangentOS);
                float3 extrudedPosOS = input.positionOS.xyz + norm * _OutlineWidth;
                float3 positionWS = TransformObjectToWorld(extrudedPosOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(norm);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float facing = dot(normalize(input.normalWS), normalize(input.viewDirWS));
                if (facing > 0.05)
                    discard;

                return _OutlineColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "OutlineSRPDefaultUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
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
                float4 tangentOS  : TANGENT;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            float3 OutlineNormal(float3 normalOS, float4 tangentOS)
            {
                float3 norm = length(tangentOS.xyz) > 0.1 ? tangentOS.xyz : normalOS;
                if (dot(norm, norm) < 0.001) norm = normalOS;
                return normalize(norm);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 norm = OutlineNormal(input.normalOS, input.tangentOS);
                float3 extrudedPosOS = input.positionOS.xyz + norm * _OutlineWidth;
                float3 positionWS = TransformObjectToWorld(extrudedPosOS);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(norm);
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float facing = dot(normalize(input.normalWS), normalize(input.viewDirWS));
                if (facing > 0.05)
                    discard;

                return _OutlineColor;
            }
            ENDHLSL
        }
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = length(v.tangent.xyz) > 0.1 ? v.tangent.xyz : v.normal;
                if (dot(norm, norm) < 0.001) norm = v.normal;
                norm = normalize(norm);

                float3 extruded = v.vertex.xyz + norm * _OutlineWidth;
                float4 worldPos = mul(unity_ObjectToWorld, float4(extruded, 1.0));
                o.pos = UnityObjectToClipPos(float4(extruded, 1.0));
                o.normalWS = UnityObjectToWorldNormal(norm);
                o.viewDirWS = _WorldSpaceCameraPos.xyz - worldPos.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = dot(normalize(i.normalWS), normalize(i.viewDirWS));
                if (facing > 0.05)
                    discard;

                return _OutlineColor;
            }
            ENDCG
        }
    }
}
