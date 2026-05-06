using System;
using System.Collections.Generic;
using Unity.GraphCommon.LowLevel.Editor;
using UnityEngine.VFX;
using UnityEngine;

namespace UnityEditor.VFX
{
    class VfxIntermediateGraphBuilder
    {
        private class ParticleSystemBuildInfo
        {
            public ParticleSystemBuildInfo(VFXData data)
            {
                Data = data;
            }

            public VFXData Data { get; }
            public VFXBasicInitialize InitContext { get; set; }
            public List<VFXBasicUpdate> UpdateContexts { get; } = new();
            public List<VFXAbstractParticleOutput> OutputContexts { get; } = new();
            public DataViewId InputSpawnData { get; set; } = DataViewId.Invalid;
        }

        private static readonly DataPath kAttributeDataPath = new DataPath(ParticleData.AttributeDataKey);
        private static readonly IDataKey kParticleBindingKey = new NameDataKey("ParticleDataBinding");
        private static readonly IDataKey kSpawnDataBindingKey = new NameDataKey("SpawnDataBinding");
        private static readonly IDataKey kMainTextureKey = new NameDataKey("MainTexture");
        static readonly IDataKey kGraphValuesKey = new NameDataKey("GraphValues");

        Dictionary<VFXData, ParticleSystemBuildInfo> m_ParticleSystems = new();
        VFXSystemNames m_SystemNames = new();
        VFXExpressionGraph m_ExpressionGraph;
        VfxSubTaskBuilder m_SubTaskBuilder = new VfxSubTaskBuilder();
        Dictionary<VFXExpression, DataViewId> m_DeduplicatedExpressionsToDataView = new();

        Dictionary<IDataDescription, Dictionary<string, uint>> m_GraphValueNameCounts = new();

        public IReadOnlyGraph BuildGraph(VFXGraph graph, VFXCompilationMode compilationMode)
        {
            var intermediateGraph = new TaskGraph();

            Clear();

            var models = new HashSet<ScriptableObject>();
            graph.CollectDependencies(models, false);

            m_ExpressionGraph = new VFXExpressionGraph();
            List<VFXContext> compilableContexts = new List<VFXContext>();
            List<VFXBasicSpawner> spawners = new List<VFXBasicSpawner>();
            foreach (var model in models)
            {
                if (model is VFXContext context && context.CanBeCompiled())
                {
                    var data = context.GetData();

                    switch (context)
                    {
                        case VFXBasicSpawner basicSpawner:
                        {
                            spawners.Add(basicSpawner);
                            break;
                        }
                        case VFXBasicInitialize initContext:
                        {
                            var particleSystemBuildInfo = GetParticleSystemBuildInfo(data);
                            Debug.Assert(particleSystemBuildInfo.InitContext == null);
                            particleSystemBuildInfo.InitContext = initContext;
                            break;
                        }
                        case VFXBasicUpdate updateContext:
                        {
                            var particleSystemBuildInfo = GetParticleSystemBuildInfo(data);
                            particleSystemBuildInfo.UpdateContexts.Add(updateContext);
                            break;
                        }
                        case VFXAbstractParticleOutput outputContext:
                        {
                            var particleSystemBuildInfo = GetParticleSystemBuildInfo(data);
                            particleSystemBuildInfo.OutputContexts.Add(outputContext);
                            break;
                        }
                    }
                    compilableContexts.Add(context);
                }
            }

            var expressionGraphOptions = compilationMode == VFXCompilationMode.Runtime ? VFXExpressionContextOption.ConstantFolding : VFXExpressionContextOption.Reduction;
            m_ExpressionGraph.CompileExpressions(compilableContexts, expressionGraphOptions);

            foreach (var spawner in spawners)
            {
                BuildSpawnerSystem(spawner, intermediateGraph);
            }
            foreach (var particleSystem in m_ParticleSystems.Values)
            {
                BuildParticleSystem(particleSystem, intermediateGraph);
            }

            //return new TaskGraph();
            return intermediateGraph;
        }

        void Clear()
        {
            m_ParticleSystems.Clear();
            m_GraphValueNameCounts.Clear();
            m_SystemNames = new VFXSystemNames();
            m_DeduplicatedExpressionsToDataView.Clear();
        }

