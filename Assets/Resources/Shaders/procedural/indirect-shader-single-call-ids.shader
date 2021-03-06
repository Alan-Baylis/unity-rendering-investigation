﻿Shader "Indirect Shader Single Call Ids" {
    
    Properties {}
    
    SubShader {

        Tags { "LightMode" = "ForwardBase" }

        Pass {
            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 5.0  
            #pragma vertex vert
            #pragma fragment frag

            struct Point {
                int modelid;
                float3 vertex;
                float3 normal;
            };

            struct Other {
                float4x4 mat;
                float4 color;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float4 id : COLOR;
            };

            uniform fixed4 _LightColor0;
            
            StructuredBuffer<Other> other;
            StructuredBuffer<Point> points;
            int idOffset = 0;

            v2f vert(uint id : SV_VertexID, uint inst : SV_InstanceID)
            {
                v2f o;

                // Position
                int idx = id + (int)idOffset;
                int modelid = points[idx].modelid;
                float4x4 mat = other[modelid].mat;
                float4 pos = float4(points[idx].vertex,1.0f);
                pos = mul(mat, pos);
                pos = UnityObjectToClipPos(pos);
                
                id = floor(idx / 3);
                o.id = float4(
                    ((id >> 0) & 0xFF) / 255.0,
                    ((id >> 8) & 0xFF) / 255.0,
                    ((id >> 16) & 0xFF) / 255.0,
                    ((id >> 24) & 0xFF) / 255.0
                );
                o.pos = pos;
                
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.id;
            }

            ENDCG
        }
    }
}