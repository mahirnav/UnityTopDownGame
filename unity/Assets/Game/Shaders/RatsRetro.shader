// Post effect for the world camera (HDR in, display out):
//   pass 0 bloom prefilter (bright parts only), pass 1 downsample, pass 2 upsample (additive),
//   pass 3 composite: bloom + exposure, ACES tone mapping, warm grade, freeze tint, and the optional
//   PS2 look (banding + ordered dither; pixelation happens before this pass).
Shader "Hidden/RatsRetro"
{
    Properties { _MainTex ("Texture", 2D) = "white" {} }

    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    half3 Box(float2 uv, float offset)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-offset, -offset, offset, offset);
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass // 0: prefilter
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            float _Threshold;
            half4 frag(v2f_img i) : SV_Target
            {
                half3 c = Box(i.uv, 1.0);
                half brightness = max(c.r, max(c.g, c.b));
                half soft = clamp(brightness - _Threshold + 0.5, 0, 1);
                soft = soft * soft * 0.5;
                half contribution = max(soft, brightness - _Threshold) / max(brightness, 1e-4);
                return half4(c * contribution, 1);
            }
            ENDCG
        }

        Pass // 1: downsample
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(Box(i.uv, 1.0), 1); }
            ENDCG
        }

        Pass // 2: upsample, added onto the bigger level
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            half4 frag(v2f_img i) : SV_Target { return half4(Box(i.uv, 0.5), 1); }
            ENDCG
        }

        Pass // 3: composite
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            sampler2D _BloomTex;
            float _Bloom;       // bloom intensity
            float _Exposure;
            float _Tonemap;     // 1 = ACES, 0 = clamp
            float4 _LowRes;     // xy = image size in pixels (for dither)
            float _Levels;      // color steps per channel, 0 = off
            float _Dither;
            float _Freeze;      // 0..1 icy desaturation
            float3 _Tint;
            float _Saturation;

            float Bayer4(int2 p)
            {
                const float4x4 m = float4x4(0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5);
                return m[p.y][p.x] / 16.0 - 0.5;
            }

            float3 Aces(float3 x) // Narkowicz fit
            {
                return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb + tex2D(_BloomTex, i.uv).rgb * _Bloom;
                c *= _Exposure;
                c = _Tonemap > 0.5 ? Aces(c) : saturate(c);

                float gray = dot(c, float3(0.299, 0.587, 0.114));
                c = saturate(lerp(float3(gray, gray, gray), c, _Saturation) * _Tint);
                gray = dot(c, float3(0.299, 0.587, 0.114));
                c = lerp(c, float3(gray * 0.82, gray * 0.95, gray * 1.15), _Freeze);
                if (_Levels > 0)
                {
                    int2 p = int2(fmod(floor(i.uv * _LowRes.xy), 4.0));
                    c = floor(c * _Levels + Bayer4(p) * _Dither + 0.5) / _Levels;
                }
                return fixed4(c, 1);
            }
            ENDCG
        }
    }
}
