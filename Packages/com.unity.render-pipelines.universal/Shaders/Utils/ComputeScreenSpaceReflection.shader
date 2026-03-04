Shader "Hidden/Universal Render Pipeline/ComputeScreenSpaceReflection"
{
    HLSLINCLUDE
    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }
        Cull Off
        ZWrite Off
        ZTest Always

        // ------------------------------------------------------------------
        // Screen Space Reflection
        // ------------------------------------------------------------------
        // Main SSR pass
        Pass
        {
            Name "SSR_Main"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment ComputeSSR
                #pragma multi_compile_local_fragment _ _HIZ_TRACE
                #pragma multi_compile_local_fragment _ _REFINE_DEPTH
                #pragma multi_compile_local_fragment _ _USE_MOTION_VECTORS
                #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        // Pass to blit after opaque with alpha blending
        Pass
        {
            Name "SSR_AfterOpaque"

            ZTest Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment CompositeSSRAfterOpaque
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Bilateral Blur
        // ------------------------------------------------------------------
        Pass
        {
            Name "SSR_Bilateral_HorizontalBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment HorizontalBilateralBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SSR_Bilateral_VerticalBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment VerticalBilateralBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SSR_Bilateral_FinalBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FinalBilateralBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Gaussian Blur
        // ------------------------------------------------------------------
        Pass
        {
            Name "SSR_Gaussian_HorizontalBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment HorizontalGaussianBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "SSR_Gaussian_VerticalBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment VerticalGaussianBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }

        // ------------------------------------------------------------------
        // Kawase Blur
        // ------------------------------------------------------------------
        Pass
        {
            Name "SSR_KawaseBlur"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment KawaseBlur
                #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
                #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ComputeScreenSpaceReflection.hlsl"
            ENDHLSL
        }
    }
}
