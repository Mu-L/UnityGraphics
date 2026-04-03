Shader "CustomFetchDepth"
{
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"                 
        }

        Pass
        {
            Name "CustomFetchDepth"

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragDepthVisualization
            #pragma target 4.5
            #pragma multi_compile _ DEPTH_AS_INPUT_ATTACHMENT DEPTH_AS_INPUT_ATTACHMENT_MSAA
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            // -------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

#if defined(DEPTH_AS_INPUT_ATTACHMENT)
            float4 FragDepthVisualization(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float depth = FetchSceneDepth(input.positionCS.xy);
                return float4(depth, 0.0f, 0.0f, 1.0);
            }
#elif defined(DEPTH_AS_INPUT_ATTACHMENT_MSAA)    
            float4 FragDepthVisualization(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Read depth directly from the framebuffer at the current fragment location
                // No texture sampling required - this reads from the input attachment
                float depth = FetchSceneDepth(input.positionCS.xy, 0);
                return float4(depth, 0.0f, 0.0f, 1.0);
            }
#else

            float4 FragDepthVisualization(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // no fallback    
                float depth = 0;

                return float4(depth, 0.0f, 0.0f, 1.0);
            }
#endif
            ENDHLSL
        }        
    }
}
