Shader "RiftOfTime/ShineStripes"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.96, 0.85, 1)
        _Scale ("Stripe density", Float) = 5
        _Speed ("Sweep speed", Float) = 0.3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Scale;
                float _Speed;
            CBUFFER_END

            struct A { float3 pos : POSITION; };
            struct V { float4 cs : SV_POSITION; };

            V vert(A i)
            {
                V o;
                o.cs = TransformObjectToHClip(i.pos);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                // faixas diagonais "/ /" em espaço de tela, que varrem o objeto
                float2 sp = i.cs.xy / _ScreenParams.y;
                float t = frac((sp.x + sp.y) * _Scale * 0.5 - _Time.y * _Speed);
                float a = smoothstep(0.00, 0.03, t) * (1.0 - smoothstep(0.09, 0.12, t));
                float b = smoothstep(0.17, 0.19, t) * (1.0 - smoothstep(0.21, 0.23, t));
                return half4(_Color.rgb, saturate(a + b * 0.8) * _Color.a);
            }
            ENDHLSL
        }
    }
}
