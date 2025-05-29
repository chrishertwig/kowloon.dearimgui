Shader "Unlit/DearImGuiURP_hlsl"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }
        LOD 100

        Lighting Off
        Cull Off ZWrite On ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            PackageRequirements
            {
                "com.unity.render-pipelines.universal" : "10.0"
                "unity" : "2020.1"
            }
            Name "KOWLOON DEARIMGUI URP"

            HLSLPROGRAM
            #pragma vertex ImGuiPassVertex
            #pragma fragment ImGuiPassFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #ifndef UNITY_COLORSPACE_GAMMA
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #endif

            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);

            struct ImVert // same layout as ImDrawVert
            {
                float2 vertex : POSITION;
                float2 uv : TEXCOORD0;
                uint color : TEXCOORD1; // gets reordered when using COLOR semantics
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            half4 unpack_color(uint c)
            {
                half4 color = half4(
                    (c) & 0xff,
                    (c >> 8) & 0xff,
                    (c >> 16) & 0xff,
                    (c >> 24) & 0xff
                ) / 255;
                #ifndef UNITY_COLORSPACE_GAMMA
                color.rgb = FastSRGBToLinear(color.rgb);
                #endif
                return color;
            }

            Varyings ImGuiPassVertex(ImVert input)
            {
                Varyings output;
                output.vertex = TransformWorldToHClip(TransformObjectToWorld(float3(input.vertex, 0.0)));
                output.uv = float2(input.uv.x, 1 - input.uv.y);
                output.color = unpack_color(input.color);
                return output;
            }

            half4 ImGuiPassFrag(Varyings input) : SV_Target
            {
                return input.color * SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv);
            }
            ENDHLSL
        }
    }
}