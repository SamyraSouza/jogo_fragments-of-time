// Falha de televisão antiga para uma imagem de UI (o vídeo do menu), feita na placa de vídeo: uma única passada, sem
// gerar malha nem alocar memória, então não trava o vídeo. Fora da rajada (_Glitch = 0) a imagem sai normal.
Shader "RiftOfTime/TvGlitchUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Glitch ("Glitch (0-1)", Range(0,1)) = 0
        _Frame ("Frame", Float) = 0
        _Bands ("Bands", Float) = 90
        _Strength ("Strength", Float) = 0.07
        _Split ("Color split", Float) = 0.006
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Glitch, _Frame, _Bands, _Strength, _Split;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float g = _Glitch;

                // faixas horizontais finas: ondulação por grupos + algumas faixas "quebradas" que saltam
                float band = floor(uv.y * _Bands);
                float wave = (hash(float2(floor(band * 0.25), _Frame)) - 0.5) * 2.0;
                float spike = step(0.74, hash(float2(band * 1.7, _Frame + 3.0)));
                float jump = (hash(float2(band, _Frame)) - 0.5) * 2.0;
                float off = (wave * 0.35 + jump * spike) * _Strength * g;

                // tranco vertical de vez em quando
                float roll = (hash(float2(_Frame, 9.0)) - 0.5) * 0.03 * step(0.6, hash(float2(_Frame, 5.0))) * g;
                uv.x += off;
                uv.y += roll;

                // cores levemente separadas (magenta/verde, como no print)
                float sp = _Split * g;
                float r = tex2D(_MainTex, uv + float2(sp, 0)).r;
                float gg = tex2D(_MainTex, uv).g;
                float b = tex2D(_MainTex, uv - float2(sp, 0)).b;
                fixed4 col = fixed4(r, gg, b, 1) * i.color;

                // linhas de varredura e grão, só durante a falha
                float scan = lerp(1.0, 0.8, step(0.5, frac(uv.y * _Bands * 0.5)) * g);
                float grain = (hash(uv * float2(900, 600) + _Frame) - 0.5) * 0.16 * g;
                col.rgb = col.rgb * scan + grain * g;
                return col;
            }
            ENDCG
        }
    }
}
