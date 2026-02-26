using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace UnityEditor.ShaderGraph
{
    class UITKPreviewTarget : PreviewTarget
    {
        static readonly GUID kSourceCodeGuid = new GUID("25a52015b0f83494a824ccc98f1854d1"); // UITKPreviewTarget.cs

        public UITKPreviewTarget()
        {
            displayName = "Preview";
            isHidden = true;
        }

        public override void Setup(ref TargetSetupContext context)
        {
            context.AddAssetDependency(kSourceCodeGuid, AssetCollection.Flags.SourceDependency);
            context.AddSubShader(SubShaders.Preview);
        }

        static class SubShaders
        {
            public static SubShaderDescriptor Preview = new SubShaderDescriptor()
            {
                renderQueue = "Geometry",
                renderType = "Opaque",
                generatesPreview = true,
                passes = new PassCollection { Passes.Preview },
            };
        }

        static class Passes
        {
            public static PassDescriptor Preview = new PassDescriptor()
            {
                // Definition
                referenceName = "SHADERPASS_PREVIEW",
                useInPreview = true,

                // Templates
                passTemplatePath = GenerationUtils.GetDefaultTemplatePath("PassMesh.template"),
                sharedTemplateDirectories = GenerationUtils.GetDefaultSharedTemplateDirectories(),

                // Collections
                structs = new StructCollection
                {
                    { Structs.Attributes },
                    { StructDescriptors.PreviewVaryings },
                    { Structs.SurfaceDescriptionInputs },
                    { Structs.VertexDescriptionInputs },
                },
                fieldDependencies = FieldDependencies.Default,
                pragmas = new PragmaCollection
                {
                    { Pragma.Vertex("vert") },
                    { Pragma.Fragment("frag") },
                },
                defines = new DefineCollection
                {
                    { KeywordDescriptors.PreviewKeyword, 1 },
                },
                includes = new IncludeCollection
                {
                    // Pre-graph
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl", IncludeLocation.Pregraph },       // TODO: put this on a conditional
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/NormalSurfaceGradient.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.render-pipelines.core/ShaderLibrary/EntityLighting.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariables.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.shadergraph/ShaderGraphLibrary/ShaderVariablesFunctions.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.shadergraph/ShaderGraphLibrary/Functions.hlsl", IncludeLocation.Pregraph },
                    { "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/UIShim.hlsl", IncludeLocation.Pregraph },

                    // Post-graph
                    { "Packages/com.unity.shadergraph/ShaderGraphLibrary/PreviewVaryings.hlsl", IncludeLocation.Postgraph },
                    { "Packages/com.unity.shadergraph/ShaderGraphLibrary/PreviewPass.hlsl", IncludeLocation.Postgraph },
                }
            };
        }
    }
}
