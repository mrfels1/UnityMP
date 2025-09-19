Shader "Unlit/DiagonalSplitCombine"
{
    Properties
    {
        _TexA ("Texture A", 2D) = "black" {}
        _TexB ("Texture B", 2D) = "black" {}
        _Point ("Line Point (uv)", Vector) = (0.5,0.5,0,0)
        _Normal("Line Normal (uv)", Vector) = (1,0,0,0)
        _Feather ("Feather (uv)", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        ZWrite Off Cull Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TexA; sampler2D _TexB;
            float4 _Point; // xy
            float4 _Normal; // xy normalized
            float _Feather;

            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(appdata v){ v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i):SV_Target
            {
                float2 uv = i.uv;
                float d = dot(uv - _Point.xy, _Normal.xy);
                float w = smoothstep(-_Feather, _Feather, d); // 0..1 зона смешения
                fixed4 ca = tex2D(_TexA, uv);
                fixed4 cb = tex2D(_TexB, uv);
                return lerp(ca, cb, w);
            }
            ENDCG
        }
    }
}
