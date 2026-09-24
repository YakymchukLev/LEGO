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
            Cull Front
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Use smoothed normal if baked in tangentOS, otherwise use vertex normal
                float3 norm = length(input.tangentOS.xyz) > 0.1 ? input.tangentOS.xyz : input.normalOS;
                if (dot(norm, norm) < 0.001) norm = input.normalOS;
                norm = normalize(norm);

                float3 extrudedPosOS = input.positionOS.xyz + norm * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(extrudedPosOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
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
            Cull Front
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
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert(appdata v)
            {
                v2f o;
                float3 norm = length(v.tangent.xyz) > 0.1 ? v.tangent.xyz : v.normal;
                if (dot(norm, norm) < 0.001) norm = v.normal;
                norm = normalize(norm);

                float3 pos = v.vertex.xyz + norm * _OutlineWidth;
                o.pos = UnityObjectToClipPos(pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
