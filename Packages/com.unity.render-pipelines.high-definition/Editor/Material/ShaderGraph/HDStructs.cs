using UnityEditor.ShaderGraph;

namespace UnityEditor.Rendering.HighDefinition.ShaderGraph
{
    static class HDStructs
    {
        public static StructDescriptor AttributesMeshProcedural = new StructDescriptor()
        {
            name = "AttributesMesh",
            packFields = false,
            fields = new FieldDescriptor[]
            {
                HDStructFields.AttributesMesh.instanceID,
                HDStructFields.AttributesMesh.vertexID,
            }
        };

        public static StructDescriptor AttributesMesh = new StructDescriptor()
        {
            name = "AttributesMesh",
            packFields = false,
            fields = new FieldDescriptor[]
            {
                HDStructFields.AttributesMesh.positionOS,
                HDStructFields.AttributesMesh.normalOS,
                HDStructFields.AttributesMesh.tangentOS,
                HDStructFields.AttributesMesh.uv0,
                HDStructFields.AttributesMesh.uv1,
                HDStructFields.AttributesMesh.uv2,
                HDStructFields.AttributesMesh.uv3,
                HDStructFields.AttributesMesh.uv4,
                HDStructFields.AttributesMesh.uv5,
                HDStructFields.AttributesMesh.uv6,
                HDStructFields.AttributesMesh.uv7,
                HDStructFields.AttributesMesh.color,
                HDStructFields.AttributesMesh.instanceID,
                HDStructFields.AttributesMesh.weights,
                HDStructFields.AttributesMesh.indices,
                HDStructFields.AttributesMesh.vertexID,
            }
        };

        public static StructDescriptor VaryingsMeshToPS = new StructDescriptor()
        {
            name = "VaryingsMeshToPS",
            packFields = true,
            populateWithCustomInterpolators = true,
            fields = new FieldDescriptor[]
            {
                HDStructFields.VaryingsMeshToPS.positionCS,
                HDStructFields.VaryingsMeshToPS.positionRWS,
                HDStructFields.VaryingsMeshToPS.positionPredisplacementRWS,
                HDStructFields.VaryingsMeshToPS.normalWS,
                HDStructFields.VaryingsMeshToPS.tangentWS,
                HDStructFields.VaryingsMeshToPS.texCoord0,
                HDStructFields.VaryingsMeshToPS.texCoord1,
                HDStructFields.VaryingsMeshToPS.texCoord2,
                HDStructFields.VaryingsMeshToPS.texCoord3,
                HDStructFields.VaryingsMeshToPS.texCoord4,
                HDStructFields.VaryingsMeshToPS.texCoord5,
                HDStructFields.VaryingsMeshToPS.texCoord6,
                HDStructFields.VaryingsMeshToPS.texCoord7,
                HDStructFields.VaryingsMeshToPS.color,
                HDStructFields.VaryingsMeshToPS.instanceID,
            }
        };

        public static StructDescriptor VaryingsMeshToDS = new StructDescriptor()
        {
            name = "VaryingsMeshToDS",
            packFields = true,
            populateWithCustomInterpolators = true,
            fields = new FieldDescriptor[]
            {
                HDStructFields.VaryingsMeshToDS.positionRWS,
                HDStructFields.VaryingsMeshToDS.positionPredisplacementRWS,
                HDStructFields.VaryingsMeshToDS.tessellationFactor,
                HDStructFields.VaryingsMeshToDS.normalWS,
                HDStructFields.VaryingsMeshToDS.tangentWS,
                HDStructFields.VaryingsMeshToDS.texCoord0,
                HDStructFields.VaryingsMeshToDS.texCoord1,
                HDStructFields.VaryingsMeshToDS.texCoord2,
                HDStructFields.VaryingsMeshToDS.texCoord3,
                HDStructFields.VaryingsMeshToDS.texCoord4,
                HDStructFields.VaryingsMeshToDS.texCoord5,
                HDStructFields.VaryingsMeshToDS.texCoord6,
                HDStructFields.VaryingsMeshToDS.texCoord7,
                HDStructFields.VaryingsMeshToDS.color,
                HDStructFields.VaryingsMeshToDS.instanceID,
            }
        };

        public static StructDescriptor VertexDescriptionInputsProcedural = new StructDescriptor()
        {
            name = "VertexDescriptionInputs",
            packFields = false,
            fields = new FieldDescriptor[]
            {
                StructFields.VertexDescriptionInputs.TimeParameters,
                StructFields.VertexDescriptionInputs.VertexID,
            }
        };
    }
}
