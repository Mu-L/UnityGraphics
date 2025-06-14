using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEditor.Graphing;
using UnityEditor.ShaderGraph.Internal;

namespace UnityEditor.ShaderGraph
{
    abstract class CodeFunctionNode : AbstractMaterialNode
        , IGeneratesBodyCode
        , IGeneratesFunction
        , IMayRequireNormal
        , IMayRequireTangent
        , IMayRequireBitangent
        , IMayRequireMeshUV
        , IMayRequireScreenPosition
        , IMayRequireNDCPosition
        , IMayRequirePixelPosition
        , IMayRequireViewDirection
        , IMayRequirePosition
        , IMayRequirePositionPredisplacement
        , IMayRequireVertexColor
    {
        [NonSerialized]
        private List<SlotAttribute> m_Slots = new List<SlotAttribute>();

        public override bool hasPreview
        {
            get { return true; }
        }

        protected CodeFunctionNode()
        {
            UpdateNodeAfterDeserialization();
        }

        protected struct Boolean
        { }

        protected struct Vector1
        { }

        protected struct Texture2D
        { }

        protected struct Texture2DArray
        { }

        protected struct Texture3D
        { }

        protected struct SamplerState
        { }

        protected struct Gradient
        { }

        protected struct DynamicDimensionVector
        { }

        protected struct ColorRGBA
        { }

        protected struct ColorRGB
        { }

        protected struct Matrix3x3
        { }

        protected struct Matrix2x2
        { }

        protected struct DynamicDimensionMatrix
        { }

        protected struct PropertyConnectionState
        { }

        protected enum Binding
        {
            None,
            ObjectSpaceNormal,
            ObjectSpaceTangent,
            ObjectSpaceBitangent,
            ObjectSpacePosition,
            ViewSpaceNormal,
            ViewSpaceTangent,
            ViewSpaceBitangent,
            ViewSpacePosition,
            WorldSpaceNormal,
            WorldSpaceTangent,
            WorldSpaceBitangent,
            WorldSpacePosition,
            TangentSpaceNormal,
            TangentSpaceTangent,
            TangentSpaceBitangent,
            TangentSpacePosition,
            MeshUV0,
            MeshUV1,
            MeshUV2,
            MeshUV3,
            MeshUV4,
            MeshUV5,
            MeshUV6,
            MeshUV7,
            ScreenPosition,
            ObjectSpaceViewDirection,
            ViewSpaceViewDirection,
            WorldSpaceViewDirection,
            TangentSpaceViewDirection,
            VertexColor,
        }

        [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
        protected class SlotAttribute : Attribute
        {
            public int slotId { get; private set; }
            public Binding binding { get; private set; }
            public bool hidden { get; private set; }
            public Vector4? defaultValue { get; private set; }
            public ShaderStageCapability stageCapability { get; private set; }

            public SlotAttribute(int mSlotId, Binding mImplicitBinding, ShaderStageCapability mStageCapability = ShaderStageCapability.All)
            {
                slotId = mSlotId;
                binding = mImplicitBinding;
                defaultValue = null;
                stageCapability = mStageCapability;
            }

            public SlotAttribute(int mSlotId, Binding mImplicitBinding, bool mHidden, ShaderStageCapability mStageCapability = ShaderStageCapability.All)
            {
                slotId = mSlotId;
                binding = mImplicitBinding;
                hidden = mHidden;
                defaultValue = null;
                stageCapability = mStageCapability;
            }

            public SlotAttribute(int mSlotId, Binding mImplicitBinding, float defaultX, float defaultY, float defaultZ, float defaultW, ShaderStageCapability mStageCapability = ShaderStageCapability.All)
            {
                slotId = mSlotId;
                binding = mImplicitBinding;
                defaultValue = new Vector4(defaultX, defaultY, defaultZ, defaultW);
                stageCapability = mStageCapability;
            }
        }

        protected abstract MethodInfo GetFunctionToConvert();

        private static SlotValueType ConvertTypeToSlotValueType(ParameterInfo p)
        {
            Type t = p.ParameterType;
            if (p.ParameterType.IsByRef)
                t = p.ParameterType.GetElementType();

            if (t == typeof(Boolean))
            {
                return SlotValueType.Boolean;
            }
            if (t == typeof(Vector1))
            {
                return SlotValueType.Vector1;
            }
            if (t == typeof(Vector2))
            {
                return SlotValueType.Vector2;
            }
            if (t == typeof(Vector3))
            {
                return SlotValueType.Vector3;
            }
            if (t == typeof(Vector4))
            {
                return SlotValueType.Vector4;
            }
            if (t == typeof(Color))
            {
                return SlotValueType.Vector4;
            }
            if (t == typeof(ColorRGBA))
            {
                return SlotValueType.Vector4;
            }
            if (t == typeof(ColorRGB))
            {
                return SlotValueType.Vector3;
            }
            if (t == typeof(Texture2D))
            {
                return SlotValueType.Texture2D;
            }
            if (t == typeof(Texture2DArray))
            {
                return SlotValueType.Texture2DArray;
            }
            if (t == typeof(Texture3D))
            {
                return SlotValueType.Texture3D;
            }
            if (t == typeof(Cubemap))
            {
                return SlotValueType.Cubemap;
            }
            if (t == typeof(Gradient))
            {
                return SlotValueType.Gradient;
            }
            if (t == typeof(SamplerState))
            {
                return SlotValueType.SamplerState;
            }
            if (t == typeof(DynamicDimensionVector))
            {
                return SlotValueType.DynamicVector;
            }
            if (t == typeof(Matrix4x4))
            {
                return SlotValueType.Matrix4;
            }
            if (t == typeof(Matrix3x3))
            {
                return SlotValueType.Matrix3;
            }
            if (t == typeof(Matrix2x2))
            {
                return SlotValueType.Matrix2;
            }
            if (t == typeof(DynamicDimensionMatrix))
            {
                return SlotValueType.DynamicMatrix;
            }
            if (t == typeof(PropertyConnectionState))
            {
                return SlotValueType.PropertyConnectionState;
            }

            throw new ArgumentException("Unsupported type " + t);
        }

        public sealed override void UpdateNodeAfterDeserialization()
        {
            var method = GetFunctionToConvert();

            if (method == null)
                throw new ArgumentException("Mapped method is null on node" + this);

            if (method.ReturnType != typeof(string))
                throw new ArgumentException("Mapped function should return string");

            // validate no duplicates
            var slotAtributes = method.GetParameters().Select(GetSlotAttribute).ToList();
            if (slotAtributes.Any(x => x == null))
                throw new ArgumentException("Missing SlotAttribute on " + method.Name);

            if (slotAtributes.GroupBy(x => x.slotId).Any(x => x.Count() > 1))
                throw new ArgumentException("Duplicate SlotAttribute on " + method.Name);

            List<MaterialSlot> slots = new List<MaterialSlot>();
            foreach (var par in method.GetParameters())
            {
                var attribute = GetSlotAttribute(par);
                var name = GraphUtil.ConvertCamelCase(par.Name, true);

                MaterialSlot s;
                if (attribute.binding == Binding.None && !par.IsOut && par.ParameterType == typeof(Color))
                    s = new ColorRGBAMaterialSlot(attribute.slotId, name, par.Name, SlotType.Input, attribute.defaultValue ?? Vector4.zero, stageCapability: attribute.stageCapability, hidden: attribute.hidden);
                else if (attribute.binding == Binding.None && !par.IsOut && par.ParameterType == typeof(ColorRGBA))
                    s = new ColorRGBAMaterialSlot(attribute.slotId, name, par.Name, SlotType.Input, attribute.defaultValue ?? Vector4.zero, stageCapability: attribute.stageCapability, hidden: attribute.hidden);
                else if (attribute.binding == Binding.None && !par.IsOut && par.ParameterType == typeof(ColorRGB))
                    s = new ColorRGBMaterialSlot(attribute.slotId, name, par.Name, SlotType.Input, attribute.defaultValue ?? Vector4.zero, ColorMode.Default, stageCapability: attribute.stageCapability, hidden: attribute.hidden);
                else if (attribute.binding == Binding.None || par.IsOut)
                    s = MaterialSlot.CreateMaterialSlot(
                        ConvertTypeToSlotValueType(par),
                        attribute.slotId,
                        name,
                        par.Name,
                        par.IsOut ? SlotType.Output : SlotType.Input,
                        attribute.defaultValue ?? Vector4.zero,
                        shaderStageCapability: attribute.stageCapability,
                        hidden: attribute.hidden);
                else
                    s = CreateBoundSlot(attribute.binding, attribute.slotId, name, par.Name, attribute.stageCapability, attribute.hidden);
                slots.Add(s);

                m_Slots.Add(attribute);
            }
            foreach (var slot in slots)
            {
                AddSlot(slot);
            }
            RemoveSlotsNameNotMatching(slots.Select(x => x.id), true);
        }

        private static MaterialSlot CreateBoundSlot(Binding attributeBinding, int slotId, string displayName, string shaderOutputName, ShaderStageCapability shaderStageCapability, bool hidden = false)
        {
            switch (attributeBinding)
            {
                case Binding.ObjectSpaceNormal:
                    return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability, hidden);
                case Binding.ObjectSpaceTangent:
                    return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability, hidden);
                case Binding.ObjectSpaceBitangent:
                    return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability, hidden);
                case Binding.ObjectSpacePosition:
                    return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability, hidden);
                case Binding.ViewSpaceNormal:
                    return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability, hidden);
                case Binding.ViewSpaceTangent:
                    return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability, hidden);
                case Binding.ViewSpaceBitangent:
                    return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability, hidden);
                case Binding.ViewSpacePosition:
                    return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability, hidden);
                case Binding.WorldSpaceNormal:
                    return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability, hidden);
                case Binding.WorldSpaceTangent:
                    return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability, hidden);
                case Binding.WorldSpaceBitangent:
                    return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability, hidden);
                case Binding.WorldSpacePosition:
                    return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability, hidden);
                case Binding.TangentSpaceNormal:
                    return new NormalMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability, hidden);
                case Binding.TangentSpaceTangent:
                    return new TangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability, hidden);
                case Binding.TangentSpaceBitangent:
                    return new BitangentMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability, hidden);
                case Binding.TangentSpacePosition:
                    return new PositionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability, hidden);
                case Binding.MeshUV0:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV0, shaderStageCapability, hidden);
                case Binding.MeshUV1:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV1, shaderStageCapability, hidden);
                case Binding.MeshUV2:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV2, shaderStageCapability, hidden);
                case Binding.MeshUV3:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV3, shaderStageCapability, hidden);
                case Binding.MeshUV4:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV4, shaderStageCapability, hidden);
                case Binding.MeshUV5:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV5, shaderStageCapability, hidden);
                case Binding.MeshUV6:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV6, shaderStageCapability, hidden);
                case Binding.MeshUV7:
                    return new UVMaterialSlot(slotId, displayName, shaderOutputName, UVChannel.UV7, shaderStageCapability, hidden);
                case Binding.ScreenPosition:
                    return new ScreenPositionMaterialSlot(slotId, displayName, shaderOutputName, ScreenSpaceType.Default, shaderStageCapability, hidden);
                case Binding.ObjectSpaceViewDirection:
                    return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Object, shaderStageCapability, hidden);
                case Binding.ViewSpaceViewDirection:
                    return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.View, shaderStageCapability, hidden);
                case Binding.WorldSpaceViewDirection:
                    return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.World, shaderStageCapability, hidden);
                case Binding.TangentSpaceViewDirection:
                    return new ViewDirectionMaterialSlot(slotId, displayName, shaderOutputName, CoordinateSpace.Tangent, shaderStageCapability, hidden);
                case Binding.VertexColor:
                    return new VertexColorMaterialSlot(slotId, displayName, shaderOutputName, shaderStageCapability, hidden);
                default:
                    throw new ArgumentOutOfRangeException("attributeBinding", attributeBinding, null);
            }
        }

        public void GenerateNodeCode(ShaderStringBuilder sb, GenerationMode generationMode)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetOutputSlots(tempSlots);
                foreach (var outSlot in tempSlots)
                {
                    sb.AppendLine(outSlot.concreteValueType.ToShaderString(PrecisionUtil.Token) + " " + GetVariableNameForSlot(outSlot.id) + ";");
                }

                string call = GetFunctionName() + "(";
                bool first = true;
                tempSlots.Clear();
                GetSlots(tempSlots);
                tempSlots.Sort((slot1, slot2) => slot1.id.CompareTo(slot2.id));
                foreach (var slot in tempSlots)
                {
                    if (!first)
                    {
                        call += ", ";
                    }
                    first = false;

                    if (slot.isInputSlot)
                        call += GetSlotValue(slot.id, generationMode);
                    else
                        call += GetVariableNameForSlot(slot.id);
                }
                call += ");";

                sb.AppendLine(call);
            }
        }

        private string GetFunctionName()
        {
            var function = GetFunctionToConvert();
            return function.Name + (function.IsStatic ? string.Empty : "_" + objectId) + "_$precision"
                + (this.GetSlots<DynamicVectorMaterialSlot>().Select(s => NodeUtils.GetSlotDimension(s.concreteValueType)).FirstOrDefault() ?? "")
                + (this.GetSlots<DynamicMatrixMaterialSlot>().Select(s => NodeUtils.GetSlotDimension(s.concreteValueType)).FirstOrDefault() ?? "");
        }

        private string GetFunctionHeader()
        {
            string header = "void " + GetFunctionName() + "(";

            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetSlots(tempSlots);
                tempSlots.Sort((slot1, slot2) => slot1.id.CompareTo(slot2.id));
                var first = true;
                foreach (var slot in tempSlots)
                {
                    if (!first)
                        header += ", ";

                    first = false;

                    if (slot.isOutputSlot)
                        header += "out ";

                    // always use generic precisions for parameters, they will get concretized by the system
                    header += slot.concreteValueType.ToShaderString(PrecisionUtil.Token) + " " + slot.shaderOutputName;
                }

                header += ")";
            }

            return header;
        }

        private static object GetDefault(Type type)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        private string GetFunctionBody(MethodInfo info)
        {
            var args = new List<object>();
            foreach (var param in info.GetParameters())
                args.Add(GetDefault(param.ParameterType));

            var result = info.Invoke(this, args.ToArray()) as string;

            if (string.IsNullOrEmpty(result))
                return string.Empty;

            // stomp any newline differences that might try to sneak in via this path
            result = result.Replace("\r\n", "\n");

            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    var toReplace = string.Format("{{slot{0}dimension}}", slot.id);
                    var replacement = NodeUtils.GetSlotDimension(slot.concreteValueType);
                    result = result.Replace(toReplace, replacement);
                }
            }

            return result;
        }

        public virtual void GenerateNodeFunction(FunctionRegistry registry, GenerationMode generationMode)
        {
            registry.ProvideFunction(GetFunctionName(), s =>
            {
                s.AppendLine(GetFunctionHeader());
                var functionBody = GetFunctionBody(GetFunctionToConvert());
                var lines = functionBody.Trim('\r', '\n', '\t', ' ');
                s.AppendLines(lines);
            });
        }

        private static SlotAttribute GetSlotAttribute([NotNull] ParameterInfo info)
        {
            var attrs = info.GetCustomAttributes(typeof(SlotAttribute), false).OfType<SlotAttribute>().ToList();
            return attrs.FirstOrDefault();
        }

        public NeededCoordinateSpace RequiresNormal(ShaderStageCapability stageCapability)
        {
            var binding = NeededCoordinateSpace.None;
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresNormal();
                return binding;
            }
        }

        public NeededCoordinateSpace RequiresViewDirection(ShaderStageCapability stageCapability)
        {
            var binding = NeededCoordinateSpace.None;
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresViewDirection();
                return binding;
            }
        }

        public NeededCoordinateSpace RequiresPosition(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                var binding = NeededCoordinateSpace.None;
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresPosition();
                return binding;
            }
        }

        public NeededCoordinateSpace RequiresPositionPredisplacement(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                var binding = NeededCoordinateSpace.None;
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresPositionPredisplacement();
                return binding;
            }
        }

        public NeededCoordinateSpace RequiresTangent(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                var binding = NeededCoordinateSpace.None;
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresTangent();
                return binding;
            }
        }

        public NeededCoordinateSpace RequiresBitangent(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                var binding = NeededCoordinateSpace.None;
                foreach (var slot in tempSlots)
                    binding |= slot.RequiresBitangent();
                return binding;
            }
        }

        public bool RequiresMeshUV(UVChannel channel, ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresMeshUV(channel))
                        return true;
                }

                return false;
            }
        }

        public bool RequiresScreenPosition(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresScreenPosition(stageCapability))
                        return true;
                }
                return false;
            }
        }

        public bool RequiresNDCPosition(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresNDCPosition(stageCapability))
                        return true;
                }
                return false;
            }
        }

        public bool RequiresPixelPosition(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresPixelPosition(stageCapability))
                        return true;
                }
                return false;
            }
        }

        public bool RequiresVertexColor(ShaderStageCapability stageCapability)
        {
            using (var tempSlots = PooledList<MaterialSlot>.Get())
            {
                GetInputSlots(tempSlots);
                foreach (var slot in tempSlots)
                {
                    if (slot.RequiresVertexColor())
                        return true;
                }

                return false;
            }
        }
    }
}
