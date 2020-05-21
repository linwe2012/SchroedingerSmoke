// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

// 参考了: https://yumayanagisawa.com/2017/11/21/unity-compute-shader-particle-system/
// 主要使用了代码框架, 内容完全重写

Shader "Unlit/ISFParticle" {
    Properties
    {
        _Scale("Scale", Color) = (1, 1, 1, 1)
        // _Positions("Positions", 1D) = "white" {}
    }
        SubShader{
            Pass {
            Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True"}
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            //Tags{ "RenderType" = "Opaque" }
            LOD 200

            CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma vertex vert
        #pragma fragment frag

        #include "UnityCG.cginc"

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 5.0
        float4 _Scale;

        struct Particle
        {
            float4 position;
        };

        struct PS_INPUT {
            float4 position : SV_POSITION;
            float3 worldNormal : TEXCOORD0;
            float3 worldPos : TEXCOORD1;
        };

        struct VS_INPUT {
            float4 position : POSITION;
            float3 normal: NORMAL;
            float4 texcoord0 : TEXCOORD0;
        };
        // particles' data
        StructuredBuffer<Particle> particleBuffer;


        PS_INPUT vert(VS_INPUT ipt, uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
        {
            PS_INPUT o;

            // Position
            float3 pos = particleBuffer[instance_id].position.xyz * _Scale.xyz + ipt.position.xyz;
            o.position = UnityObjectToClipPos(float4(pos, 1));
            o.worldNormal = UnityObjectToWorldNormal(ipt.normal);
            o.worldPos = mul(unity_ObjectToWorld, pos).xyz;
            return o;
        }

        float4 frag(PS_INPUT i) : COLOR
        {
            float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));
            float scale = dot(-viewDir, i.worldNormal);
            // scale = lerp(0.3, 0.8, scale);
            return float4(0.99, 0.99, 1.0, 0.2);
        }


        ENDCG
        }
    }
        FallBack Off
}
