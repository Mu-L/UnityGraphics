using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Collections.ObjectModel;
using System.Text;

using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace UnityEditor.VFX
{
    enum VFXContextBufferSizeMode
    {
        FixedSize,
        ScaleWithCapacity,
        FixedSizePlusScaleWithCapacity,
    }

    struct VFXContextBufferDescriptor
    {
        public uint bufferCount;
        public uint stride;
        public bool isPerCamera;
        public string baseName;
        public VFXContextBufferSizeMode bufferSizeMode;
        public uint size;
        public GraphicsBuffer.Target bufferTarget;
        public bool includeInSystemMappings;
        public float capacityScaleMultiplier;
    }

    struct VFXContextCompiledData
    {
        public List<VFXTask> tasks;
        public List<VFXContextBufferDescriptor> buffers;
        public (VFXSlot slot, VFXData data)[] linkedEventOut;

        public int AllocateIndirectBuffer(bool isPerCamera = true, uint stride = 4u, string overrideBufferName = null, uint bufferCount = 1)
        {
            buffers.Add(new VFXContextBufferDescriptor
            {
                bufferSizeMode = VFXContextBufferSizeMode.FixedSizePlusScaleWithCapacity,
                size = 1, // Add 1 to the buffer size to hold the counter in index 0
                capacityScaleMultiplier = 1,
                baseName = overrideBufferName ?? VFXDataParticle.k_IndirectBufferName,
                isPerCamera = isPerCamera,
                stride = stride,
                bufferCount = bufferCount,
                bufferTarget = GraphicsBuffer.Target.Structured,
                includeInSystemMappings = true,
            });

            return buffers.Count - 1;
        }
    }

    struct VFXTaskCompiledData
    {
        public VFXExpressionMapper cpuMapper;
        public VFXExpressionMapper gpuMapper;
        public VFXUniformMapper uniformMapper;
        public VFXSGInputs SGInputs;
        public List<uint> instancingSplitValues;
        public ReadOnlyDictionary<VFXExpression, BufferType> bufferTypeUsage;
        public VFXMapping[] parameters;
        public (VFXSlot slot, VFXData data)[] linkedEventOut;
        public IHLSLCodeHolder[] hlslCodeHolders;
        public int indexInShaderSource;
    }

    struct VFXCompiledData
    {
        public Dictionary<VFXTask, VFXTaskCompiledData> taskToCompiledData;
        public Dictionary<VFXContext, VFXContextCompiledData> contextToCompiledData;
    }

    class VFXDependentBuffersData
    {
        public Dictionary<VFXData, int> attributeBuffers = new Dictionary<VFXData, int>();
        public Dictionary<VFXData, int> stripBuffers = new Dictionary<VFXData, int>();
        public Dictionary<VFXData, int> eventBuffers = new Dictionary<VFXData, int>();
        public Dictionary<VFXData, int> boundsBuffers = new Dictionary<VFXData, int>();
    }

    class VFXGraphCompiledData
    {
        // 3: Serialize material
        // 4: Bounds helper change
        // 5: HasAttributeBuffer flag
        // 6: needsComputeBounds needs Sanitization
        // 7: changes in data serialization and additional mappings added to runtime data (graphValueOffset and parentSystemIndex)
        public const uint compiledVersion = 7;

        public VFXGraphCompiledData(VFXGraph graph)
        {
            if (graph == null)
                throw new ArgumentNullException("VFXGraph cannot be null");
            m_Graph = graph;
        }

        private struct GeneratedCodeData
        {
            public VFXContext context;
            public VFXTask task;
            public bool computeShader;
            public System.Text.StringBuilder content;
            public VFXCompilationMode compilMode;
        }

        private static VFXExpressionObjectValueContainerDesc<T> CreateObjectValueDesc<T>(VFXExpression exp, int expIndex)
        {
            var desc = new VFXExpressionObjectValueContainerDesc<T>();
            desc.entityId = exp.Get<EntityId>();
            return desc;
        }

        private static VFXExpressionValueContainerDesc<T> CreateValueDesc<T>(VFXExpression exp, int expIndex)
        {
            var desc = new VFXExpressionValueContainerDesc<T>();
            desc.value = exp.Get<T>();
            return desc;
        }

        private void SetValueDesc<T>(VFXExpressionValueContainerDesc desc, VFXExpression exp)
        {
            ((VFXExpressionValueContainerDesc<T>)desc).value = exp.Get<T>();
        }

        private void SetObjectValueDesc<T>(VFXExpressionValueContainerDesc desc, VFXExpression exp)
        {
            ((VFXExpressionObjectValueContainerDesc<T>)desc).entityId = exp.Get<EntityId>();
        }

        public uint FindReducedExpressionIndexFromSlotCPU(VFXSlot slot)
        {
            if (m_ExpressionGraph == null)
            {
                return uint.MaxValue;
            }
            var targetExpression = slot.GetExpression();
            if (targetExpression == null)
            {
                return uint.MaxValue;
            }

            if (!m_ExpressionGraph.CPUExpressionsToReduced.ContainsKey(targetExpression))
            {
                return uint.MaxValue;
            }

            var ouputExpression = m_ExpressionGraph.CPUExpressionsToReduced[targetExpression];
            return (uint)m_ExpressionGraph.GetFlattenedIndex(ouputExpression);
        }

        private static void FillExpressionDescs(VFXExpressionGraph graph, List<VFXExpressionDesc> outExpressionCommonDescs, List<VFXExpressionDesc> outExpressionPerSpawnEventDescs, List<VFXExpressionValueContainerDesc> outValueDescs)
        {
            var flatGraph = graph.FlattenedExpressions;
            var numFlattenedExpressions = flatGraph.Count;

            var maxCommonExpressionIndex = (uint)numFlattenedExpressions;
            for (int i = 0; i < numFlattenedExpressions; ++i)
            {
                var exp = flatGraph[i];
                if (exp.Is(VFXExpression.Flags.PerSpawn) && maxCommonExpressionIndex == numFlattenedExpressions)
                    maxCommonExpressionIndex = (uint)i;

                if (!exp.Is(VFXExpression.Flags.PerSpawn) && maxCommonExpressionIndex != numFlattenedExpressions)
                    throw new InvalidOperationException("Not contiguous expression VFXExpression.Flags.PerSpawn detected");

                // Must match data in C++ expression
                if (exp.Is(VFXExpression.Flags.Value))
                {
                    VFXExpressionValueContainerDesc value;
                    switch (exp.valueType)
                    {
                        case VFXValueType.Float: value = CreateValueDesc<float>(exp, i); break;
                        case VFXValueType.Float2: value = CreateValueDesc<Vector2>(exp, i); break;
                        case VFXValueType.Float3: value = CreateValueDesc<Vector3>(exp, i); break;
                        case VFXValueType.Float4: value = CreateValueDesc<Vector4>(exp, i); break;
                        case VFXValueType.Int32: value = CreateValueDesc<int>(exp, i); break;
                        case VFXValueType.Uint32: value = CreateValueDesc<uint>(exp, i); break;
                        case VFXValueType.Texture2D:
                        case VFXValueType.Texture2DArray:
                        case VFXValueType.Texture3D:
                        case VFXValueType.TextureCube:
                        case VFXValueType.TextureCubeArray:
                            value = CreateObjectValueDesc<Texture>(exp, i);
                            break;
                        case VFXValueType.CameraBuffer: value = CreateObjectValueDesc<Texture>(exp, i); break;
                        case VFXValueType.Matrix4x4: value = CreateValueDesc<Matrix4x4>(exp, i); break;
                        case VFXValueType.Curve: value = CreateValueDesc<AnimationCurve>(exp, i); break;
                        case VFXValueType.ColorGradient: value = CreateValueDesc<Gradient>(exp, i); break;
                        case VFXValueType.Mesh: value = CreateObjectValueDesc<Mesh>(exp, i); break;
                        case VFXValueType.SkinnedMeshRenderer: value = CreateObjectValueDesc<SkinnedMeshRenderer>(exp, i); break;
                        case VFXValueType.Boolean: value = CreateValueDesc<bool>(exp, i); break;
                        case VFXValueType.Buffer: value = CreateValueDesc<GraphicsBuffer>(exp, i); break;
                        default: throw new InvalidOperationException("Invalid type : " + exp.valueType);
                    }
                    value.expressionIndex = (uint)i;
                    outValueDescs.Add(value);
                }

                var outExpressionsDesc = i >= maxCommonExpressionIndex ? outExpressionPerSpawnEventDescs : outExpressionCommonDescs;
                outExpressionsDesc.Add(new VFXExpressionDesc
                {
                    op = exp.operation,
                    data = exp.GetOperands(graph).ToArray(),
                });
            }
        }

        private static void CollectExposedDesc(List<(VFXMapping, VFXSpace, SpaceableType)> outExposedParameters, string name, VFXSlot slot, VFXExpressionGraph graph)
        {
            var expression = slot.valueType != VFXValueType.None ? slot.GetInExpression() : null;
            if (expression != null)
            {
                var exprIndex = graph.GetFlattenedIndex(expression);
                if (exprIndex == -1)
                    throw new InvalidOperationException("Unable to retrieve value from exposed for " + name);

                var space = slot.space;
                var spaceableType = SpaceableType.None;
                if (space != VFXSpace.None)
                    spaceableType = slot.GetSpaceTransformationType();

                outExposedParameters.Add((
                        new VFXMapping()
                        {
                            name = name,
                            index = exprIndex
                        },
                        space,
                        spaceableType
                    ));
            }
            else
            {
                foreach (var child in slot.children)
                {
                    CollectExposedDesc(outExposedParameters, name + "_" + child.name, child, graph);
                }
            }
        }

        private static void FillExposedDescs(List<(VFXMapping, VFXSpace, SpaceableType)> outExposedParameters, VFXExpressionGraph graph, IEnumerable<VFXParameter> parameters)
        {
            foreach (var parameter in parameters)
            {
                if (parameter.exposed && !parameter.isOutput)
                {
                    CollectExposedDesc(outExposedParameters, parameter.exposedName, parameter.GetOutputSlot(0), graph);
                }
            }
        }

        class VFXSpawnContextLayer
        {
            public VFXContext context;
            public int depth;
        }

        private static List<VFXSpawnContextLayer> CollectContextParentRecursively(IEnumerable<VFXContext> inputList, ref SubgraphInfos subgraphContexts, int currentDepth = 0)
        {
            var contextEffectiveInputLinks = subgraphContexts.contextEffectiveInputLinks;
            var contextList = inputList.SelectMany(o => contextEffectiveInputLinks[o].SelectMany(t => t))
                .Select(t => t.context).Distinct()
                .Select(c => new VFXSpawnContextLayer()
                {
                    context = c,
                    depth = currentDepth
                }).ToList();

            if (contextList.Any(o => contextEffectiveInputLinks[o.context].Any()))
            {
                var parentContextList = CollectContextParentRecursively(contextList.Select(c => c.context), ref subgraphContexts, currentDepth + 1);
                foreach (var parentContextEntry in parentContextList)
                {
                    var currentEntry = contextList.FirstOrDefault(o => o.context == parentContextEntry.context);
                    if (currentEntry == null)
                    {
                        contextList.Add(parentContextEntry);
                    }
                    else if (parentContextEntry.depth > currentEntry.depth)
                    {
                        currentEntry.depth = parentContextEntry.depth;
                    }
                }
            }
            return contextList;
        }

        private static VFXContext[] CollectSpawnersHierarchy(IEnumerable<VFXContext> vfxContext, ref SubgraphInfos subgraphContexts)
        {
            var initContext = vfxContext.Where(o => o.contextType == VFXContextType.Init || o.contextType == VFXContextType.OutputEvent).ToList();
            var spawnerHierarchy = CollectContextParentRecursively(initContext, ref subgraphContexts);
            var spawnerList = spawnerHierarchy.Where(o => o.context.contextType == VFXContextType.Spawner)
                .OrderByDescending(o => o.depth)
                .Select(o => o.context).ToArray();
            return spawnerList;
        }

        struct SpawnInfo
        {
            public int bufferIndex;
            public int systemIndex;
        }

        private static VFXCPUBufferData ComputeArrayOfStructureInitialData(IEnumerable<VFXLayoutElementDesc> layout, VFXGraph vfxGraph)
        {
            var data = new VFXCPUBufferData();
            foreach (var element in layout)
            {
                vfxGraph.attributesManager.TryFind(element.name, out var attribute);
                bool useAttribute = attribute.name == element.name;
                if (element.type == VFXValueType.Boolean)
                {
                    var v = useAttribute ? attribute.value.Get<bool>() : default(bool);
                    data.PushBool(v);
                }
                else if (element.type == VFXValueType.Float)
                {
                    var v = useAttribute ? attribute.value.Get<float>() : default(float);
                    data.PushFloat(v);
                }
                else if (element.type == VFXValueType.Float2)
                {
                    var v = useAttribute ? attribute.value.Get<Vector2>() : default(Vector2);
                    data.PushFloat(v.x);
                    data.PushFloat(v.y);
                }
                else if (element.type == VFXValueType.Float3)
                {
                    var v = useAttribute ? attribute.value.Get<Vector3>() : default(Vector3);
                    data.PushFloat(v.x);
                    data.PushFloat(v.y);
                    data.PushFloat(v.z);
                }
                else if (element.type == VFXValueType.Float4)
                {
                    var v = useAttribute ? attribute.value.Get<Vector4>() : default(Vector4);
                    data.PushFloat(v.x);
                    data.PushFloat(v.y);
                    data.PushFloat(v.z);
                    data.PushFloat(v.w);
                }
                else if (element.type == VFXValueType.Int32)
                {
                    var v = useAttribute ? attribute.value.Get<int>() : default(int);
                    data.PushInt(v);
                }
                else if (element.type == VFXValueType.Uint32)
                {
                    var v = useAttribute ? attribute.value.Get<uint>() : default(uint);
                    data.PushUInt(v);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
            return data;
        }

        void RecursePutSubgraphParent(Dictionary<VFXSubgraphContext, VFXSubgraphContext> parents, List<VFXSubgraphContext> subgraphs, VFXSubgraphContext subgraph)
        {
            foreach (var subSubgraph in subgraph.subChildren.OfType<VFXSubgraphContext>().Where(t => t.subgraph != null))
            {
                subgraphs.Add(subSubgraph);
                parents[subSubgraph] = subgraph;

                RecursePutSubgraphParent(parents, subgraphs, subSubgraph);
            }
        }

        static List<VFXContextLink>[] ComputeContextEffectiveLinks(VFXContext context, ref SubgraphInfos subgraphInfos)
        {
            List<VFXContextLink>[] result = new List<VFXContextLink>[context.inputFlowSlot.Length];
            Dictionary<string, int> eventNameIndice = new Dictionary<string, int>();
            for (int i = 0; i < context.inputFlowSlot.Length; ++i)
            {
                result[i] = new List<VFXContextLink>();
                VFXSubgraphContext parentSubgraph = null;

                subgraphInfos.spawnerSubgraph.TryGetValue(context, out parentSubgraph);

                List<VFXContext> subgraphAncestors = new List<VFXContext>();

                subgraphAncestors.Add(context);

                while (parentSubgraph != null)
                {
                    subgraphAncestors.Add(parentSubgraph);
                    if (!subgraphInfos.subgraphParents.TryGetValue(parentSubgraph, out parentSubgraph))
                    {
                        parentSubgraph = null;
                    }
                }

                List<List<int>> defaultEventPaths = new List<List<int>>();

                defaultEventPaths.Add(new List<int>(new int[] { i }));

                List<List<int>> newEventPaths = new List<List<int>>();

                for (int j = 0; j < subgraphAncestors.Count; ++j)
                {
                    var sg = subgraphAncestors[j];
                    var nextSg = j < subgraphAncestors.Count - 1 ? subgraphAncestors[j + 1] as VFXSubgraphContext : null;

                    foreach (var path in defaultEventPaths)
                    {
                        int currentFlowIndex = path.Last();
                        var eventSlot = sg.inputFlowSlot[currentFlowIndex];
                        var eventSlotSpawners = eventSlot.link.Where(t => t.context.contextType == VFXContextType.Spawner);
                        result[i].AddRange(eventSlotSpawners);

                        var eventSlotEvents = eventSlot.link.Where(t => t.context is VFXBasicEvent);

                        if (eventSlotEvents.Any())
                        {
                            foreach (var evt in eventSlotEvents)
                            {
                                string eventName = (evt.context as VFXBasicEvent).eventName;

                                switch (eventName)
                                {
                                    case VisualEffectAsset.PlayEventName:
                                        if (nextSg != null)
                                            newEventPaths.Add(path.Concat(new int[] { 0 }).ToList());
                                        else
                                            result[i].Add(evt);
                                        break;
                                    case VisualEffectAsset.StopEventName:
                                        if (nextSg != null)
                                            newEventPaths.Add(path.Concat(new int[] { 1 }).ToList());
                                        else
                                            result[i].Add(evt);
                                        break;
                                    default:
                                    {
                                        if (nextSg != null)
                                        {
                                            int eventIndex = nextSg.GetInputFlowIndex(eventName);
                                            if (eventIndex != -1)
                                                newEventPaths.Add(path.Concat(new int[] { eventIndex }).ToList());
                                        }
                                        else
                                        {
                                            result[i].Add(evt);
                                        }
                                    }
                                    break;
                                }
                            }
                        }
                        else if (!eventSlot.link.Any())
                        {
                            if (!(sg is VFXSubgraphContext))
                            {
                                if (nextSg != null)
                                {
                                    int fixedSlotIndex = currentFlowIndex > 1 ? currentFlowIndex : nextSg.GetInputFlowIndex(currentFlowIndex == 1 ? VisualEffectAsset.StopEventName : VisualEffectAsset.PlayEventName);
                                    if (fixedSlotIndex >= 0)
                                        newEventPaths.Add(path.Concat(new int[] { fixedSlotIndex }).ToList());
                                }
                                else
                                    newEventPaths.Add(path.Concat(new int[] { currentFlowIndex }).ToList());
                            }
                            else
                            {
                                var sgsg = sg as VFXSubgraphContext;

                                var eventName = sgsg.GetInputFlowName(currentFlowIndex);

                                var eventCtx = sgsg.GetEventContext(eventName);
                                if (eventCtx != null)
                                    result[i].Add(new VFXContextLink() { slotIndex = 0, context = eventCtx });
                            }
                        }
                    }
                    defaultEventPaths.Clear();
                    defaultEventPaths.AddRange(newEventPaths);
                    newEventPaths.Clear();
                }
            }
            return result;
        }

        private class ProcessChunk
        {
            public int startIndex;
            public int endIndex;
        }

        static VFXMapping[] ComputePreProcessExpressionForSpawn(IEnumerable<VFXExpression> expressionPerSpawnToProcess, VFXExpressionGraph graph)
        {
            var allExpressions = new HashSet<VFXExpression>();
            foreach (var expression in expressionPerSpawnToProcess)
                VFXExpression.CollectParentExpressionRecursively(expression, allExpressions);

            var expressionIndexes = allExpressions.
                Where(o => o.Is(VFXExpression.Flags.PerSpawn)) //Filter only per spawn part of graph
                .Select(o => graph.GetFlattenedIndex(o))
                .OrderBy(i => i);

            //Additional verification of appropriate expected expression index
            //In flatten expression, all common expressions are sorted first, then, we have chunk of additional preprocess
            //We aren't supposed to happen a chunk which is running common expression here.
            if (expressionIndexes.Any(i => i < graph.CommonExpressionCount))
            {
                var expressionInCommon = allExpressions
                    .Where(o => graph.GetFlattenedIndex(o) < graph.CommonExpressionCount)
                    .OrderBy(o => graph.GetFlattenedIndex(o));
                Debug.LogErrorFormat("Unexpected preprocess expression detected : {0} (count)", expressionInCommon.Count());
            }

            var processChunk = new List<ProcessChunk>();
            int previousIndex = int.MinValue;
            foreach (var indice in expressionIndexes)
            {
                if (indice != previousIndex + 1)
                    processChunk.Add(new ProcessChunk()
                    {
                        startIndex = indice,
                        endIndex = indice + 1
                    });
                else
                    processChunk.Last().endIndex = indice + 1;
                previousIndex = indice;
            }

            return processChunk.SelectMany((o, i) =>
            {
                var prefix = VFXCodeGeneratorHelper.GeneratePrefix((uint)i);
                return new[]
                {
                    new VFXMapping
                    {
                        name = "start_" + prefix,
                        index = o.startIndex
                    },
                    new VFXMapping
                    {
                        name = "end_" + prefix,
                        index = o.endIndex
                    }
                };
            }).ToArray();
        }

        private static VFXEditorTaskDesc[] BuildEditorTaskDescFromBlockSpawner(IEnumerable<VFXBlock> blocks, VFXTaskCompiledData taskData, VFXExpressionGraph graph)
        {
            var taskDescList = new List<VFXEditorTaskDesc>();

            int index = 0;
            foreach (var b in blocks)
            {
                var spawnerBlock = b as VFXAbstractSpawner;
                if (spawnerBlock == null)
                {
                    throw new InvalidCastException("Unexpected block type in spawnerContext");
                }
                if (spawnerBlock.spawnerType == VFXTaskType.CustomCallbackSpawner && spawnerBlock.customBehavior == null)
                {
                    throw new InvalidOperationException("VFXAbstractSpawner excepts a custom behavior for custom callback type");
                }
                if (spawnerBlock.spawnerType != VFXTaskType.CustomCallbackSpawner && spawnerBlock.customBehavior != null)
                {
                    throw new InvalidOperationException("VFXAbstractSpawner only expects a custom behavior for custom callback type");
                }

                var mappingList = new List<VFXMapping>();
                var expressionPerSpawnToProcess = new List<VFXExpression>();
                foreach (var namedExpression in taskData.cpuMapper.CollectExpression(index, false))
                {
                    mappingList.Add(new VFXMapping()
                    {
                        index = graph.GetFlattenedIndex(namedExpression.exp),
                        name = namedExpression.name
                    });

                    if (namedExpression.exp.Is(VFXExpression.Flags.PerSpawn))
                        expressionPerSpawnToProcess.Add(namedExpression.exp);
                }

                if (expressionPerSpawnToProcess.Any())
                {
                    var mappingPreProcess = ComputePreProcessExpressionForSpawn(expressionPerSpawnToProcess, graph);
                    var preProcessTask = new VFXEditorTaskDesc
                    {
                        type = UnityEngine.VFX.VFXTaskType.EvaluateExpressionsSpawner,
                        buffers = Array.Empty<VFXMapping>(),
                        values = mappingPreProcess,
                        parameters = taskData.parameters,
                        processor = null,
                        shaderSourceIndex = -1
                    };
                    taskDescList.Add(preProcessTask);
                }

                Object processor = null;
                if (spawnerBlock.customBehavior != null)
                    processor = spawnerBlock.customBehavior;

                taskDescList.Add(new VFXEditorTaskDesc
                {
                    type = (UnityEngine.VFX.VFXTaskType)spawnerBlock.spawnerType,
                    buffers = Array.Empty<VFXMapping>(),
                    values = GetSortedUniformValues(mappingList),
                    parameters = taskData.parameters,
                    processor = processor,
                    shaderSourceIndex = -1
                });
                index++;
            }

            return taskDescList.ToArray();
        }

        private static VFXMapping[] GetSortedUniformValues(List<VFXMapping> mappingList)
        {
            // Order by index, except activation slot, that should be first
            return mappingList.OrderBy(o => o.name == VFXBlock.activationSlotName ? -1 : o.index).ToArray();
        }

        private static void FillSpawner(Dictionary<VFXContext, SpawnInfo> outContextSpawnToSpawnInfo,
            Dictionary<VFXData, uint> outDataToSystemIndex,
            List<VFXCPUBufferDesc> outCpuBufferDescs,
            List<VFXEditorSystemDesc> outSystemDescs,
            IEnumerable<VFXContext> contexts,
            VFXExpressionGraph graph,
            VFXCompiledData compiledData,
            ref SubgraphInfos subgraphInfos,
            VFXGraph vfxGraph = null)
        {
            var systemNames = vfxGraph != null ? vfxGraph.systemNames : null;
            var spawners = CollectSpawnersHierarchy(contexts, ref subgraphInfos);
            foreach (var it in spawners.Select((spawner, index) => new { spawner, index }))
            {
                outContextSpawnToSpawnInfo.Add(it.spawner, new SpawnInfo() { bufferIndex = outCpuBufferDescs.Count, systemIndex = it.index });
                outCpuBufferDescs.Add(new VFXCPUBufferDesc()
                {
                    capacity = 1u,
                    stride = graph.GlobalEventAttributes.First().offset.structure,
                    layout = graph.GlobalEventAttributes.ToArray(),
                    initialData = ComputeArrayOfStructureInitialData(graph.GlobalEventAttributes, vfxGraph)
                });
            }
            foreach (var spawnContext in spawners)
            {
                var buffers = new List<VFXMapping>();
                buffers.Add(new VFXMapping()
                {
                    index = outContextSpawnToSpawnInfo[spawnContext].bufferIndex,
                    name = "spawner_output"
                });

                for (int indexSlot = 0; indexSlot < 2 && indexSlot < spawnContext.inputFlowSlot.Length; ++indexSlot)
                {
                    foreach (var input in subgraphInfos.contextEffectiveInputLinks[spawnContext][indexSlot])
                    {
                        var inputContext = input.context;
                        if (outContextSpawnToSpawnInfo.ContainsKey(inputContext))
                        {
                            buffers.Add(new VFXMapping()
                            {
                                index = outContextSpawnToSpawnInfo[inputContext].bufferIndex,
                                name = "spawner_input_" + (indexSlot == 0 ? "OnPlay" : "OnStop")
                            });
                        }
                    }
                }

                foreach (var task in compiledData.contextToCompiledData[spawnContext].tasks)
                {
                    var contextData = compiledData.taskToCompiledData[task];
                    var contextExpressions = contextData.cpuMapper.CollectExpression(-1);
                    var systemValueMappings = new List<VFXMapping>();
                    var expressionPerSpawnToProcess = new List<VFXExpression>();
                    foreach (var contextExpression in contextExpressions)
                    {
                        var expressionIndex = graph.GetFlattenedIndex(contextExpression.exp);
                        systemValueMappings.Add(new VFXMapping(contextExpression.name, expressionIndex));
                        if (contextExpression.exp.Is(VFXExpression.Flags.PerSpawn))
                        {
                            expressionPerSpawnToProcess.Add(contextExpression.exp);
                        }
                    }

                    if (expressionPerSpawnToProcess.Any())
                    {
                        var addiionnalValues = ComputePreProcessExpressionForSpawn(expressionPerSpawnToProcess, graph);
                        systemValueMappings.AddRange(addiionnalValues);
                    }

                    string nativeName = string.Empty;
                    if (systemNames != null)
                        nativeName = systemNames.GetUniqueSystemName(spawnContext.GetData());
                    else
                        throw new InvalidOperationException("system names manager cannot be null");

                    outDataToSystemIndex.Add(spawnContext.GetData(), (uint)outSystemDescs.Count);
                    compiledData.taskToCompiledData[task] = contextData;

                    outSystemDescs.Add(new VFXEditorSystemDesc()
                    {
                        values = systemValueMappings.ToArray(),
                        buffers = buffers.ToArray(),
                        capacity = 0u,
                        name = nativeName,
                        flags = VFXSystemFlag.SystemDefault,
                        layer = uint.MaxValue,
                        tasks = BuildEditorTaskDescFromBlockSpawner(spawnContext.activeFlattenedChildrenWithImplicit, contextData, graph)
                    });
                }
            }
        }

        struct SubgraphInfos
        {
            public Dictionary<VFXSubgraphContext, VFXSubgraphContext> subgraphParents;
            public Dictionary<VFXContext, VFXSubgraphContext> spawnerSubgraph;
            public List<VFXSubgraphContext> subgraphs;
            public Dictionary<VFXContext, List<VFXContextLink>[]> contextEffectiveInputLinks;

            public List<VFXContextLink> GetContextEffectiveOutputLinks(VFXContext context, int slot)
            {
                List<VFXContextLink> effectiveOuts = new List<VFXContextLink>();

                foreach (var kv in contextEffectiveInputLinks)
                {
                    for (int i = 0; i < kv.Value.Length; ++i)
                    {
                        foreach (var link in kv.Value[i])
                        {
                            if (link.context == context && link.slotIndex == slot)
                                effectiveOuts.Add(new VFXContextLink() { context = kv.Key, slotIndex = i });
                        }
                    }
                }
                return effectiveOuts;
            }
        }

        private static void FillEvent(List<EventDesc> outEventDesc, Dictionary<VFXContext, SpawnInfo> contextSpawnToSpawnInfo, IEnumerable<VFXContext> contexts, IEnumerable<VFXData> compilableData, ref SubgraphInfos subgraphInfos)
        {
            var contextEffectiveInputLinks = subgraphInfos.contextEffectiveInputLinks;

            var allPlayNotLinked = contextSpawnToSpawnInfo.Where(o => !contextEffectiveInputLinks[o.Key][0].Any()).Select(o => o.Key).ToList();
            var allStopNotLinked = contextSpawnToSpawnInfo.Where(o => !contextEffectiveInputLinks[o.Key][1].Any()).Select(o => o.Key).ToList();

            var eventDescTemp = new EventDesc[]
            {
                new EventDesc() { name = VisualEffectAsset.PlayEventName, startSystems = allPlayNotLinked, stopSystems = new List<VFXContext>(), initSystems = new List<VFXContext>() },
                new EventDesc() { name = VisualEffectAsset.StopEventName, startSystems = new List<VFXContext>(), stopSystems = allStopNotLinked, initSystems = new List<VFXContext>() },
            }.ToList();

            var specialNames = new HashSet<string>(new string[] { VisualEffectAsset.PlayEventName, VisualEffectAsset.StopEventName });

            var events = contexts.Where(o => o.contextType == VFXContextType.Event);
            foreach (var evt in events)
            {
                var eventName = (evt as VFXBasicEvent).eventName;

                if (subgraphInfos.spawnerSubgraph.ContainsKey(evt) && specialNames.Contains(eventName))
                    continue;

                List<VFXContextLink> effectiveOuts = subgraphInfos.GetContextEffectiveOutputLinks(evt, 0);

                foreach (var link in effectiveOuts)
                {
                    var eventIndex = eventDescTemp.FindIndex(o => o.name == eventName);
                    if (eventIndex == -1)
                    {
                        eventIndex = eventDescTemp.Count;
                        eventDescTemp.Add(new EventDesc
                        {
                            name = eventName,
                            startSystems = new List<VFXContext>(),
                            stopSystems = new List<VFXContext>(),
                            initSystems = new List<VFXContext>()
                        });
                    }

                    var eventDesc = eventDescTemp[eventIndex];
                    if (link.context.contextType == VFXContextType.Spawner)
                    {
                        if (contextSpawnToSpawnInfo.ContainsKey(link.context))
                        {
                            var startSystem = link.slotIndex == 0;
                            if (startSystem)
                            {
                                eventDesc.startSystems.Add(link.context);
                            }
                            else
                            {
                                eventDesc.stopSystems.Add(link.context);
                            }
                        }
                    }
                    else if (link.context.contextType == VFXContextType.Init)
                    {
                        eventDesc.initSystems.Add(link.context);
                    }
                    else
                    {
                        throw new InvalidOperationException(string.Format("Unexpected link context : " + link.context.contextType));
                    }
                }
            }

            outEventDesc.AddRange(eventDescTemp);
        }

        private void GenerateShaders(List<GeneratedCodeData> outGeneratedCodeData, VFXExpressionGraph graph, IEnumerable<VFXContext> contexts, VFXCompiledData compiledData, VFXCompilationMode compilationMode, HashSet<string> dependencies, bool enableShaderDebugSymbols, Dictionary<VFXContext, VFXExpressionMapper> gpuMappers)
        {
            Profiler.BeginSample("VFXEditor.GenerateShaders");
            try
            {
                var codeGeneratorCache = new VFXCodeGenerator.Cache();
                var errorMessage = new StringBuilder();
                foreach (var context in contexts)
                {
                    VFXExpressionMapper gpuMapper = null;
                    if (gpuMappers?.TryGetValue(context, out gpuMapper) != true)
                    {
                        gpuMapper = graph.BuildGPUMapper(context);
                    }
                    var uniformMapper = new VFXUniformMapper(gpuMapper, context.doesGenerateShader, false);

                    foreach (var task in compiledData.contextToCompiledData[context].tasks)
                    {
                        // Add gpu and uniform mapper
                        var contextData = compiledData.taskToCompiledData[task];
                        contextData.gpuMapper = gpuMapper;
                        contextData.uniformMapper = uniformMapper;
                        contextData.bufferTypeUsage = graph.GetBufferUsage(context);

                        if (task.doesGenerateShader)
                        {
                            var generatedContent = VFXCodeGenerator.Build(context, task, compilationMode, contextData, dependencies, enableShaderDebugSymbols, codeGeneratorCache, out var errors);
                            if (generatedContent != null && generatedContent.Length > 0)
                            {
                                contextData.indexInShaderSource = outGeneratedCodeData.Count;
                                outGeneratedCodeData.Add(new GeneratedCodeData()
                                {
                                    context = context,
                                    task = task,
                                    computeShader = task.shaderType == VFXTaskShaderType.ComputeShader,
                                    compilMode = compilationMode,
                                    content = generatedContent
                                });
                            }
                            else if (errors?.Count > 0)
                            {
                                errorMessage.AppendLine($"Code generation failure from context {context.name.Replace("\n", " ")} {(string.IsNullOrEmpty(context.label) ? $"({context.label})" : string.Empty)}");
                                errors.ForEach(x =>
                                {
                                    errorMessage.AppendLine($"\t{x}");
                                    m_Graph.RegisterCompileError("CompileError", x, context);
                                });
                            }
                        }

                        compiledData.taskToCompiledData[task] = contextData;
                    }
                }
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        private static VFXShaderSourceDesc[] SaveShaderFiles(VisualEffectResource resource,
            List<GeneratedCodeData> generatedCodeData,
            VFXCompiledData compiledData,
            VFXSystemNames systemNames)
        {
            Profiler.BeginSample("VFXEditor.SaveShaderFiles");
            try
            {
                var descs = new VFXShaderSourceDesc[generatedCodeData.Count];
                var assetName = string.Empty;
                if (resource.asset != null)
                {
                    assetName = resource.asset.name; //Most Common case, asset is already available
                }
                else
                {
                    var assetPath = AssetDatabase.GetAssetPath(resource); //Can occur during Copy/Past or Rename
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        assetName = Path.GetFileNameWithoutExtension(assetPath);
                    }
                    else if (resource.name != null) //Unable to retrieve asset path, last fallback use serialized resource name
                    {
                        assetName = resource.name;
                    }
                }

                for (int i = 0; i < generatedCodeData.Count; ++i)
                {
                    var generated = generatedCodeData[i];

                    var systemName = systemNames.GetUniqueSystemName(generated.context.GetData());
                    var contextLetter = generated.context.letter;
                    var contextName = string.IsNullOrEmpty(generated.context.label) ? generated.context.name: generated.context.label;
                    contextName = contextName.Replace('\n', ' ');
                    contextName = contextName.Replace('|', ' ');

                    var shaderName = string.Empty;
                    var fileName = string.Empty;
                    if (contextLetter == '\0')
                    {
                        fileName = string.Format("[{0}] [{1}] {2}", assetName, systemName, contextName);
                        shaderName = string.Format("Hidden/VFX/{0}/{1}/{2}", assetName, systemName, contextName);
                    }
                    else
                    {
                        fileName = string.Format("[{0}] [{1}]{2} {3}", assetName, systemName, contextLetter, contextName);
                        shaderName = string.Format("Hidden/VFX/{0}/{1}/{2}/{3}", assetName, systemName, contextLetter, contextName);
                    }

                    if (!string.IsNullOrEmpty(generated.task.name))
                    {
                        fileName += string.Format(" - {0}", generated.task.name);
                        shaderName += string.Format("/{0}", generated.task.name);
                    }

                    if (!generated.computeShader)
                    {
                        generated.content.Insert(0, "Shader \"" + shaderName + "\"\n");
                    }
                    descs[i].source = generated.content.ToString();
                    descs[i].name = fileName;
                    descs[i].compute = generated.computeShader;
                }

                return descs;
            }
            finally
            {
                Profiler.EndSample();
            }
        }

        public void FillDependentBuffer(
            IEnumerable<VFXData> compilableData,
            List<VFXGPUBufferDesc> bufferDescs,
            VFXDependentBuffersData buffers)
        {
            // TODO This should be in VFXDataParticle
            foreach (var data in compilableData.OfType<VFXDataParticle>())
            {
                int attributeBufferIndex = -1;
                if (data.attributeBufferSize > 0)
                {
                    attributeBufferIndex = bufferDescs.Count;
                    bufferDescs.Add(data.attributeBufferDesc);
                }
                buffers.attributeBuffers.Add(data, attributeBufferIndex);

                int stripBufferIndex = -1;
                if (data.hasStrip)
                {
                    stripBufferIndex = bufferDescs.Count;
                    uint stripCapacity = (uint)data.GetSettingValue("stripCapacity");
                    bufferDescs.Add(new VFXGPUBufferDesc() { debugName = "VFXStripDataBuffer", target = GraphicsBuffer.Target.Structured, size = stripCapacity * 5 + 1, stride = 4});
                }
                buffers.stripBuffers.Add(data, stripBufferIndex);

                int boundsBufferIndex = -1;
                if (data.NeedsComputeBounds())
                {
                    boundsBufferIndex = bufferDescs.Count;
                    bufferDescs.Add(new VFXGPUBufferDesc() { debugName = "VFXBoundsBuffer", target = GraphicsBuffer.Target.Structured, size = 6, stride = 4});
                }
                buffers.boundsBuffers.Add(data, boundsBufferIndex);
            }

            //Prepare GPU event buffer
            foreach (var data in compilableData.SelectMany(o => o.dependenciesOut).Distinct().OfType<VFXDataParticle>())
            {
                var eventBufferIndex = -1;
                uint capacity = (uint)data.GetSettingValue("capacity");
                if (capacity > 0)
                {
                    eventBufferIndex = bufferDescs.Count;
                    // event count (1) + total event count (1) + event prefix sum (1) + source index (capacity)
                    bufferDescs.Add(new VFXGPUBufferDesc() { debugName = "VFXGPUEventsBuffer", target = GraphicsBuffer.Target.Structured, size = 3 + capacity, stride = 4 });
                }
                buffers.eventBuffers.Add(data, eventBufferIndex);
            }
        }

        VFXRendererSettings GetRendererSettings(VFXRendererSettings initialSettings, IEnumerable<IVFXSubRenderer> subRenderers)
        {
            var settings = initialSettings;
            settings.shadowCastingMode = subRenderers.Any(r => r.hasShadowCasting) ? ShadowCastingMode.On : ShadowCastingMode.Off;
            settings.motionVectorGenerationMode = subRenderers.Any(r => r.hasMotionVector) ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;
            return settings;
        }

        private class VFXImplicitContextOfExposedExpression : VFXContext
        {
            private VFXExpressionMapper mapper;

            public VFXImplicitContextOfExposedExpression() : base(VFXContextType.None, VFXDataType.None, VFXDataType.None) { }

            private static void CollectExposedExpression(List<VFXExpression> expressions, VFXSlot slot)
            {
                var expression = slot.valueType != VFXValueType.None ? slot.GetInExpression() : null;
                if (expression != null)
                    expressions.Add(expression);
                else
                {
                    foreach (var child in slot.children)
                        CollectExposedExpression(expressions, child);
                }
            }

            public void FillExpression(VFXGraph graph)
            {
                var allExposedParameter = graph.children.OfType<VFXParameter>().Where(o => o.exposed);
                var expressionsList = new List<VFXExpression>();
                foreach (var parameter in allExposedParameter)
                    CollectExposedExpression(expressionsList, parameter.outputSlots[0]);

                mapper = new VFXExpressionMapper();
                for (int i = 0; i < expressionsList.Count; ++i)
                    mapper.AddExpression(expressionsList[i], "ImplicitExposedExpression", i);
            }

            public override VFXExpressionMapper GetExpressionMapper(VFXDeviceTarget target)
            {
                return target == VFXDeviceTarget.CPU ? mapper : null;
            }
        }

        void ComputeEffectiveInputLinks(ref SubgraphInfos subgraphInfos, IEnumerable<VFXContext> compilableContexts)
        {
            var contextEffectiveInputLinks = subgraphInfos.contextEffectiveInputLinks;
            foreach (var context in compilableContexts.Where(t => !(t is VFXSubgraphContext)))
            {
                contextEffectiveInputLinks[context] = ComputeContextEffectiveLinks(context, ref subgraphInfos);
                ComputeEffectiveInputLinks(ref subgraphInfos, contextEffectiveInputLinks[context].SelectMany(t => t).Select(t => t.context).Where(t => !contextEffectiveInputLinks.ContainsKey(t)));
            }
        }

        struct EventDesc
        {
            public string name;
            public List<VFXContext> startSystems;
            public List<VFXContext> stopSystems;
            public List<VFXContext> initSystems;
        }

        static IEnumerable<uint> ConvertDataToSystemIndex(IEnumerable<VFXContext> input, Dictionary<VFXData, uint> dataToSystemIndex)
        {
            foreach (var context in input)
                if (dataToSystemIndex.TryGetValue(context.GetData(), out var index))
                    yield return index;
        }

        private static IEnumerable<(VFXSlot slot, VFXData data)> ComputeEventListFromSlot(IEnumerable<VFXSlot> slots)
        {
            foreach (var slot in slots)
            {
                var context = ((VFXModel)slot.owner).GetFirstOfType<VFXContext>();
                if (context.CanBeCompiled())
                {
                    var count = context.outputContexts.Count();
                    if (count == 0)
                        throw new InvalidOperationException("Unexpected invalid GPU Event");

                    if (count > 1)
                        throw new InvalidOperationException("Unexpected multiple GPU Event");

                    var outputContext = context.outputContexts.First().GetData();
                    yield return (slot, outputContext);
                }
            }
        }

        public struct VFXCompileOutput
        {
            public bool success;

            public HashSet<string> sourceDependencies;

            public VFXExpressionSheet sheet;
            public VFXEditorSystemDesc[] systemDesc;
            public VFXEventDesc[] eventDesc;
            public VFXGPUBufferDesc[] gpuBufferDesc;
            public VFXCPUBufferDesc[] cpuBufferDesc;
            public VFXTemporaryGPUBufferDesc[] temporaryBufferDesc;
            public VFXShaderSourceDesc[] shaderSourceDesc;
            public VFXRendererSettings rendererSettings;
            public VFXInstancingDisabledReason instancingDisabledReason;

            public uint version;
        }

        public VFXCompileOutput Compile(VFXCompilationMode compilationMode, bool enableShaderDebugSymbols, VFXAnalytics analytics)
        {
            var output = new VFXCompileOutput()
            {
                sourceDependencies = new HashSet<string>()
            };

            // Early out in case: (Not even displaying the popup)
            if (VFXLibrary.currentSRPBinder == null)  // One of supported SRPs is not current SRP
            {
                output.success = false;
                return output;
            }

            //Graph is empty
            if (m_Graph.children.Count() == 0)
            {
                output.success = true;
                output.version = compiledVersion;
                output.systemDesc = Array.Empty<VFXEditorSystemDesc>();
                return output;
            }

            Profiler.BeginSample("VFXEditor.CompileAsset");
            float nbSteps = 12.0f;
            string assetPath = AssetDatabase.GetAssetPath(visualEffectResource);
            string progressBarTitle = "Compiling " + assetPath;
            try
            {
                EditorUtility.DisplayProgressBar(progressBarTitle, "Collecting dependencies", 0 / nbSteps);
                var models = new HashSet<ScriptableObject>();
                m_Graph.CollectDependencies(models, false);

                var resource = m_Graph.GetResource();
                resource.ClearSourceDependencies();

                foreach (VFXModel model in models.Where(t => t is IVFXSlotContainer))
                {
                    model.GetSourceDependentAssets(output.sourceDependencies);
                }

                var contexts = models.OfType<VFXContext>().ToArray();

                foreach (var c in contexts) // Unflag all contexts
                    c.MarkAsCompiled(false);


                IEnumerable<VFXContext> compilableContexts = contexts.Where(c => c.CanBeCompiled()).ToArray();
                var compilableData = models.OfType<VFXData>().Where(d => d.CanBeCompiled());

                IEnumerable<VFXContext> implicitContexts = Enumerable.Empty<VFXContext>();
                foreach (var d in compilableData) // Flag compiled contexts
                    implicitContexts = implicitContexts.Concat(d.InitImplicitContexts());
                compilableContexts = compilableContexts.Concat(implicitContexts.ToArray());

                foreach (var c in compilableContexts) // Flag compiled contexts
                    c.MarkAsCompiled(true);

                EditorUtility.DisplayProgressBar(progressBarTitle, "Collecting attributes", 1 / nbSteps);
                foreach (var data in compilableData)
                    data.CollectAttributes();

                EditorUtility.DisplayProgressBar(progressBarTitle, "Process dependencies", 2 / nbSteps);
                foreach (var data in compilableData)
                    data.ProcessDependencies();

                // Sort the systems by layer so they get updated in the right order. It has to be done after processing the dependencies
                compilableData = compilableData.OrderBy(d => d.layer);

                EditorUtility.DisplayProgressBar(progressBarTitle, "Compiling expression Graph", 3 / nbSteps);
                m_ExpressionGraph = new VFXExpressionGraph();
                var exposedExpressionContext = ScriptableObject.CreateInstance<VFXImplicitContextOfExposedExpression>();
                exposedExpressionContext.FillExpression(m_Graph); //Force all exposed expression to be visible, only for registering in CompileExpressions

                var expressionContextOptions = compilationMode == VFXCompilationMode.Runtime ? VFXExpressionContextOption.ConstantFolding : VFXExpressionContextOption.Reduction;
                m_ExpressionGraph.CompileExpressions(compilableContexts.Concat(new VFXContext[] { exposedExpressionContext }), expressionContextOptions);

                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating bytecode", 4 / nbSteps);
                var expressionDescs = new List<VFXExpressionDesc>();
                var expressionPerSpawnEventAttributesDescs = new List<VFXExpressionDesc>();
                var valueDescs = new List<VFXExpressionValueContainerDesc>();
                FillExpressionDescs(m_ExpressionGraph, expressionDescs, expressionPerSpawnEventAttributesDescs, valueDescs);


                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating mappings", 5 / nbSteps);

                var compiledData = new VFXCompiledData { contextToCompiledData = new(), taskToCompiledData = new() };

                // Initialize contexts and tasks
                foreach (var context in compilableContexts)
                {
                    var contextCompiledData = context.PrepareCompiledData();
                    var cpuMapper = m_ExpressionGraph.BuildCPUMapper(context);
                    var instancingSplitValues = context.CreateInstancingSplitValues(m_ExpressionGraph);

                    foreach (var task in contextCompiledData.tasks)
                    {
                        var contextData = new VFXTaskCompiledData() { indexInShaderSource = -1 };
                        contextData.hlslCodeHolders = m_ExpressionGraph.GetCustomHLSLExpressions(context);
                        contextData.cpuMapper = cpuMapper;
                        contextData.parameters = context.additionalMappings.ToArray();
                        contextData.linkedEventOut = ComputeEventListFromSlot(context.allLinkedOutputSlot).ToArray();
                        contextData.instancingSplitValues = instancingSplitValues;

                        compiledData.taskToCompiledData[task] = contextData;
                    }

                    compiledData.contextToCompiledData[context] = contextCompiledData;
                }


                var exposedParameterDescs = new List<(VFXMapping mapping, VFXSpace space, SpaceableType spaceType)>();
                FillExposedDescs(exposedParameterDescs, m_ExpressionGraph, m_Graph.children.OfType<VFXParameter>());
                SubgraphInfos subgraphInfos;
                subgraphInfos.subgraphParents = new Dictionary<VFXSubgraphContext, VFXSubgraphContext>();

                subgraphInfos.subgraphs = new List<VFXSubgraphContext>();

                foreach (var subgraph in m_Graph.children.OfType<VFXSubgraphContext>().Where(t => t.subgraph != null))
                {
                    subgraphInfos.subgraphs.Add(subgraph);
                    RecursePutSubgraphParent(subgraphInfos.subgraphParents, subgraphInfos.subgraphs, subgraph);
                }

                subgraphInfos.spawnerSubgraph = new Dictionary<VFXContext, VFXSubgraphContext>();

                foreach (var subgraph in subgraphInfos.subgraphs)
                {
                    foreach (var spawner in subgraph.subChildren.OfType<VFXContext>())
                        subgraphInfos.spawnerSubgraph.Add(spawner, subgraph);
                }

                subgraphInfos.contextEffectiveInputLinks = new Dictionary<VFXContext, List<VFXContextLink>[]>();

                ComputeEffectiveInputLinks(ref subgraphInfos, compilableContexts.Where(o => o.contextType == VFXContextType.Init || o.contextType == VFXContextType.OutputEvent));

                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating Attribute layouts", 6 / nbSteps);
                foreach (var data in compilableData)
                    data.GenerateAttributeLayout(subgraphInfos.contextEffectiveInputLinks);

                var generatedCodeData = new List<GeneratedCodeData>();

                var gpuMappers = new Dictionary<VFXContext, VFXExpressionMapper>();
                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating Graph Values layouts", 7 / nbSteps);
                {
                    foreach (var data in compilableData)
                        if (data is VFXDataParticle particleData)
                            particleData.GenerateSystemUniformMapper(m_ExpressionGraph, compiledData, ref gpuMappers);
                }
                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating shaders", 8 / nbSteps);
                GenerateShaders(generatedCodeData, m_ExpressionGraph, compilableContexts, compiledData, compilationMode, output.sourceDependencies, enableShaderDebugSymbols, gpuMappers);

                m_Graph.systemNames.Sync(m_Graph);
                EditorUtility.DisplayProgressBar(progressBarTitle, "Saving shaders", 9 / nbSteps);
                VFXShaderSourceDesc[] shaderSources = SaveShaderFiles(m_Graph.visualEffectResource, generatedCodeData, compiledData, m_Graph.systemNames);

                var bufferDescs = new List<VFXGPUBufferDesc>();
                var temporaryBufferDescs = new List<VFXTemporaryGPUBufferDesc>();
                var cpuBufferDescs = new List<VFXCPUBufferDesc>();
                var systemDescs = new List<VFXEditorSystemDesc>();

                EditorUtility.DisplayProgressBar(progressBarTitle, "Generating systems", 10 / nbSteps);
                cpuBufferDescs.Add(new VFXCPUBufferDesc()
                {
                    //Global attribute descriptor, always first entry in cpuBufferDesc, it can be empty (stride == 0).
                    capacity = 1u,
                    layout = m_ExpressionGraph.GlobalEventAttributes.ToArray(),
                    stride = m_ExpressionGraph.GlobalEventAttributes.Any() ? m_ExpressionGraph.GlobalEventAttributes.First().offset.structure : 0u,
                    initialData = ComputeArrayOfStructureInitialData(m_ExpressionGraph.GlobalEventAttributes, m_Graph)
                });

                var contextSpawnToSpawnInfo = new Dictionary<VFXContext, SpawnInfo>();
                var dataToSystemIndex = new Dictionary<VFXData, uint>();
                FillSpawner(contextSpawnToSpawnInfo, dataToSystemIndex, cpuBufferDescs, systemDescs, compilableContexts, m_ExpressionGraph, compiledData, ref subgraphInfos, m_Graph);

                var eventDescs = new List<EventDesc>();

                FillEvent(eventDescs, contextSpawnToSpawnInfo, compilableContexts, compilableData, ref subgraphInfos);
                var dependentBuffersData = new VFXDependentBuffersData();
                FillDependentBuffer(compilableData, bufferDescs, dependentBuffersData);

                var contextSpawnToBufferIndex = contextSpawnToSpawnInfo.Select(o => new { o.Key, o.Value.bufferIndex }).ToDictionary(o => o.Key, o => o.bufferIndex);
                foreach (var data in compilableData)
                {
                    if (data.type != VFXDataType.SpawnEvent)
                    {
                        //^ dataToSystemIndex have already been filled by FillSpawner
                        //TODO: Rework this approach and always use FillDescs after an appropriate ordering of compilableData
                        dataToSystemIndex.Add(data, (uint)systemDescs.Count);
                    }

                    data.FillDescs(m_Graph.errorManager.compileReporter,
                        compilationMode,
                        bufferDescs,
                        temporaryBufferDescs,
                        systemDescs,
                        m_ExpressionGraph,
                        compiledData,
                        compilableContexts,
                        contextSpawnToBufferIndex,
                        dependentBuffersData,
                        subgraphInfos.contextEffectiveInputLinks,
                        dataToSystemIndex,
                        m_Graph.systemNames);
                }

                // Early check : OutputEvent should not be duplicated with same name
                var outputEventNames = systemDescs.Where(o => o.type == VFXSystemType.OutputEvent).Select(o => o.name);
                if (outputEventNames.Count() != outputEventNames.Distinct().Count())
                {
                    throw new InvalidOperationException("There are duplicated entries in OutputEvent");
                }

                // Update transient renderer settings
                ShadowCastingMode shadowCastingMode = compilableContexts.OfType<IVFXSubRenderer>().Any(r => r.hasShadowCasting) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                MotionVectorGenerationMode motionVectorGenerationMode = compilableContexts.OfType<IVFXSubRenderer>().Any(r => r.hasMotionVector) ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;

                EditorUtility.DisplayProgressBar(progressBarTitle, "Setting up systems", 11 / nbSteps);
                var expressionSheet = new VFXExpressionSheet();
                expressionSheet.expressions = expressionDescs.ToArray();
                expressionSheet.expressionsPerSpawnEventAttribute = expressionPerSpawnEventAttributesDescs.ToArray();
                expressionSheet.values = valueDescs.OrderBy(o => o.expressionIndex).ToArray();

                var sortedExposedProperties = exposedParameterDescs.OrderBy(o => o.mapping.name);
                expressionSheet.exposed = sortedExposedProperties.Select(o => new VFXExposedMapping() { mapping = o.mapping, space = (VFXSpace)o.space }).ToArray();

                var vfxEventDesc = eventDescs.Select(e =>
                {
                    return new VFXEventDesc()
                    {
                        name = e.name,
                        initSystems = ConvertDataToSystemIndex(e.initSystems, dataToSystemIndex).ToArray(),
                        startSystems = ConvertDataToSystemIndex(e.startSystems, dataToSystemIndex).ToArray(),
                        stopSystems = ConvertDataToSystemIndex(e.stopSystems, dataToSystemIndex).ToArray()
                    };
                }).Where(e =>
                    {
                        return e.initSystems.Length > 0 || e.startSystems.Length > 0 || e.stopSystems.Length > 0;
                    }).ToArray();

                VFXInstancingDisabledReason instancingDisabledReason = ValidateInstancing(compilableContexts);

                output.success = true;

                output.sheet = expressionSheet;
                output.systemDesc = systemDescs.ToArray();
                output.eventDesc = vfxEventDesc;
                output.gpuBufferDesc = bufferDescs.ToArray();
                output.cpuBufferDesc = cpuBufferDescs.ToArray();
                output.temporaryBufferDesc = temporaryBufferDescs.ToArray();
                output.shaderSourceDesc = shaderSources;
                output.rendererSettings = new() { shadowCastingMode = shadowCastingMode, motionVectorGenerationMode = motionVectorGenerationMode };
                output.instancingDisabledReason = instancingDisabledReason;
                output.version = compiledVersion;

                m_ExpressionValues = expressionSheet.values;
            }
            catch (Exception e)
            {
                Debug.LogError($"Unity cannot compile the VisualEffectAsset at path \"{assetPath}\" because of the following exception:\n{e}");
                analytics?.OnCompilationError(e);
                output.success = false;
                return output;
            }
            finally
            {
                Profiler.EndSample();
                EditorUtility.ClearProgressBar();
            }

            m_Graph.onRuntimeDataChanged?.Invoke(m_Graph);
            return output;
        }

        public void UpdateValues()
        {
            if (m_ExpressionGraph == null)
                return;

            var flatGraph = m_ExpressionGraph.FlattenedExpressions;
            var numFlattenedExpressions = flatGraph.Count;

            int descIndex = 0;
            for (int i = 0; i < numFlattenedExpressions; ++i)
            {
                var exp = flatGraph[i];
                if (exp.Is(VFXExpression.Flags.Value))
                {
                    var desc = m_ExpressionValues[descIndex++];
                    if (desc.expressionIndex != i)
                        throw new InvalidOperationException();

                    switch (exp.valueType)
                    {
                        case VFXValueType.Float: SetValueDesc<float>(desc, exp); break;
                        case VFXValueType.Float2: SetValueDesc<Vector2>(desc, exp); break;
                        case VFXValueType.Float3: SetValueDesc<Vector3>(desc, exp); break;
                        case VFXValueType.Float4: SetValueDesc<Vector4>(desc, exp); break;
                        case VFXValueType.Int32: SetValueDesc<int>(desc, exp); break;
                        case VFXValueType.Uint32: SetValueDesc<uint>(desc, exp); break;
                        case VFXValueType.Texture2D:
                        case VFXValueType.Texture2DArray:
                        case VFXValueType.Texture3D:
                        case VFXValueType.TextureCube:
                        case VFXValueType.TextureCubeArray:
                            SetObjectValueDesc<Texture>(desc, exp);
                            break;
                        case VFXValueType.CameraBuffer: SetObjectValueDesc<Texture>(desc, exp); break;
                        case VFXValueType.Matrix4x4: SetValueDesc<Matrix4x4>(desc, exp); break;
                        case VFXValueType.Curve: SetValueDesc<AnimationCurve>(desc, exp); break;
                        case VFXValueType.ColorGradient: SetValueDesc<Gradient>(desc, exp); break;
                        case VFXValueType.Mesh: SetObjectValueDesc<Mesh>(desc, exp); break;
                        case VFXValueType.SkinnedMeshRenderer: SetObjectValueDesc<SkinnedMeshRenderer>(desc, exp); break;
                        case VFXValueType.Boolean: SetValueDesc<bool>(desc, exp); break;
                        case VFXValueType.Buffer: break; //The GraphicsBuffer type isn't serialized
                        default: throw new InvalidOperationException("Invalid type");
                    }
                }
            }

            VisualEffectAssetUtility.SetValueSheet(m_Graph.visualEffectResource.asset, m_ExpressionValues);
        }

        public VFXInstancingDisabledReason ValidateInstancing(IEnumerable<VFXContext> compilableContexts)
        {
            VFXInstancingDisabledReason reason = VFXInstancingDisabledReason.None;

            foreach (VFXContext model in compilableContexts)
            {
                if (model is VFXOutputEvent)
                {
                    reason |= VFXInstancingDisabledReason.OutputEvent;
                }

                if (model is VFXStaticMeshOutput)
                {
                    reason |= VFXInstancingDisabledReason.MeshOutput;
                }
            }

            return reason;
        }

        public VisualEffectResource visualEffectResource
        {
            get
            {
                if (m_Graph != null)
                {
                    return m_Graph.visualEffectResource;
                }
                return null;
            }
        }

        private VFXGraph m_Graph;

        [NonSerialized]
        private VFXExpressionGraph m_ExpressionGraph;
        [NonSerialized]
        private VFXExpressionValueContainerDesc[] m_ExpressionValues;
    }
}
