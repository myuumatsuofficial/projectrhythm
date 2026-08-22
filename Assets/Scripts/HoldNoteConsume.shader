Shader "Custom/HoldNoteConsume"
{
    // Shader ini TIDAK mengubah mesh/transform sama sekali.
    // Bagian yang sudah "dikonsumsi" akan di-fade alpha-nya menjadi transparan
    // berdasarkan _ConsumeAmount (0 = belum ada yang terkonsumsi, 1 = full terkonsumsi).
    //
    // Progress axis default: UV.y mesh (0 di salah satu ujung note, 1 di ujung lain).
    // Jika mesh capsule/cylinder custom kamu UV-nya tidak rapi 0->1 di sepanjang note,
    // lihat komentar "OBJECT SPACE ALTERNATIVE" di bagian fragment shader.

    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)

        _ConsumeAmount ("Consume Amount (0-1)", Range(0,1)) = 0
        _FadeWidth ("Fade Edge Softness", Range(0.001, 0.5)) = 0.06

        // 1 = konsumsi mengarah dari UV.y=0 ke UV.y=1
        // -1 = kebalikannya (dari UV.y=1 ke UV.y=0)
        _ConsumeDirection ("Consume Direction (1 or -1)", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionOS  : TEXCOORD1; // untuk alternatif object-space
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _ConsumeAmount;
                float _FadeWidth;
                float _ConsumeDirection;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.positionOS = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 col = texColor * _BaseColor;

                // === UV-BASED PROGRESS (default) ===
                float t = (_ConsumeDirection > 0) ? IN.uv.y : (1.0 - IN.uv.y);

                // === OBJECT SPACE ALTERNATIVE ===
                // Kalau UV mesh capsule/cylinder kamu tidak reliable (misal ada distorsi di ujung),
                // ganti baris "float t = ..." di atas dengan versi ini, dan set
                // _MinLocalY / _MaxLocalY sesuai bounds lokal mesh note kamu (bisa dikirim dari C#):
                //
                // float t = saturate((IN.positionOS.y - _MinLocalY) / (_MaxLocalY - _MinLocalY));
                // if (_ConsumeDirection < 0) t = 1.0 - t;

                // Soft edge fade di batas konsumsi, biar tidak ada garis tegas/aliasing
                half edge = smoothstep(_ConsumeAmount - _FadeWidth, _ConsumeAmount + _FadeWidth, t);

                col.a *= edge;

                clip(col.a - 0.001); // buang fragment yang sudah full transparan (hemat overdraw)

                return col;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