        void BuildSpawnerSystem(VFXBasicSpawner spawner, TaskGraph intermediateGraph)
        {
            var spawnDataDescription = BuildSpawnerDataDescription(spawner.GetData());
            var spawnData = intermediateGraph.AddData(spawnDataDescription.Name, spawnDataDescription);

            // Propagate spawn data to linked contexts
            foreach (var outputContext in spawner.outputContexts)
            {
                var particleSystemBuildInfo = GetParticleSystemBuildInfo(outputContext.GetData());
                particleSystemBuildInfo.InputSpawnData = spawnData;
            }

            bool first = true;
            foreach (var block in spawner.activeFlattenedChildrenWithImplicit)
            {
                if (block is VFXAbstractSpawner spawnerBlock)
                {
                    SpawnerTask subTask = new SpawnerTask(block.name, spawnerBlock.spawnerType, kSpawnDataBindingKey);
                    var spawnerTaskNodeId = intermediateGraph.AddTask(subTask);
                    intermediateGraph.BindData(spawnerTaskNodeId, kSpawnDataBindingKey, spawnData, first ? BindingUsage.Write : BindingUsage.ReadWrite);
                    BindSpawnerExpressions(intermediateGraph, spawner, spawnerTaskNodeId);
                    first = false;
                }
            }
        }

        IDataDescription BuildSpawnerDataDescription(VFXData data)
        {
            return new SpawnData(m_SystemNames.GetUniqueSystemName(data));
        }

        ParticleSystemBuildInfo GetParticleSystemBuildInfo(VFXData data)
        {
            ParticleSystemBuildInfo particleSystemBuildInfo = null;
            if (!m_ParticleSystems.TryGetValue(data, out particleSystemBuildInfo))
            {
                particleSystemBuildInfo = new ParticleSystemBuildInfo(data);
                m_ParticleSystems.Add(data, particleSystemBuildInfo);
            }
            return particleSystemBuildInfo;
        }

        void BuildParticleSystem(ParticleSystemBuildInfo particleSystemBuildInfo, TaskGraph intermediateGraph)
        {
            //if (!Validate(particleSystem))
            //    return; // And log error

            var particleDataDescription = BuildParticleDataDescription(particleSystemBuildInfo.Data);
            var particleData = intermediateGraph.AddData(particleDataDescription.Name, particleDataDescription);

            var systemTask = intermediateGraph.AddTask(BuildSystemTask());
            intermediateGraph.BindData(systemTask, kParticleBindingKey, particleData, BindingUsage.Write);

            BuildGraphValuesBuffer(intermediateGraph, systemTask, out var graphValuesBufferViewId, out var contextDataViewId, out var graphValuesBuffer);

            var initializeTask = intermediateGraph.AddTask(BuildInitializeTask(particleSystemBuildInfo.InitContext));
            BindExpressions(intermediateGraph, particleSystemBuildInfo.InitContext, initializeTask, systemTask, graphValuesBuffer, graphValuesBufferViewId);
            BindContextData(intermediateGraph, initializeTask, contextDataViewId);

            var spawnData = particleSystemBuildInfo.InputSpawnData;
            if (spawnData.IsValid)
            {
                intermediateGraph.BindData(initializeTask, kSpawnDataBindingKey, spawnData, BindingUsage.Read);
            }
            intermediateGraph.BindData(initializeTask, kParticleBindingKey, particleData, BindingUsage.ReadWrite);

            foreach (var updateContext in particleSystemBuildInfo.UpdateContexts)
            {
                var updateTask = intermediateGraph.AddTask(BuildUpdateTask(updateContext));
                intermediateGraph.BindData(updateTask, kParticleBindingKey, particleData, BindingUsage.ReadWrite);
                BindExpressions(intermediateGraph, updateContext, updateTask, systemTask, graphValuesBuffer, graphValuesBufferViewId);
                BindContextData(intermediateGraph, updateTask, contextDataViewId);
            }

            foreach (var outputContext in particleSystemBuildInfo.OutputContexts)
            {
                var outputTask = intermediateGraph.AddTask(BuildOutputTask(outputContext));
                intermediateGraph.BindData(outputTask, kParticleBindingKey, particleData, BindingUsage.Read);
                BindExpressions(intermediateGraph, outputContext, outputTask, systemTask, graphValuesBuffer, graphValuesBufferViewId);
                BindContextData(intermediateGraph, outputTask, contextDataViewId);
            }
        }

