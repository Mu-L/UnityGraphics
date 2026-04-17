using System;
using System.Collections.Generic;
using Unity.GraphCommon.LowLevel;
using Unity.GraphCommon.LowLevel.Editor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace UnityEditor.VFX
{
    class VfxGraphLegacyCompilationOutput
    {
        public List<UnityEditor.VFX.VFXExpressionDesc> SheetExpressions { get; } = new();
        public List<UnityEditor.VFX.VFXExpressionDesc> SheetExpressionsPerSpawnEventAttribute { get; } = new();
        public List<UnityEditor.VFX.VFXExpressionValueContainerDesc> SheetValues { get; } = new();
        public List<UnityEditor.VFX.VFXExposedMapping> SheetExposed { get; } = new();
        public List<UnityEditor.VFX.VFXEditorSystemDesc> SystemDescs { get; } = new();
        public List<UnityEditor.VFX.VFXEventDesc> EventDescs { get; } = new();
        public List<UnityEditor.VFX.VFXGPUBufferDesc> GpuBufferDescs { get; } = new();
        public List<UnityEditor.VFX.VFXCPUBufferDesc> CpuBufferDescs { get; } = new();
        public List<UnityEditor.VFX.VFXTemporaryGPUBufferDesc> TemporaryBufferDescs { get; } = new();
        public List<UnityEditor.VFX.VFXShaderSourceDesc> ShaderSourceDescs { get; } = new();
        public UnityEngine.VFX.VFXCompilationMode CompilationMode { get; set; } = UnityEngine.VFX.VFXCompilationMode.Edition;
        public List<UnityEngine.Object> Objects { get; } = new();
        public uint Version { get; set; }

        public VisualEffectAssetDesc GenerateAssetDesc()
        {
            var vfxAssetDesc = new VisualEffectAssetDesc();
            vfxAssetDesc.compilationMode = VFXCompilationMode.Runtime;
            vfxAssetDesc.systemDesc = SystemDescs.ToArray();
            vfxAssetDesc.cpuBufferDesc = CpuBufferDescs.ToArray();
            vfxAssetDesc.gpuBufferDesc = GpuBufferDescs.ToArray();
            vfxAssetDesc.temporaryBufferDesc = TemporaryBufferDescs.ToArray();
            vfxAssetDesc.shaderSourceDesc = ShaderSourceDescs.ToArray();
            vfxAssetDesc.sheet = new VFXExpressionSheet()
            {
                exposed = SheetExposed.ToArray(),
                expressions = SheetExpressions.ToArray(),
                expressionsPerSpawnEventAttribute = SheetExpressionsPerSpawnEventAttribute.ToArray(),
                values = SheetValues.ToArray()
            };
            vfxAssetDesc.eventDesc = EventDescs.ToArray();
            vfxAssetDesc.rendererSettings = new()
            {
                motionVectorGenerationMode = MotionVectorGenerationMode.Camera,
                shadowCastingMode = ShadowCastingMode.Off
            };
            vfxAssetDesc.instancingDisabledReason = VFXInstancingDisabledReason.Unknown;
            vfxAssetDesc.version = Version;

            return vfxAssetDesc;
        }
    }

    class VfxGraphLegacyOutputPass : DataGenerationPass<VfxGraphLegacyCompilationOutput>
    {
        VfxGraphLegacyCompilationOutput m_currentOutput;

        static readonly Dictionary<System.Type, UnityEngine.VFX.VFXValueType> s_ValueTypeConversion = new()
        {
            { typeof(float), UnityEngine.VFX.VFXValueType.Float },
            { typeof(Vector2), UnityEngine.VFX.VFXValueType.Float2 },
            { typeof(Vector3), UnityEngine.VFX.VFXValueType.Float3 },
            { typeof(Vector4), UnityEngine.VFX.VFXValueType.Float4 },
            { typeof(Color), UnityEngine.VFX.VFXValueType.Float4 },
            { typeof(int), UnityEngine.VFX.VFXValueType.Int32 },
            { typeof(uint), UnityEngine.VFX.VFXValueType.Uint32 },
            { typeof(EntityId), UnityEngine.VFX.VFXValueType.EntityId },
            { typeof(Texture2D), UnityEngine.VFX.VFXValueType.Texture2D },
            { typeof(Texture2DArray), UnityEngine.VFX.VFXValueType.Texture2DArray },
            { typeof(Texture3D), UnityEngine.VFX.VFXValueType.Texture3D },
            { typeof(Cubemap), UnityEngine.VFX.VFXValueType.TextureCube },
            { typeof(CubemapArray), UnityEngine.VFX.VFXValueType.TextureCubeArray },
            { typeof(Matrix4x4), UnityEngine.VFX.VFXValueType.Matrix4x4 },
            { typeof(AnimationCurve), UnityEngine.VFX.VFXValueType.Curve },
            { typeof(Gradient), UnityEngine.VFX.VFXValueType.ColorGradient },
            { typeof(Mesh), UnityEngine.VFX.VFXValueType.Mesh },
            { typeof(SkinnedMeshRenderer), UnityEngine.VFX.VFXValueType.SkinnedMeshRenderer },
            { typeof(bool), UnityEngine.VFX.VFXValueType.Boolean },
            { typeof(GraphicsBuffer), UnityEngine.VFX.VFXValueType.Buffer },
        };

        readonly Dictionary<IDataDescription, uint> m_GpuBufferDescIndices = new();
        readonly Dictionary<IDataDescription, uint> m_CpuBufferDescIndices = new();
        readonly Dictionary<DataNodeId, uint> m_ValuesExpressionIndices = new();

        static UnityEngine.VFX.VFXValueType GetVFXValueTypeFromType(System.Type type) => s_ValueTypeConversion.TryGetValue(type, out var valueType) ? valueType : UnityEngine.VFX.VFXValueType.None;

        public VfxGraphLegacyCompilationOutput Execute(ref CompilationContext context)
        {
            VfxGraphLegacyCompilationOutput output = new();
            Cleanup();
            m_currentOutput = output;
            m_currentOutput.Version = 7;

            AddDataContainerSources(ref context);

            GenerateExpressionSheet(ref context);
            GenerateBufferDescriptions(ref context);
            GenerateSystemDescs(ref context);

            output.EventDescs.Add(new() { name = UnityEngine.VFX.VisualEffectAsset.PlayEventName, startSystems = new[] { 0u }, stopSystems = Array.Empty<uint>(), initSystems = Array.Empty<uint>() });
            output.EventDescs.Add(new() { name = UnityEngine.VFX.VisualEffectAsset.StopEventName, startSystems = Array.Empty<uint>(), stopSystems = new[] { 0u }, initSystems = Array.Empty<uint>() });

            foreach (var buffer in output.CpuBufferDescs)
            {
                //Debug.Log("Buffer " + buffer.capacity);
            }

            Cleanup();

            return output;
        }

        uint AddExpressionRecursively(VFXExpression expression)
        {
            List<uint> parentExpressionIndices = new();
            foreach (var parentExpression in expression.parents)
            {
                var parentExpressionValue = AddExpressionRecursively(parentExpression);
                parentExpressionIndices.Add(parentExpressionValue);
            }

            // See VFXExpressionAbstract GetOperands for reference
            var data = new VFXExpression.Operands(-1);
            for(int i = 0; i < parentExpressionIndices.Count; i++)
                data[i] = (int)parentExpressionIndices[i];
            for (int i = 0; i < expression.additionalOperands.Length; i++)
                data[VFXExpression.Operands.OperandCount - expression.additionalOperands.Length + i] = expression.additionalOperands[i];

            uint vfxExpressionIndex = AddExpression(expression.operation, data[0], data[1], data[2], data[3]);

            if (expression.Is(VFXExpression.Flags.Value))
            {
                m_currentOutput.SheetValues.Add(CreateValueContainerDesc(expression, vfxExpressionIndex));
            }

            return vfxExpressionIndex;
        }

        void AddDataContainerSources(ref CompilationContext context)
        {
            var generatedCodeContainer = context.data.Get<GeneratedCodeContainer>();
            foreach (var dataContainer in context.graph.DataContainers)
            {
                string sourceCode = generatedCodeContainer.Find(dataContainer.Id);
                if (sourceCode != null)
                {
                    AddShaderSourceDesc($"{dataContainer.Name}.hlsl", sourceCode, false);
                }
            }
        }

        void GenerateExpressionSheet(ref CompilationContext context)
        {
            foreach (var dataNode in context.graph.DataNodes)
            {
                if (dataNode.TaskNode.Task is LegacyExpressionTask expressionTask)
                {
                    foreach (var childDataNode in dataNode.Children)
                    {
                        if (childDataNode.TaskNode.Task is GpuKernelTask or PlaceholderSystemTask or RenderingTask or SpawnerTask)
                        {
                            uint vfxExpressionIndex = AddExpressionRecursively(expressionTask.Expression);
                            m_ValuesExpressionIndices.Add(childDataNode.Id, vfxExpressionIndex);
                        }
                    }
                }
            }
        }

        void GenerateBufferDescriptions(ref CompilationContext context)
        {
            GenerateAttributeBufferDescriptions(ref context);
            GenerateGraphValuesBufferDescriptions(ref context);
            GenerateDeadListBuffersDescription(ref context);
            GenerateSpawnBuffersDescriptions(ref context);
        }

        void GenerateAttributeBufferDescriptions(ref CompilationContext context)
        {
            AttributeSetLayoutCompilationData attributeSetLayouts = context.data.Get<AttributeSetLayoutCompilationData>();
            foreach (var kvp in attributeSetLayouts)
            {
                AttributeData attributeData = kvp.Key;
                var attributeSetLayout = kvp.Value;
                uint capacity = attributeSetLayout.Capacity;

                var layoutElementDescs = new List<VFXLayoutElementDesc>();
                foreach (var attribute in attributeSetLayout.Attributes)
                {
                    (uint bucketOffset, uint bucketSize, uint elementOffset) = attributeSetLayout.GetBucketLocation(attribute);
                    layoutElementDescs.Add(new VFXLayoutElementDesc()
                    {
                        name = attribute.Name,
                        type = GetVFXValueTypeFromType(attribute.Type),
                        offset = new VFXLayoutOffset()
                        {
                            bucket = bucketOffset,
                            element = elementOffset,
                            structure = bucketSize
                        },
                    });
                }

                VFXGPUBufferDesc bufferDesc = new VFXGPUBufferDesc()
                {
                    target = GraphicsBuffer.Target.Raw,
                    size = attributeSetLayout.GetBufferSize(),
                    stride = 4u,
                    capacity = capacity,
                    mode = ComputeBufferMode.Immutable,
                    layout = layoutElementDescs.ToArray(),
                };
                uint bufferIndex = AddGPUBufferData(bufferDesc);
                m_GpuBufferDescIndices[attributeData] = bufferIndex;
            }
        }

        void GenerateGraphValuesBufferDescriptions(ref CompilationContext context)
        {
            var dataLayoutContainer = context.data.Get<DataLayoutContainer>();

            foreach (var dataContainer in context.graph.DataContainers)
            {
                if (dataLayoutContainer.TryGetLayout(dataContainer.Id, out var valueBufferLayout))
                {
                    uint bufferIndex = AddGPUBufferData(new VFXGPUBufferDesc()
                    {
                        target = GraphicsBuffer.Target.Raw,
                        size = valueBufferLayout.GetBufferSize(),
                        stride = 4u,
                        mode = ComputeBufferMode.Dynamic,
                    });

                    m_GpuBufferDescIndices[dataContainer.RootDataView.DataDescription] = bufferIndex;
                }
            }
        }

        void GenerateDeadListBuffersDescription(ref CompilationContext context)
        {
            foreach (var dataView in context.graph.DataViews)
            {
                if (dataView.DataDescription is DeadListData deadListData)
                {
                    Debug.Assert(dataView.Parent.HasValue);
                    if (dataView.Parent.Value.DataDescription is ParticleData particleData)
                    {
                        uint bufferIndex = AddGPUBufferData(new VFXGPUBufferDesc()
                        {
                            target = GraphicsBuffer.Target.Structured,
                            size = particleData.Capacity + 2,
                            stride = 4u,
                            mode = ComputeBufferMode.Immutable,
                        });

                        m_GpuBufferDescIndices[deadListData] = bufferIndex;
                    }
                }
            }
        }

        void GenerateSpawnBuffersDescriptions(ref CompilationContext context)
        {
            foreach (var dataView in context.graph.DataViews)
            {
                if (dataView.DataDescription is SpawnData spawnData)
                {
                    uint bufferIndex = AddGPUBufferData(new VFXGPUBufferDesc()
                    {
                        target = GraphicsBuffer.Target.Structured,
                        size = 2,
                        stride = 4u,
                        mode = ComputeBufferMode.Dynamic,
                    });
                    m_GpuBufferDescIndices[spawnData] = bufferIndex;
                }
            }
        }

        void GenerateSystemDescs(ref CompilationContext context)
        {
            GenerateSpawnerSystemDescs(ref context);

            var particleSystemContainer = context.data.Get<VfxGraphLegacyParticleSystemContainer>();
            foreach (var particleSystem in particleSystemContainer)
            {
                if (GenerateParticleSystemDesc(ref context, particleSystem, out var systemDesc))
                {
                    m_currentOutput.SystemDescs.Add(systemDesc);
                }
            }
        }

        void GenerateSpawnerSystemDescs(ref CompilationContext context)
        {
            Dictionary<SpawnData, VFXEditorSystemDesc> spawnDataMap = new();

            // Collect systems from spawn data first
            foreach (var dataView in context.graph.DataViews)
            {
                if (dataView.Root.DataDescription is SpawnData spawnData)
                {
                    if (!spawnDataMap.ContainsKey(spawnData))
                    {
                        GenerateSpawnerSystemDesc(ref context, spawnData, out var systemDesc);
                        spawnDataMap[spawnData] = systemDesc;
                    }
                }
            }
            // Then fill tasks for each spawner system
            foreach (var taskNode in context.graph.TaskNodes)
            {
                if (taskNode.Task is SpawnerTask spawnerTask)
                {
                    foreach (var dataNode in taskNode.DataNodes)
                    {
                        if(dataNode.UsedDataViewsRoot.DataDescription is SpawnData spawnDataDescription)
                        {
                            var systemDesc = spawnDataMap[spawnDataDescription];
                            List<VFXEditorTaskDesc> taskDescs = new List<VFXEditorTaskDesc>(systemDesc.tasks);
                            GenerateSpawnerTask(ref context, spawnerTask, taskNode, out var task);
                            taskDescs.Add(task);
                            systemDesc.tasks = taskDescs.ToArray();
                            spawnDataMap[spawnDataDescription] = systemDesc;
                        }
                    }
                }
            }
            foreach (var spawnerSystemDesc in spawnDataMap.Values)
            {
                m_currentOutput.SystemDescs.Add(spawnerSystemDesc);
            }
        }

        bool GenerateSpawnerSystemDesc(ref CompilationContext context, SpawnData spawnData, out UnityEditor.VFX.VFXEditorSystemDesc systemDesc)
        {
            systemDesc = new();

            var cpuData = new UnityEditor.VFX.VFXCPUBufferData();
            cpuData.PushFloat(1.0f);
            var spawnerOutputIndex = AddCPUBufferData(new()
            {
                capacity = 1u,
                stride = 1u,
                initialData = cpuData,
                layout = new[]
                    {
                        new UnityEditor.VFX.VFXLayoutElementDesc()
                        {
                            name = VFXAttribute.SpawnCount.name,
                            offset = new () { bucket = 0u, element = 0u, structure = 1u},
                            type = UnityEngine.VFX.VFXValueType.Float
                        }
                    }
            });
            m_CpuBufferDescIndices[spawnData] = spawnerOutputIndex;

            systemDesc.name = "Spawn System";
            systemDesc.type = UnityEngine.VFX.VFXSystemType.Spawner;
            systemDesc.buffers = new[] { new UnityEditor.VFX.VFXMapping("spawner_output", (int)spawnerOutputIndex) };
            systemDesc.tasks = Array.Empty<VFXEditorTaskDesc>();
            return true;
        }

        bool GenerateSpawnerTask(ref CompilationContext context, SpawnerTask spawnerTask, TaskNode spawnerTaskNode, out UnityEditor.VFX.VFXEditorTaskDesc taskDesc)
        {
            taskDesc = new();

            taskDesc.shaderSourceIndex = -1;
            taskDesc.type = (UnityEngine.VFX.VFXTaskType)spawnerTask.SpawnerType;

            List<VFXMapping> valueMappings = new();
            var taskNode = spawnerTaskNode;
            foreach (var dataBinding in taskNode.DataBindings)
            {
                if (m_ValuesExpressionIndices.TryGetValue(dataBinding.DataNode.Id, out var expressionIndex))
                {
                    string name = dataBinding.BindingDataKey.ToString();
                    valueMappings.Add(new VFXMapping(name, (int)expressionIndex));
                }
            }

            taskDesc.values = valueMappings.ToArray();
            return true;
        }

        bool GenerateParticleSystemDesc(ref CompilationContext context, VfxGraphLegacyParticleSystemContainer.ParticleSystem particleSystem, out UnityEditor.VFX.VFXEditorSystemDesc systemDesc)
        {
            systemDesc = new();
            systemDesc.type = UnityEngine.VFX.VFXSystemType.Particle;
            systemDesc.capacity = particleSystem.Capacity;

            var bufferMappings = GenerateSystemBuffersMappings(context, particleSystem);
            systemDesc.buffers = bufferMappings;

            List<UnityEditor.VFX.VFXEditorTaskDesc> taskDescs = new();
            List<UnityEditor.VFX.VFXInstanceSplitDesc> instanceSplitDescs = new();
            foreach (var task in particleSystem.Tasks)
            {
                if (GenerateParticleSystemTask(ref context, task, out var taskDesc))
                {
                    taskDescs.Add(taskDesc);
                }
                instanceSplitDescs.Add(new UnityEditor.VFX.VFXInstanceSplitDesc()
                {
                    values = Array.Empty<uint>(),
                });
            }
            systemDesc.values = GenerateSystemValuesMappings(context, particleSystem);
            systemDesc.tasks = taskDescs.ToArray();
            systemDesc.instanceSplitDescs = instanceSplitDescs.ToArray();

            foreach(var dataView in context.graph.DataViews)
            {
                if (dataView.DataDescription is DeadListData)
                {
                    systemDesc.flags |= VFXSystemFlag.SystemHasKill;
                    break;
                }

            }


            return true;
        }

        VFXMapping[] GenerateSystemValuesMappings(CompilationContext context, VfxGraphLegacyParticleSystemContainer.ParticleSystem particleSystem)
        {
            var valueMappings = new List<VFXMapping>();
            var graphValueMappings = new List<(int, VFXMapping)>();
            var taskNode = context.graph.TaskNodes[particleSystem.SystemTask.Id];

            var dataLayoutContainer = context.data.Get<DataLayoutContainer>();

            DataContainerId graphValuesContainerId = DataContainerId.Invalid;
            // Find graph values buffer in bindings
            foreach (var dataBinding in taskNode.DataBindings)
            {
                if (dataBinding.BindingDataKey == TemplatedTask.GraphValuesBufferKey)
                {
                    graphValuesContainerId = dataBinding.DataView.DataContainer.Id;
                }
            }
            dataLayoutContainer.TryGetLayout(graphValuesContainerId, out var graphValuesBufferLayout);

            foreach (var dataBinding in taskNode.DataBindings)
            {
                if (m_ValuesExpressionIndices.TryGetValue(dataBinding.DataNode.Id, out var index))
                {
                    var name = dataBinding.BindingDataKey.ToString();
                    // "System values"
                    if(name is "bounds_center" or "bounds_size" or "boundsPadding")
                    {
                        valueMappings.Add(new VFXMapping(name, (int)index));
                        continue;
                    }

                    // Graph values
                    int graphValueOffset = graphValuesBufferLayout.GetValueOffset(dataBinding.DataView.DataDescription as ValueData);
                    graphValueMappings.Add((graphValueOffset, new VFXMapping(name, (int)index)));
                }
            }
            List<VFXMapping> mappings = new();
            foreach (var mapping in valueMappings)
            {
                mappings.Add(mapping);
            }
            mappings.Add(new VFXMapping("graphValuesOffset", valueMappings.Count + 1));

            //Need to add the graph value mapping in the order of graph value layout for the runtime to work correctly
            graphValueMappings.Sort((a, b) => a.Item1.CompareTo(b.Item1));
            foreach ( (int _, VFXMapping mapping) in graphValueMappings)
            {
                mappings.Add(mapping);
            }
            return mappings.ToArray();

        }
        VFXMapping[] GenerateSystemBuffersMappings(CompilationContext context, VfxGraphLegacyParticleSystemContainer.ParticleSystem particleSystem)
        {
            HashSet<VFXMapping> bufferMappings = new();
            foreach (var task in particleSystem.Tasks)
            {
                var taskNode = context.graph.TaskNodes[task.Id];
                foreach (var dataBinding in taskNode.DataBindings)
                {
                    foreach (var dataView in dataBinding.DataNode.UsedDataViews)
                    {
                        if (m_GpuBufferDescIndices.TryGetValue(dataView.DataDescription, out var gpuIndex))
                        {
                            //TODO: Get the mapping name from the data view or data binding or something
                            if(dataView.DataDescription is AttributeData)
                            {
                                if(dataBinding.BindingDataKey.ToString().Equals("SpawnDataBinding"))
                                    bufferMappings.Add(new VFXMapping("sourceAttributeBuffer", (int)gpuIndex));
                                else if (dataBinding.BindingDataKey.ToString().Equals("ParticleDataBinding"))
                                    bufferMappings.Add(new VFXMapping("attributeBuffer", (int)gpuIndex));
                            }
                            else if(dataView.DataDescription is StructuredData)
                            {
                                bufferMappings.Add(new VFXMapping("graphValuesBuffer", (int)gpuIndex));
                            }
                            else if (dataView.DataDescription is DeadListData)
                            {
                                bufferMappings.Add(new VFXMapping("deadList", (int)gpuIndex));
                            }
                            else if (dataView.DataDescription is SpawnData)
                            {
                                bufferMappings.Add(new VFXMapping("instancingPrefixSum", (int)gpuIndex));
                            }
                        }

                        if (m_CpuBufferDescIndices.TryGetValue(dataView.DataDescription, out var cpuIndex))
                        {
                            bufferMappings.Add(new VFXMapping("spawner_input", (int)cpuIndex));
                        }
                    }
                }
            }
            return HashSetToArray(bufferMappings);
        }

        bool GenerateParticleSystemTask(ref CompilationContext context, VfxGraphLegacyParticleSystemContainer.Task task, out UnityEditor.VFX.VFXEditorTaskDesc taskDesc)
        {
            taskDesc = new();

            var generatedCodeContainer = context.data.Get<GeneratedCodeContainer>();
            string sourceCode = generatedCodeContainer.Find(task.Id);
            bool isCompute = !task.TaskType.HasFlag(UnityEngine.VFX.VFXTaskType.Output);
            taskDesc.shaderSourceIndex = (int)AddShaderSourceDesc(task.Name, sourceCode, isCompute);
            taskDesc.type = task.TaskType;

            HashSet<VFXMapping> bufferMappings = new();
            List<VFXMapping> valueMappings = new();
            var taskNode = context.graph.TaskNodes[task.Id];
            foreach (var dataBinding in taskNode.DataBindings)
            {
                foreach (var dataView in dataBinding.DataNode.UsedDataViews)
                {
                    // TODO: VFXMapping name should be linked to what is done in the description writers
                    if (m_GpuBufferDescIndices.TryGetValue(dataView.DataDescription, out var gpuIndex))
                    {
                        if(dataView.DataDescription is AttributeData)
                        {
                            bufferMappings.Add(new VFXMapping($"_{dataView.DataContainer.IdentifierName}_attributeBuffer", (int)gpuIndex));
                        }
                        else if (dataView.DataDescription is DeadListData)
                        {
                            bufferMappings.Add(new VFXMapping($"_{dataView.DataContainer.IdentifierName}_deadListBuffer", (int)gpuIndex));
                        }
                        else if(dataView.Root.DataDescription is StructuredData)
                        {
                            bufferMappings.Add(new VFXMapping($"_{dataView.DataContainer.IdentifierName}_buffer", (int)gpuIndex));
                        }
                        else if (dataView.DataDescription is SpawnData)
                        {
                            bufferMappings.Add(new VFXMapping($"_{dataView.DataContainer.IdentifierName}_instancingPrefixSum", (int)gpuIndex));
                        }
                    }
                }
                if (m_ValuesExpressionIndices.TryGetValue(dataBinding.DataNode.Id, out var expressionIndex))
                {
                    // For textures/buffers for now we need to use the name of the data container to match with what is generated in the description writer, we should find a better way to link them together
                    string name = dataBinding.DataNode.DataContainer.Name;
                    valueMappings.Add(new VFXMapping(name, (int)expressionIndex));
                }
            }
            if (taskNode.Task is GpuKernelTask gpuKernelTask)
            {
                //taskDesc.processor = gpuKernelTask.Shader;
            }
            else if (taskNode.Task is RenderingTask renderingTask)
            {
                //taskDesc.processor = renderingTask.Material;
            }

            taskDesc.values = valueMappings.ToArray();
            taskDesc.buffers = HashSetToArray(bufferMappings);

            return true;
        }

        VFXExpressionValueContainerDesc CreateValueContainerDesc(VFXExpression exp, uint expressionIndex)
        {
            VFXExpressionValueContainerDesc value;
            switch (exp.valueType)
            {
                case VFXValueType.Float: value = CreateValueDesc<float>(exp, (int)expressionIndex); break;
                case VFXValueType.Float2: value = CreateValueDesc<Vector2>(exp, (int)expressionIndex); break;
                case VFXValueType.Float3: value = CreateValueDesc<Vector3>(exp, (int)expressionIndex); break;
                case VFXValueType.Float4: value = CreateValueDesc<Vector4>(exp, (int)expressionIndex); break;
                case VFXValueType.Int32: value = CreateValueDesc<int>(exp, (int)expressionIndex); break;
                case VFXValueType.Uint32: value = CreateValueDesc<uint>(exp, (int)expressionIndex); break;
                case VFXValueType.Texture2D:
                case VFXValueType.Texture2DArray:
                case VFXValueType.Texture3D:
                case VFXValueType.TextureCube:
                case VFXValueType.TextureCubeArray:
                    value = CreateObjectValueDesc<Texture>(exp, (int)expressionIndex);
                    break;
                case VFXValueType.CameraBuffer: value = CreateObjectValueDesc<Texture>(exp, (int)expressionIndex); break;
                case VFXValueType.Matrix4x4: value = CreateValueDesc<Matrix4x4>(exp, (int)expressionIndex); break;
                case VFXValueType.Curve: value = CreateValueDesc<AnimationCurve>(exp, (int)expressionIndex); break;
                case VFXValueType.ColorGradient: value = CreateValueDesc<Gradient>(exp, (int)expressionIndex); break;
                case VFXValueType.Mesh: value = CreateObjectValueDesc<Mesh>(exp, (int)expressionIndex); break;
                case VFXValueType.SkinnedMeshRenderer: value = CreateObjectValueDesc<SkinnedMeshRenderer>(exp, (int)expressionIndex); break;
                case VFXValueType.Boolean: value = CreateValueDesc<bool>(exp, (int)expressionIndex); break;
                case VFXValueType.Buffer: value = CreateValueDesc<GraphicsBuffer>(exp, (int)expressionIndex); break;
                default: throw new InvalidOperationException("Invalid type : " + exp.valueType);
            }

            return value;
        }

        private static VFXExpressionValueContainerDesc<T> CreateValueDesc<T>(VFXExpression exp, int expressionIndex)
        {
            var desc = new VFXExpressionValueContainerDesc<T>();
            desc.value = exp.Get<T>();
            desc.expressionIndex = (uint)expressionIndex;
            return desc;
        }
        private static VFXExpressionObjectValueContainerDesc<T> CreateObjectValueDesc<T>(VFXExpression exp, int expressionIndex)
        {
            var desc = new VFXExpressionObjectValueContainerDesc<T>();
            desc.entityId = exp.Get<EntityId>();
            desc.expressionIndex = (uint)expressionIndex;
            return desc;
        }

        uint AddExpression(VFXExpressionOperation op, int data0, int data1, int data2, int data3)
        {
            UnityEditor.VFX.VFXExpressionDesc vfxExpression = new(){ op = op };
            vfxExpression.data = new[] { data0, data1, data2, data3 };
            var vfxExpressionIndex = (uint)m_currentOutput.SheetExpressions.Count;
            m_currentOutput.SheetExpressions.Add(vfxExpression);
            return vfxExpressionIndex;
        }

        uint AddCPUBufferData(UnityEditor.VFX.VFXCPUBufferDesc data)
        {
            uint bufferDataIndex = (uint)m_currentOutput.CpuBufferDescs.Count;
            m_currentOutput.CpuBufferDescs.Add(data);
            return bufferDataIndex;
        }

        uint AddGPUBufferData(UnityEditor.VFX.VFXGPUBufferDesc data)
        {
            uint bufferDataIndex = (uint)m_currentOutput.GpuBufferDescs.Count;
            m_currentOutput.GpuBufferDescs.Add(data);
            return bufferDataIndex;
        }

        uint AddShaderSourceDesc(string name, string sourceCode, bool isCompute)
        {
            uint shaderSourceIndex = (uint)m_currentOutput.ShaderSourceDescs.Count;

            UnityEditor.VFX.VFXShaderSourceDesc shaderSourceDesc = new();
            shaderSourceDesc.name = name;
            shaderSourceDesc.source = sourceCode;
            shaderSourceDesc.compute = isCompute;

            m_currentOutput.ShaderSourceDescs.Add(shaderSourceDesc);
            return shaderSourceIndex;
        }

        void Cleanup()
        {
            m_GpuBufferDescIndices.Clear();
            m_CpuBufferDescIndices.Clear();
            m_ValuesExpressionIndices.Clear();
            m_currentOutput = null;
        }

        T[] HashSetToArray<T>(HashSet<T> hashSet)
        {
            T[] array = new T[hashSet.Count];
            int index = 0;
            foreach (T value in hashSet)
            {
                array[index++] = value;
            }
            return array;
        }
    }
}
