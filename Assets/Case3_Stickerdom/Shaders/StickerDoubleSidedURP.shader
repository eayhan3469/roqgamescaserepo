Shader "Custom/StickerDoubleSidedURP"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _BaseMap ("Base Map", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1, 1, 1, 1)
        _BackSideColor ("Backside Adhesive Color", Color) = (0.90, 0.91, 0.93, 1.0)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.05
        _ShineProgress ("Shine Ray Progress", Range(-0.5, 1.5)) = -0.5
        _ShineColor ("Shine Ray Color", Color) = (1.0, 1.0, 1.0, 0.85)

        [Header(Backside Multi Stage Gradient Live Controls)]
        _CreaseColor ("1 Crease Black Color", Color) = (0.0, 0.0, 0.0, 1.0)
        _PeakColor ("2 Apex Peak White Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _MidDipColor ("3 Mid Dip Dark Gray Color", Color) = (0.28, 0.30, 0.36, 1.0)
        _EndToneColor ("4 Body Gradient Tone Color", Color) = (0.80, 0.82, 0.88, 1.0)
        _TipColor ("5 Flap Tip Final Color", Color) = (0.98, 0.98, 1.00, 1.0)
        _CreasePosition ("1 Crease End Position", Range(0.00, 2.00)) = 0.4
        _PeakPosition ("2 Apex Peak Position", Range(0.00, 2.00)) = 0.7
        _DipPosition ("3 Mid Dip Position", Range(0.00, 2.00)) = 0.989
        _TailPosition ("4 Body Tail Position", Range(0.00, 2.00)) = 1.253
        _ApexGlossIntensity ("Apex Gloss Intensity", Range(0.0, 3.0)) = 0.05
        [Toggle] _InvertGradient ("Invert Gradient Direction", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"="TransparentCutout"
            "Queue"="AlphaTest+50"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
        }

        Cull Off
        Lighting Off
        ZWrite On
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "DoubleSidedPass"

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _BackSideColor;
                float4 _ShineColor;
                float4 _CreaseColor;
                float4 _PeakColor;
                float4 _MidDipColor;
                float4 _EndToneColor;
                float4 _TipColor;
                float _Cutoff;
                float _ShineProgress;
                float _CreasePosition;
                float _PeakPosition;
                float _DipPosition;
                float _TailPosition;
                float _ApexGlossIntensity;
                float _InvertGradient;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (texCol.a < _Cutoff) discard;

                // Masada kalan görünmez arka yüzey poligonlarını anında discard et
                if (input.color.a < 0.05f) discard;

                // input.color.g:
                // > 0.5 = ÖN YÜZEY (Front Sheet: %100 saf renkli görsel)
                // <= 0.5 = ARKA YÜZEY (Back Sheet: Sökülen kanattaki yapışkan kağıt)
                if (input.color.g > 0.5f)
                {
                    // 1. ÖN YÜZ: %100 DOKUNULMAZ, SAF ORİJİNAL STICKER GÖRSELİ (SIFIR GÖLGE, SIFIR ETKİ)
                    float3 frontRgb = texCol.rgb * _Color.rgb;

                    // Diagonal Shine Ray / Gloss Light Sweep across sticker on landing
                    if (_ShineProgress > -0.4f && _ShineProgress < 1.4f)
                    {
                        float rayPos = (input.uv.x + input.uv.y) * 0.5f;
                        float rayDist = abs(rayPos - _ShineProgress);
                        float rayIntensity = saturate(1.0f - rayDist / 0.12f);
                        rayIntensity = pow(rayIntensity, 2.0f);

                        frontRgb += _ShineColor.rgb * (rayIntensity * _ShineColor.a);
                    }

                    return float4(frontRgb, texCol.a * _Color.a);
                }
                else
                {
                    // 2. SADECE HAVAYA KALKAN SÖKÜLMÜŞ ARKA KANAT (Adhesive Backside):
                    // Birebir Çizdiğiniz Çok Kademeli Degrade (Sabit Fiziksel Uzaklık / World-Space Distance):
                    float u = max(input.color.r, 0.0f);
                    if (_InvertGradient > 0.5f)
                    {
                        u = max(_TailPosition - u, 0.0f);
                    }

                    float3 colBlack   = _CreaseColor.rgb;
                    float3 colWhite   = _PeakColor.rgb;
                    float3 colDarkGray = _MidDipColor.rgb;
                    float3 colEndTone = _EndToneColor.rgb;
                    float3 colTip     = _TipColor.rgb;

                    float cPos = max(_CreasePosition, 0.0f);
                    float pPos = max(_PeakPosition, cPos + 0.001f);
                    float dPos = max(_DipPosition, pPos + 0.001f);
                    float tPos = max(_TailPosition, dPos + 0.001f);

                    float3 backRgb;

                    if (u < cPos)
                    {
                        // 0 -> cPos: Zemin Temas Çizgisi TAM SİYAH (Sabit Fiziksel Genişlik)
                        backRgb = colBlack;
                    }
                    else if (u < pPos)
                    {
                        // cPos -> pPos: Zemin Temas Çizgisinden Tepe Noktasına (Siyah -> Beyaz)
                        float t = (u - cPos) / (pPos - cPos);
                        backRgb = lerp(colBlack, colWhite, t);
                    }
                    else if (u < dPos)
                    {
                        // pPos -> dPos: Tepe Noktasından Koyu Griye İniş (Beyaz -> Koyu Gri)
                        float t = (u - pPos) / (dPos - pPos);
                        backRgb = lerp(colWhite, colDarkGray, t);
                    }
                    else if (u < tPos)
                    {
                        // dPos -> tPos: Koyu Griden Gövde Tonuna Degrade (Koyu Gri -> Gövde Tonu)
                        float t = (u - dPos) / (tPos - dPos);
                        backRgb = lerp(colDarkGray, colEndTone, t);
                    }
                    else
                    {
                        // tPos+: Gövdeden Uca Beyaz
                        backRgb = colTip;
                    }

                    // Tepe noktası silindirik parlama vurgusu
                    float apexGloss = saturate(input.color.b);
                    backRgb = saturate(backRgb + float3(0.50f, 0.50f, 0.50f) * _ApexGlossIntensity * pow(apexGloss, 2.0f));

                    return float4(saturate(backRgb * _Color.rgb), texCol.a * _BackSideColor.a * _Color.a * input.color.a);
                }
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