        void BuildGraphValuesBuffer(TaskGraph intermediateGraph, TaskNodeId systemTask, out DataViewId graphValuesBufferViewId, out DataViewId contextDataViewId, out StructuredData graphValuesBuffer)
        {
            graphValuesBuffer = new StructuredData();
            graphValuesBufferViewId = intermediateGraph.AddData("GraphValuesBuffer", graphValuesBuffer);

            StructuredData contextData = new StructuredData();
            contextData.AddSubdata(TemplatedTask.MaxParticleCountKey, ValueData.Create(typeof(uint)));
            contextData.AddSubdata(TemplatedTask.SystemSeedKey, ValueData.Create(typeof(uint)));
            contextData.AddSubdata(TemplatedTask.InitSpawnIndexKey, ValueData.Create(typeof(uint)));
            NameDataKey paddingKey = new NameDataKey("padding");
            contextData.AddSubdata(paddingKey, ValueData.Create(typeof(uint)));

            graphValuesBuffer.AddSubdata(TemplatedTask.ContextDataKey, contextData); // Adds a default subdata for ContextData, which is expected from the C++ runtime.

            contextDataViewId = intermediateGraph.GetSubdata(graphValuesBufferViewId, TemplatedTask.ContextDataKey);
            intermediateGraph.GetSubdata(contextDataViewId, paddingKey); // Force data view to be registered

            UnorderedData graphValuesUnordered = new UnorderedData();
            graphValuesBuffer.AddSubdata(kGraphValuesKey, graphValuesUnordered);

            intermediateGraph.BindData(systemTask, TemplatedTask.GraphValuesBufferKey, graphValuesBufferViewId, BindingUsage.Write);

            m_GraphValueNameCounts.Add(graphValuesUnordered, new Dictionary<string, uint>());
        }

        void BindContextData(TaskGraph intermediateGraph, TaskNodeId contextTask, DataViewId contextDataViewId)
        {
            intermediateGraph.BindData(contextTask, TemplatedTask.ContextDataKey, contextDataViewId, BindingUsage.Read);
        }

        void BindSpawnerExpressions(TaskGraph intermediateGraph, VFXBasicSpawner spawner, TaskNodeId spawnerTaskNodeId)
        {
            // For spawner, we only bind CPU expressions and we do not use the full name for the binding.
            var cpuMapper = m_ExpressionGraph.BuildCPUMapper(spawner);
            foreach (var expression in cpuMapper.expressions)
            {
                var expressionDataViewId = AddExpressionRecursively(intermediateGraph, expression);
                string bindingName = cpuMapper.GetData(expression)[0].name;
                intermediateGraph.BindData(spawnerTaskNodeId, new NameDataKey(bindingName), expressionDataViewId, BindingUsage.Read);
            }
        }

        void BindExpressions(TaskGraph intermediateGraph, VFXContext context, TaskNodeId contextTask, TaskNodeId systemTask, StructuredData graphValuesBuffer, DataViewId graphValuesViewId)
        {
            BindGPUExpressions(intermediateGraph, context, contextTask, systemTask, graphValuesBuffer, graphValuesViewId);
            BindCPUExpressions(intermediateGraph, context, contextTask, systemTask);
        }

