// Pálpebras: uma tela cheia preta com uma "abertura" em forma de olho (lente) que cresce com _Open (0 = fechado, 1 = tudo aberto).
// A borda é macia e preta, com cílios irregulares e um sombreado interno que
// escurece e embaça a visão perto das pálpebras. A pálpebra de cima é mais alta que a de baixo. Tudo na placa de vídeo.
Shader "RiftOfTime/EyeLid"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Open ("Open (0-1)", Range(0,1)) = 0
        _Feather ("Feather", Float) = 0.06
        _Aspect ("Aspect", Float) = 1.78
        _Time2 ("Time", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 texcoord : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            float _Open, _Feather, _Aspect, _Time2;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float hash(float x) { return frac(sin(x * 127.1) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float u = (i.uv.x - 0.5) * 2.0;             // -1..1
                float v = (i.uv.y - 0.5) * 2.0;             // -1..1
                float O = saturate(_Open);

                // altura da abertura em cada x (lente): mais alta no meio, e cresce além da tela quando O -> 1
                float h = O * (1.05 - 0.95 * u * u) + 1.7 * O * O * O * O;
                // cílios: a borda de cima fica irregular
                float lash = (hash(floor(u * 70.0)) - 0.5) * 0.035 * smoothstep(0.02, 0.4, O);
                float hTop = h * 1.12 + lash;
                float hBot = h * 0.78;

                float edge = (v >= 0.0) ? (hTop - v) : (hBot + v);      // >0 dentro da abertura; <0 na pálpebra
                float f = max(_Feather, 0.0001);

                // fora (pálpebra): preto puro; dentro da abertura, um sombreado preto suave perto das pálpebras
                float lid = 1.0 - smoothstep(-f * 0.4, f, edge);
                float innerT = (1.0 - smoothstep(0.0, 0.55, max(edge, 0.0))) * step(0.0, edge);
                float a = saturate(max(lid, innerT * 0.6));
                return fixed4(0, 0, 0, a);
            }
            ENDCG
        }
    }
}
