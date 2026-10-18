// PS2-era post effect: the image arrives already downscaled (point-filtered), then gets color banding with
// ordered dithering in low-res pixel space, plus an icy desaturation during freezes.
Shader "Hidden/RatsRetro"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _LowRes;   // xy = low-res size in pixels
            float _Levels;    // color steps per channel, 0 = off
            float _Dither;    // dither strength
            float _Freeze;    // 0..1 desaturate toward icy gray

            float Bayer4(int2 p)
            {
                const float4x4 m = float4x4(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
                return m[p.y][p.x] / 16.0 - 0.5;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                float gray = dot(c.rgb, float3(0.299, 0.587, 0.114));
                c.rgb = lerp(c.rgb, float3(gray * 0.82, gray * 0.95, gray * 1.15), _Freeze);
                if (_Levels > 0)
                {
                    int2 p = int2(fmod(floor(i.uv * _LowRes.xy), 4.0));
                    c.rgb = floor(c.rgb * _Levels + Bayer4(p) * _Dither + 0.5) / _Levels;
                }
                return c;
            }
            ENDCG
        }
    }
}