        void BindGPUExpressions(TaskGraph intermediateGraph, VFXContext context, TaskNodeId contextTask, TaskNodeId systemTask, StructuredData graphValuesBuffer, DataViewId graphValuesBufferViewId)
        {
            var graphValuesViewId = intermediateGraph.GetSubdata(graphValuesBufferViewId, kGraphValuesKey);
            var graphValuesUnordered = intermediateGraph.DataViews[graphValuesViewId].DataDescription as UnorderedData;
            Debug.Assert(graphValuesUnordered != null);
            Debug.Assert(m_GraphValueNameCounts.ContainsKey(graphValuesUnordered));

            var gpuMapper = m_ExpressionGraph.BuildGPUMapper(context);
            var uniformMapper = new VFXUniformMapper(gpuMapper, false, false);

            foreach (var expression in uniformMapper.textures)
            {
                var expressionDataViewId = AddExpressionRecursively(intermediateGraph, expression);
                string bindingName = uniformMapper.GetName(expression);
                IDataKey bindingKey = new NameDataKey(bindingName);
                intermediateGraph.BindData(contextTask, bindingKey, expressionDataViewId, BindingUsage.Read);
            }
            foreach (var expression in uniformMapper.buffers)
            {
                var expressionDataViewId = AddExpressionRecursively(intermediateGraph, expression);
                string bindingName = uniformMapper.GetName(expression);
                IDataKey bindingKey = new NameDataKey(bindingName);
                intermediateGraph.BindData(contextTask, bindingKey, expressionDataViewId, BindingUsage.Read);
            }

            foreach (var expression in uniformMapper.uniforms)
            {
                var expressionDataViewId = AddExpressionRecursively(intermediateGraph, expression);
                if(expression.Is(VFXExpression.Flags.Constant))
                    continue;
                if(expression.IsAny(VFXExpression.Flags.NotCompilableOnCPU))
                    continue;
                Debug.Assert(VFXExpression.IsUniform(expression.valueType));

                var expressionValue = intermediateGraph.DataViews[expressionDataViewId].DataDescription;
                string bindingName = uniformMapper.GetName(expression);

                bool addToGraphValues = graphValuesUnordered.GetSubDataKey(expressionValue) == null;
                if (addToGraphValues)
                {
                    var nameCountMap = m_GraphValueNameCounts[graphValuesUnordered];
                    if (nameCountMap.TryGetValue(bindingName, out uint count))
                    {
                        nameCountMap[bindingName] = count + 1;
                    }
                    else
                    {
                        count = 0;
                        nameCountMap.Add(bindingName, 1);
                    }
                    string systemUniqueBindingName = $"{bindingName}_{VFXCodeGeneratorHelper.GeneratePrefix(count)}";
                    IDataKey systemBindingKey = new NameDataKey(systemUniqueBindingName);
                    Debug.Assert(graphValuesUnordered.GetSubdata(systemBindingKey) == null, $"Graph value with name {systemUniqueBindingName} already exists.");
                    graphValuesUnordered.AddSubdata(systemBindingKey, expressionValue);
                    intermediateGraph.BindData(systemTask, systemBindingKey, expressionDataViewId, BindingUsage.Read);
                }

                IDataKey contextBindingKey = new NameDataKey(bindingName);
                var subdataViewId = intermediateGraph.GetSubdata(graphValuesViewId, graphValuesUnordered.GetSubDataKey(expressionValue));
                intermediateGraph.BindData(contextTask, contextBindingKey, subdataViewId, BindingUsage.Read);
            }
        }

        void BindCPUExpressions(TaskGraph intermediateGraph, VFXContext context, TaskNodeId contextTask, TaskNodeId systemTask)
        {
            var cpuMapper = m_ExpressionGraph.BuildCPUMapper(context);

            TaskNodeId targetNodeId = context is VFXBasicSpawner ? contextTask : systemTask;

            foreach (var expression in cpuMapper.expressions)
            {
                var expressionDataViewId = AddExpressionRecursively(intermediateGraph, expression);
                string bindingName = cpuMapper.GetData(expression)[0].fullName;
                intermediateGraph.BindData(targetNodeId, new NameDataKey(bindingName), expressionDataViewId, BindingUsage.Read);
            }
        }

        IDataDescription BuildParticleDataDescription(VFXData data)
        {
            uint capacity = (uint)data.GetSettingValue("capacity");
            return new ParticleData(m_SystemNames.GetUniqueSystemName(data), new Bounds(), data is ISpaceable spaceable ? spaceable.space : VFXSpace.None, capacity);
        }

