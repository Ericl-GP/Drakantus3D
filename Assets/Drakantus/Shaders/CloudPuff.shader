// Nuvem low-poly: sem neblina (continua branca ao longe), sem projetar sombra, iluminação suave de 2 cores.
// Compatível com o SRP Batcher (centenas de nuvens = poucos draw calls).
Shader "Drakantus/CloudPuff"
{
    Properties
    {
        _BaseColor ("Cor iluminada", Color) = (1,1,1,1)
        _ShadeColor ("Cor da sombra", Color) = (0.64,0.70,0.82,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadeColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            Varyings vert (Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light l = GetMainLight();
                float wrap = dot(n, l.direction) * 0.5 + 0.5;   // lado do sol
                float top  = n.y * 0.5 + 0.5;                    // topo mais claro que a barriga
                float t = smoothstep(0.30, 0.80, saturate(wrap * 0.65 + top * 0.35));
                half3 col = lerp(_ShadeColor.rgb, _BaseColor.rgb, t);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
