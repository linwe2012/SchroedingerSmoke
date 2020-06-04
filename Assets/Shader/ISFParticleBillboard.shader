// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'
// 参考 Shader: https://gist.github.com/kaiware007/8ebad2d28638ff83b6b74970a4f70c9a

// 参考了: https://yumayanagisawa.com/2017/11/21/unity-compute-shader-particle-system/
// 主要使用了代码框架, 内容完全重写

Shader "Unlit/ISFParticle" {
    Properties
    {
        _Scale("Scale", Color) = (1, 1, 1, 1)
        _SmokeTexture("SmokeTexture", 2D) = "white" {}
        _BaseColor("BaseColor", Color) = (0.99, 0.99, 1.0, 0.0)
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
            //#pragma multi_compile_fwdbase

            #include "UnityCG.cginc"
            //#include "AutoLight.cginc"

            // Use shader model 3.0 target, to get nicer looking lighting
            #pragma target 5.0
            float4 _Scale;
            float4 _BaseColor;

            struct Particle
            {
                float4 position;
            };

            struct PS_INPUT {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                //LIGHTING_COORDS(3, 4)
            };

            struct VS_INPUT {
                float4 position : POSITION;
                float4 texcoord0 : TEXCOORD0;
            };
            // particles' data
            StructuredBuffer<Particle> particleBuffer;
            sampler2D _SmokeTexture;


            PS_INPUT vert(VS_INPUT ipt, uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                PS_INPUT o;

                // Position
                float3 pos = particleBuffer[instance_id].position.xyz * _Scale.xyz + ipt.position.xyz;
                // o.position = UnityObjectToClipPos(float4(pos, 1));
                o.uv = ipt.texcoord0.xy;

                float3 vpos = mul((float3x3)unity_ObjectToWorld, pos);
                float4 worldCoord = float4(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23, 1);
                float4 viewPos = mul(UNITY_MATRIX_V, worldCoord) + float4(vpos, 0);
                float4 outPos = mul(UNITY_MATRIX_P, viewPos);
                o.position = outPos;
                //TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            float4 frag(PS_INPUT i) : COLOR
            {
                float4 color = tex2D(_SmokeTexture, i.uv);

                // scale = lerp(0.3, 0.8, scale);
                // return float4(0.99, 0.99, 1.0, 0.01);
                float alpha = (color.r + color.g + color.b - 0.3f) * 0.0005f;
                alpha = clamp(alpha, 0, 1);
                //float atten = LIGHT_ATTENUATION(i);
                return float4(_BaseColor.rgb, alpha);
            }


            ENDCG
            }
        }
            FallBack Off
}


/*
Shader "Unlit/ISFParticleBillboard"
{
    Properties
    {
        _Scale("Scale", Color) = (1, 1, 1, 1)
        _SmokeTexture("SmokeTexture", 2D) = "white" {}
        _BaseColor("BaseColor", Color) = (0.99, 0.99, 1.0, 0.0)
    }
    SubShader
    {
            // "DisableBatching" = "True"
        Tags{ "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent"  }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
       
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                // UNITY_FOG_COORDS(1)
                float4 position : SV_POSITION;
            };

            struct Particle
            {
                float4 position;
            };

            float4 _Scale;
            float4 _BaseColor;
            StructuredBuffer<Particle> particleBuffer;
            sampler2D _SmokeTexture;

            v2f vert (appdata v, uint vertex_id : SV_VertexID, uint instance_id : SV_InstanceID)
            {
                v2f o;
                float3 vertex = particleBuffer[instance_id].position.xyz * _Scale.xyz + v.vertex.xyz;
                // o.vertex = UnityObjectToClipPos(float4(vertex, 0));
                o.uv = v.uv.xy; // + float2((instance_id >> 8), instance_id & (128-1)) / 912;

                // billboard mesh towards camera
                //float3 vpos = mul((float3x3)unity_ObjectToWorld, vertex);
                //float4 worldCoord = float4(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23, 1);
                //float4 viewPos = mul(UNITY_MATRIX_V, worldCoord) + float4(vpos, 0);
                //float4 outPos = mul(UNITY_MATRIX_P, viewPos);
                //o.position = outPos;

                float3 pos = particleBuffer[instance_id].position.xyz * _Scale.xyz + v.vertex.xyz;
                o.position = UnityObjectToClipPos(float4(pos, 1));
                

                // UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                // fixed4 col = tex2D(_MainTex, i.uv);
                // apply fog
               //  UNITY_APPLY_FOG(i.fogCoord, col);
                float4 color = tex2D(_SmokeTexture, i.uv);
                float alpha = (color.r + color.g + color.b - 0.3f) * 0.0005f;
                alpha = clamp(alpha, 0, 1);

                return float4(_BaseColor.rgb, alpha);
            }
            ENDCG
        }
        FallBack Off
    }
}
*/