        ITask BuildSystemTask()
        {
            return new PlaceholderSystemTask(
                new List<(IDataKey, IExpression)>(),
                new List<BindingRelativePath>()
            {
                new(kParticleBindingKey, DataPath.Empty),
                new(TemplatedTask.GraphValuesBufferKey, DataPath.Empty)
            });
        }

        ITask BuildInitializeTask(VFXBasicInitialize initContext)
        {
            AttributeSet particleAttributes = new AttributeSet();
            particleAttributes.AddAttribute(VFXAttributesManager.ConvertToNewCompiler(VFXAttribute.Alive), AttributeUsage.Write);
            particleAttributes.AddAttribute(VFXAttributesManager.ConvertToNewCompiler(VFXAttribute.ParticleId), AttributeUsage.Write);
            particleAttributes.AddAttribute(VFXAttributesManager.ConvertToNewCompiler(VFXAttribute.Seed), AttributeUsage.Write);

            BindingUsagePaths particleSystemUsage = new();
            particleSystemUsage.Add(kAttributeDataPath, particleAttributes);
            //TODO: Is this where it should be added? Maybe not the dead list, but the id tracker? Which is the same key here anyways
            particleSystemUsage.Read.Add(new DataPath(ParticleData.DeadlistKey));
            particleSystemUsage.Write.Add(new DataPath(ParticleData.DeadlistKey));

            BindingUsagePaths spawnSystemUsage = new();
            spawnSystemUsage.Read.Add(new DataPath(SpawnData.SourceAttributeDataKey));

            BindingUsagePaths contextDataUsage = new();
            contextDataUsage.Read.Add(TemplatedTask.MaxParticleCountPath);
            contextDataUsage.Read.Add(TemplatedTask.SystemSeedPath);
            contextDataUsage.Read.Add(TemplatedTask.InitSpawnIndexPath);

            var args = new TemplatedTaskArgs
            {
                Subtasks = m_SubTaskBuilder.GenerateSubtasks(initContext, m_ExpressionGraph),

                AttributeKeyMappings = new()
                {
                    [AttributeData.DefaultKey] = new(kParticleBindingKey, kAttributeDataPath) // TODO: Use a proper key for particle attributes and just provide which one is default
                },

                Bindings = new()
                {
                    [kParticleBindingKey] = new(typeof(ParticleData), particleSystemUsage),
                    [kSpawnDataBindingKey] = new(typeof(SpawnData), spawnSystemUsage),
                    [TemplatedTask.ContextDataKey] = new(typeof(ValueData), contextDataUsage)
                }
            };
            return new TemplatedTask("Init", args);
        }

        ITask BuildUpdateTask(VFXBasicUpdate updateContext)
        {
            AttributeSet particleAttributes = new AttributeSet();
            particleAttributes.AddAttribute(VFXAttributesManager.ConvertToNewCompiler(VFXAttribute.Alive), AttributeUsage.Read);

            BindingUsagePaths particleSystemUsage = new();
            particleSystemUsage.Add(kAttributeDataPath, particleAttributes);
            //TODO: Is this where it should be added? Maybe not the dead list, but the id tracker? Which is the same key here anyways
            particleSystemUsage.Read.Add(new DataPath(ParticleData.DeadlistKey));
            particleSystemUsage.Write.Add(new DataPath(ParticleData.DeadlistKey));

            BindingUsagePaths contextDataUsage = new();
            contextDataUsage.Read.Add(TemplatedTask.MaxParticleCountPath);
            contextDataUsage.Read.Add(TemplatedTask.SystemSeedPath);

            TemplatedTaskArgs args = new TemplatedTaskArgs
            {
                Subtasks = m_SubTaskBuilder.GenerateSubtasks(updateContext, m_ExpressionGraph),

                AttributeKeyMappings = new()
                {
                    [AttributeData.DefaultKey] = new(kParticleBindingKey, kAttributeDataPath)
                },

                Bindings = new()
                {
                    [kParticleBindingKey] = new(typeof(ParticleData), particleSystemUsage),
                    [TemplatedTask.ContextDataKey] = new(typeof(ValueData), contextDataUsage)
                }
            };

            return new TemplatedTask("Update", args);
        }

