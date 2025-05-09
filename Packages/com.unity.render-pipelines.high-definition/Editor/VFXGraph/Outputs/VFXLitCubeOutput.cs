using System.Collections.Generic;
using System.Linq;

namespace UnityEditor.VFX.HDRP
{
    [VFXInfo(name = "Output Particle|HDRP Lit|Cube", category = "#5Output Debug", experimental = true, synonyms = new []{ "Box" })]
    class VFXLitCubeOutput : VFXAbstractParticleHDRPLitOutput
    {
        public override string name => "Output Particle".AppendLabel("HDRP Lit", false) + "\nCube";
        public override string codeGeneratorTemplate => RenderPipeTemplate("VFXParticleLitCube");
        public override VFXTaskType taskType => VFXTaskType.ParticleHexahedronOutput;
        public override bool implementsMotionVector => true;


        public override void OnEnable()
        {
            blendMode = BlendMode.Opaque;
            base.OnEnable();
        }

        public override IEnumerable<VFXAttributeInfo> attributes
        {
            get
            {
                yield return new VFXAttributeInfo(VFXAttribute.Position, VFXAttributeMode.Read);
                if (colorMode != ColorMode.None)
                    yield return new VFXAttributeInfo(VFXAttribute.Color, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alpha, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Alive, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AxisZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.AngleZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.PivotZ, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.Size, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleX, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleY, VFXAttributeMode.Read);
                yield return new VFXAttributeInfo(VFXAttribute.ScaleZ, VFXAttributeMode.Read);
            }
        }

        protected override IEnumerable<string> filteredOutSettings
        {
            get
            {
                foreach (var setting in base.filteredOutSettings)
                    yield return setting;

                yield return nameof(blendMode);
                yield return nameof(shaderGraph);
                yield return nameof(enableRayTracing);
            }
        }

        protected override IEnumerable<string> untransferableSettings
        {
            get
            {
                foreach (var setting in base.untransferableSettings)
                {
                    yield return setting;
                }
                yield return nameof(blendMode);
                yield return nameof(shaderGraph);
                yield return nameof(enableRayTracing);
            }
        }
    }
}
