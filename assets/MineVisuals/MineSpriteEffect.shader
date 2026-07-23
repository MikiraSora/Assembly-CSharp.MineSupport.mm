Shader "MineSupport/MineSpriteEffect"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _GrayFloor("Gray Floor", Range(0, 1)) = 0.58
        _GrayCeiling("Gray Ceiling", Range(0, 1)) = 1
        _HatchScale("Hatch Scale", Range(1, 32)) = 9
        _HatchStrength("Hatch Strength", Range(0, 1)) = 0.72
        _HatchDark("Hatch Dark", Range(0, 1)) = 0.18
        _OutlineStrength("Outline Strength", Range(0, 1)) = 0.85
        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment MineSpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed _GrayFloor;
            fixed _GrayCeiling;
            fixed _HatchScale;
            fixed _HatchStrength;
            fixed _HatchDark;
            fixed _OutlineStrength;
            float4 _MainTex_TexelSize;

            fixed4 MineSpriteFrag(v2f input) : SV_Target
            {
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                fixed luminance = dot(color.rgb, fixed3(0.2126, 0.7152, 0.0722));
                fixed gray = lerp(_GrayFloor, _GrayCeiling, saturate(luminance));

                fixed hatch = step(0.82, frac((input.texcoord.x + input.texcoord.y) * _HatchScale));
                gray = lerp(gray, _HatchDark, hatch * _HatchStrength);

                float2 texel = _MainTex_TexelSize.xy;
                fixed neighborAlpha = min(
                    min(SampleSpriteTexture(input.texcoord + float2(texel.x, 0)).a,
                        SampleSpriteTexture(input.texcoord - float2(texel.x, 0)).a),
                    min(SampleSpriteTexture(input.texcoord + float2(0, texel.y)).a,
                        SampleSpriteTexture(input.texcoord - float2(0, texel.y)).a));
                fixed innerEdge = saturate((color.a - neighborAlpha) * 4);
                gray = lerp(gray, 1, innerEdge * _OutlineStrength);

                color.rgb = gray * color.a;
                return color;
            }
            ENDCG
        }
    }
}