        ITask BuildOutputTask(VFXAbstractParticleOutput outputContext)
        {
            List<VFXAttribute> s_ReadAttributes = new()
            {
                VFXAttribute.Alive,
                VFXAttribute.Color,
                VFXAttribute.Alpha,
                VFXAttribute.Position,
                VFXAttribute.Size,
                VFXAttribute.ScaleX,
                VFXAttribute.ScaleY,
                VFXAttribute.ScaleZ
            };

            AttributeSet particleAttributes = new AttributeSet();
            foreach (var attribute in s_ReadAttributes)
            {
                particleAttributes.AddAttribute(VFXAttributesManager.ConvertToNewCompiler(attribute), AttributeUsage.Read);
            }

            BindingUsagePaths particleSystemUsage = new();
            particleSystemUsage.Add(kAttributeDataPath, particleAttributes);

            BindingUsagePaths mainTextureUsage = new();
            mainTextureUsage.Read.Add(DataPath.Empty);

            TemplatedTaskArgs args = new TemplatedTaskArgs
            {
                Subtasks = m_SubTaskBuilder.GenerateSubtasks(outputContext, m_ExpressionGraph),

                AttributeKeyMappings = new()
                {
                    [AttributeData.DefaultKey] = new(kParticleBindingKey, kAttributeDataPath)
                },

                Bindings = new()
                {
                    [kParticleBindingKey] = new(typeof(ParticleData), particleSystemUsage),
                    [kMainTextureKey] = new(typeof(ValueData<Texture2D>), mainTextureUsage)
                }
            };

            return new TemplatedTask("Output", args, false);
        }

        DataViewId AddExpressionRecursively(TaskGraph taskGraph, VFXExpression expression)
        {
            if (m_DeduplicatedExpressionsToDataView.TryGetValue(expression, out var cachedDataViewId))
            {
                return cachedDataViewId;
            }

            LegacyExpressionTask expressionTask = new LegacyExpressionTask(expression);
            var taskNodeId = taskGraph.AddTask(expressionTask);

            var parents = expression.parents;
            for (int i = 0; i < parents.Length; ++i)
            {
                taskGraph.BindData(taskNodeId, new IndexDataKey(i), AddExpressionRecursively(taskGraph, parents[i]), BindingUsage.Read);
            }
            var dataDescription = ValueData.Create(VFXValueTypeToType(expression.valueType));
            var dataViewId = taskGraph.AddData($"{expression.GetType().Name}_{taskGraph.DataContainers.Count}", dataDescription);
            taskGraph.BindData(taskNodeId, LegacyExpressionTask.Value, dataViewId, BindingUsage.Write);
            m_DeduplicatedExpressionsToDataView[expression] = dataViewId;
            return dataViewId;
        }

        public static Type VFXValueTypeToType(VFXValueType type)
        {
            switch (type)
            {
                case VFXValueType.Float: return typeof(float);
                case VFXValueType.Float2: return typeof(Vector2);
                case VFXValueType.Float3: return typeof(Vector3);
                case VFXValueType.Float4: return typeof(Vector4);
                case VFXValueType.Int32: return typeof(int);
                case VFXValueType.Uint32: return typeof(uint);
                case VFXValueType.Texture2D: return typeof(Texture2D);
                case VFXValueType.Texture2DArray: return typeof(Texture2DArray);
                case VFXValueType.Texture3D: return typeof(Texture3D);
                case VFXValueType.TextureCube: return typeof(Cubemap);
                case VFXValueType.TextureCubeArray: return typeof(CubemapArray);
                case VFXValueType.CameraBuffer: return typeof(CameraBuffer);
                case VFXValueType.Matrix4x4: return typeof(Matrix4x4);
                case VFXValueType.Mesh: return typeof(Mesh);
                case VFXValueType.Curve: return typeof(AnimationCurve);
                case VFXValueType.ColorGradient: return typeof(Gradient);
                case VFXValueType.Boolean: return typeof(bool);
                case VFXValueType.Buffer: return typeof(GraphicsBuffer);
            }
            throw new NotImplementedException(type.ToString());
        }
    }
}
