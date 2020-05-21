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
            Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
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
        };
        // particles' data
        StructuredBuffer<Particle> particleBuffer;


        PS_INPUT vert(uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
        {
            PS_INPUT o;

            // Position
            o.position = UnityObjectToClipPos(float4(particleBuffer[instance_id].position.xyz * _Scale.xyz, 1));

            return o;
        }

        float4 frag(PS_INPUT i) : COLOR
        {
            return float4(0.6, 0.6, 0.65, 0.4);
        }


        ENDCG
        }
    }
        FallBack Off
}
