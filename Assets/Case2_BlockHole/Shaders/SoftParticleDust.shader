Shader "BlockHole/SoftParticleDust"
{
    Properties
    {
        _Softness ("Softness Falloff", Range(1.0, 10.0)) = 3.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline"="UniversalPipeline" 
            "IgnoreProjector"="True" 
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            Name "SoftDustPass"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Radial distance from center [-1, 1]
                float2 centerOffset = (i.texcoord - float2(0.5, 0.5)) * 2.0;
                float dist = length(centerOffset);

                // High-visibility soft puff: solid visible body in center, velvety smooth falloff to outer edge
                float radialAlpha = smoothstep(1.0, 0.20, dist);

                fixed4 col = i.color;
                col.a *= radialAlpha;

                return col;
            }
            ENDCG
        }
    }
}
