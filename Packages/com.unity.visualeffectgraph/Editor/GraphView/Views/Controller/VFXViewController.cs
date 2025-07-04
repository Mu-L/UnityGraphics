#define NOTIFICATION_VALIDATION
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Profiling;

using UnityObject = UnityEngine.Object;

namespace UnityEditor.VFX.UI
{
    internal partial class VFXViewController : Controller<VisualEffectResource>
    {
        private int m_UseCount;
        public int useCount
        {
            get { return m_UseCount; }
            set
            {
                m_UseCount = value;
                if (m_UseCount == 0)
                {
                    RemoveController(this);
                }
            }
        }

        public enum Priorities
        {
            Graph,
            Node,
            Slot,
            Default,
            GroupNode,
            Count
        }

        string m_Name;

        public string name
        {
            get { return m_Name; }
        }

        string ComputeName()
        {
            if (model == null)
                return "";
            string assetPath = AssetDatabase.GetAssetPath(model);
            if (!string.IsNullOrEmpty(assetPath))
            {
                return Path.GetFileNameWithoutExtension(assetPath);
            }
            else
            {
                return model.name;
            }
        }

        static Dictionary<ScriptableObject, bool>[] NewPrioritizedHashSet()
        {
            Dictionary<ScriptableObject, bool>[] result = new Dictionary<ScriptableObject, bool>[(int)Priorities.Count];

            for (int i = 0; i < (int)Priorities.Count; ++i)
            {
                result[i] = new Dictionary<ScriptableObject, bool>();
            }

            return result;
        }

        Priorities GetPriority(VFXObject obj)
        {
            if (obj is IVFXSlotContainer)
            {
                return Priorities.Node;
            }
            if (obj is VFXSlot)
            {
                return Priorities.Slot;
            }
            if (obj is VFXUI)
            {
                return Priorities.GroupNode;
            }
            if (obj is VFXGraph)
            {
                return Priorities.Graph;
            }
            return Priorities.Default;
        }

        Dictionary<ScriptableObject, bool>[] modifiedModels = NewPrioritizedHashSet();
        Dictionary<ScriptableObject, bool>[] otherModifiedModels = NewPrioritizedHashSet();

        private void OnObjectModified(VFXObject obj, bool uiChange)
        {
            // uiChange == false is stronger : if we have a uiChange and there was a nonUIChange before we keep the non uichange.
            if (!uiChange)
            {
                modifiedModels[(int)GetPriority(obj)][obj] = false;
            }
            else
            {
                if (!modifiedModels[(int)GetPriority(obj)].ContainsKey(obj))
                    modifiedModels[(int)GetPriority(obj)][obj] = true;
            }
        }

        Dictionary<ScriptableObject, List<Action>> m_Notified = new Dictionary<ScriptableObject, List<Action>>();


        public void RegisterNotification(VFXObject target, Action action)
        {
            if (target == null)
                return;

            target.onModified += OnObjectModified;

            List<Action> notifieds;
            if (m_Notified.TryGetValue(target, out notifieds))
            {
#if NOTIFICATION_VALIDATION
                if (notifieds.Contains(action))
                    Debug.LogError("Adding the same notification twice on:" + target.name);
#endif
                notifieds.Add(action);
            }
            else
            {
                notifieds = new List<Action>();
                notifieds.Add(action);

                m_Notified.Add(target, notifieds);
            }
        }

        public void UnRegisterNotification(VFXObject target, Action action)
        {
            if (object.ReferenceEquals(target, null))
                return;

            target.onModified -= OnObjectModified;
            List<Action> notifieds;
            if (m_Notified.TryGetValue(target, out notifieds))
            {
#if NOTIFICATION_VALIDATION
                if (!notifieds.Contains(action))
                    Debug.LogError("Removing a non existent notification" + target.name);
#endif
                notifieds.Remove(action);

                if (m_CurrentlyNotified == target)
                {
                    m_CurrentActions.Remove(action);
                }
            }
        }

        bool m_InNotify = false;

        ScriptableObject m_CurrentlyNotified; //this and the next list are used when in case a notification removes a following modification
        List<Action> m_CurrentActions = new List<Action>();


        public bool errorRefresh { get; set; } = true;

        public void NotifyUpdate()
        {
            m_InNotify = true;
            Profiler.BeginSample("VFXViewController.NotifyUpdate");
            if (model == null || m_Graph == null || m_Graph != model.graph)
            {
                // In this case the asset has been destroyed or reimported after having changed outside.
                // Lets rebuild everything and clear the undo stack.
                Clear();
                if (model != null && model.graph != null)
                    InitializeUndoStack();
                ModelChanged(model);
            }

            var tmp = modifiedModels;
            modifiedModels = otherModifiedModels;
            otherModifiedModels = tmp;


            int cpt = 0;
            foreach (var objs in otherModifiedModels)
            {
                foreach (var kv in objs)
                {
                    var obj = kv.Key;
                    List<Action> notifieds;
                    Profiler.BeginSample("VFXViewController.Notify:" + obj.GetType().Name);
                    if (m_Notified.TryGetValue(obj, out notifieds))
                    {
                        m_CurrentlyNotified = obj;
                        m_CurrentActions.Clear();
                        m_CurrentActions.AddRange(notifieds);
                        m_CurrentActions.Reverse();
                        while (m_CurrentActions.Count > 0)
                        {
                            var action = m_CurrentActions[m_CurrentActions.Count - 1];
                            try
                            {
                                action();
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e);
                            }
                            cpt++;
                            m_CurrentActions.RemoveAt(m_CurrentActions.Count - 1);
                        }
                    }
                    Profiler.EndSample();
                    if (!kv.Value && obj is VFXModel vfxModel && errorRefresh) // we refresh errors only if it wasn't a ui change
                    {
                        vfxModel.RefreshErrors();
                    }
                }
                m_CurrentlyNotified = null;

                objs.Clear();
            }
            /*
            if (cpt > 0)
                Debug.LogWarningFormat("{0} notification sent this frame", cpt);*/
            Profiler.EndSample();

            m_InNotify = false;

            string newName = ComputeName();
            if (newName != m_Name)
            {
                m_Name = newName;

                if (model != null && model.name != m_Name)
                {
                    bool prevDirty = EditorUtility.IsDirty(model);
                    model.name = m_Name;
                    if (!prevDirty)
                        EditorUtility.ClearDirty(model);
                }
                if (graph != null && (graph as UnityObject).name != m_Name)
                {
                    bool prevDirty = EditorUtility.IsDirty(graph);
                    (graph as UnityObject).name = m_Name;
                    if (!prevDirty)
                        EditorUtility.ClearDirty(graph);
                }

                NotifyChange(Change.assetName);
            }

            if (m_DataEdgesMightHaveChangedAsked)
            {
                m_DataEdgesMightHaveChangedAsked = false;
                DataEdgesMightHaveChanged();
            }
        }

        public VFXGraph graph { get { return model != null ? model.graph as VFXGraph : null; } }

        readonly List<VFXFlowAnchorController> m_FlowAnchorController = new List<VFXFlowAnchorController>();

        // Model / Controller synchronization
        readonly Dictionary<VFXModel, List<VFXNodeController>> m_SyncedModels = new Dictionary<VFXModel, List<VFXNodeController>>();

        readonly List<VFXDataEdgeController> m_DataEdges = new List<VFXDataEdgeController>();
        readonly List<VFXFlowEdgeController> m_FlowEdges = new List<VFXFlowEdgeController>();

        public override IEnumerable<Controller> allChildren
        {
            get
            {
                return m_SyncedModels.Values.SelectMany(t => t)
                    .Cast<Controller>()
                    .Concat(m_DataEdges)
                    .Concat(m_FlowEdges)
                    .Concat(m_ParameterControllers.Values)
                    .Concat(m_GroupNodeControllers)
                    .Concat(m_StickyNoteControllers);
        }
        }

        public void LightApplyChanges()
        {
            ModelChanged(model);
            GraphChanged();
        }

        public override void ApplyChanges()
        {
            ModelChanged(model);
            GraphChanged();
            foreach (var controller in allChildren)
            {
                controller.ApplyChanges();
            }
        }

        void GraphLost()
        {
            Clear();
            if (!object.ReferenceEquals(m_Graph, null))
            {
                RemoveInvalidateDelegate(m_Graph, InvalidateExpressionGraph);
                RemoveInvalidateDelegate(m_Graph, IncremenentGraphUndoRedoState);
                RemoveInvalidateDelegate(m_Graph, ReinitIfNeeded);

                UnRegisterNotification(m_Graph, GraphChanged);

                m_Graph = null;
            }
            if (!object.ReferenceEquals(m_UI, null))
            {
                UnRegisterNotification(m_UI, UIChanged);
                m_UI = null;
            }
        }

        public override void OnDisable()
        {
            Profiler.BeginSample("VFXViewController.OnDisable");
            GraphLost();
            ReleaseUndoStack();
            Undo.undoRedoPerformed -= SynchronizeUndoRedoState;
            Undo.willFlushUndoRecord -= WillFlushUndoRecord;

            base.OnDisable();
            Profiler.EndSample();
        }

        public IEnumerable<VFXNodeController> AllSlotContainerControllers
        {
            get
            {
                var operatorControllers = m_SyncedModels.Values.SelectMany(t => t).OfType<VFXNodeController>();
                var blockControllers = (contexts.SelectMany(t => t.blockControllers)).Cast<VFXNodeController>();

                return operatorControllers.Concat(blockControllers);
            }
        }

        private bool RecreateNodeEdges()
        {
            var unusedEdges = new HashSet<VFXDataEdgeController>(m_DataEdges);
            var nodeToUpdate = new HashSet<VFXNodeController>();

            foreach (var operatorControllers in m_SyncedModels.Values)
            {
                foreach (var nodeController in operatorControllers)
                {
                    foreach (var input in nodeController.inputPorts)
                    {
                        if (RecreateInputSlotEdge(ref unusedEdges, nodeController, input))
                        {
                            nodeToUpdate.Add(nodeController);
                        }
                    }
                    if (nodeController is VFXContextController contextController)
                    {
                        foreach (var block in contextController.blockControllers)
                        {
                            foreach (var input in block.inputPorts)
                            {
                                if (RecreateInputSlotEdge(ref unusedEdges, block, input))
                                {
                                    nodeToUpdate.Add(block);
                                }
                            }
                        }
                    }
                }
            }

            foreach (var edge in unusedEdges)
            {
                nodeToUpdate.Add(edge.input.sourceNode);
                edge.OnDisable();

                m_DataEdges.Remove(edge);
            }

            foreach (var node in nodeToUpdate)
            {
                node.UpdateAllEditable();
                // Mark model as changed since it's connections have changed
                OnObjectModified(node.model, false);
            }

            return nodeToUpdate.Any();
        }

        bool m_DataEdgesMightHaveChangedAsked;

        public void DataEdgesMightHaveChanged()
        {
            if (m_Syncing) return;

            if (m_InNotify)
            {
                m_DataEdgesMightHaveChangedAsked = true;
                return;
            }

            Profiler.BeginSample("VFXViewController.DataEdgesMightHaveChanged");
            bool change = RecreateNodeEdges();

            if (change || m_ForceDataEdgeNotification)
            {
                m_ForceDataEdgeNotification = false;
                NotifyChange(Change.dataEdge);
            }
            Profiler.EndSample();
        }

        public bool RecreateInputSlotEdge(ref HashSet<VFXDataEdgeController> unusedEdges, VFXNodeController slotContainer, VFXDataAnchorController input)
        {
            VFXSlot inputSlot = input.model;
            if (inputSlot == null)
                return false;

            bool changed = false;
            if (input.HasLink())
            {
                VFXNodeController operatorControllerFrom = null;

                IVFXSlotContainer targetSlotContainer = inputSlot.refSlot.owner;
                if (targetSlotContainer == null)
                {
                    return false;
                }

                switch (targetSlotContainer)
                {
                    case VFXParameter vfxParameter:
                        if (m_ParameterControllers.TryGetValue(vfxParameter, out var controller))
                        {
                            operatorControllerFrom = controller.GetParameterForLink(inputSlot);
                        }
                        break;
                    case VFXBlock vfxBlock:
                        var context = vfxBlock.GetParent();
                        if (m_SyncedModels.TryGetValue(context, out var contextControllers) && contextControllers.Any())
                        {
                            operatorControllerFrom = ((VFXContextController)contextControllers[0]).blockControllers.FirstOrDefault(t => t.model == vfxBlock);
                        }
                        break;
                    case VFXModel vfxModel:
                        if (m_SyncedModels.TryGetValue(vfxModel, out var nodeControllers) && nodeControllers.Count > 0)
                        {
                            operatorControllerFrom = nodeControllers[0];
                        }
                        break;
                    default:
                        throw new Exception($"`targetSlotContainer` has unexpected type {targetSlotContainer.GetType().Name}");
                }

                if (operatorControllerFrom != null && slotContainer != null)
                {
                    var anchorFrom = operatorControllerFrom.outputPorts.FirstOrDefault(o => o.model == inputSlot.refSlot);

                    var edgController = m_DataEdges.FirstOrDefault(t => t.input == input && t.output == anchorFrom);

                    if (edgController != null)
                    {
                        unusedEdges.Remove(edgController);
                    }
                    else if (anchorFrom != null)
                    {
                        edgController = new VFXDataEdgeController(input, anchorFrom);
                        m_DataEdges.Add(edgController);
                        changed = true;
                    }
                }
            }

            foreach (VFXSlot subSlot in inputSlot.children)
            {
                VFXDataAnchorController subAnchor = slotContainer.inputPorts.FirstOrDefault(t => t.model == subSlot);
                // Can be null for example for hidden values from Vector3Spaceables
                changed |= subAnchor != null && RecreateInputSlotEdge(ref unusedEdges, slotContainer, subAnchor);
            }

            return changed;
        }

        public IEnumerable<VFXContextController> contexts
        {
            get { return m_SyncedModels.Values.SelectMany(t => t).OfType<VFXContextController>(); }
        }
        public IEnumerable<VFXNodeController> nodes
        {
            get { return m_SyncedModels.Values.SelectMany(t => t); }
        }

        public void FlowEdgesMightHaveChanged()
        {
            if (m_Syncing) return;

            bool change = RecreateFlowEdges();
            if (change)
            {
                UpdateSystems(); // System will change based on flowEdges
                NotifyChange(Change.flowEdge);
            }
        }

        public class Change
        {
            public const int flowEdge = 1;
            public const int dataEdge = 2;

            public const int groupNode = 3;

            public const int assetName = 4;

            public const int ui = 5;

            public const int destroy = 666;
        }

        bool RecreateFlowEdges()
        {
            bool changed = false;
            HashSet<VFXFlowEdgeController> unusedEdges = new HashSet<VFXFlowEdgeController>();
            foreach (var e in m_FlowEdges)
            {
                unusedEdges.Add(e);
            }

            var contextControllers = contexts;
            foreach (var outController in contextControllers.ToArray())
            {
                var output = outController.model;
                for (int slotIndex = 0; slotIndex < output.inputFlowSlot.Length; ++slotIndex)
                {
                    var inputFlowSlot = output.inputFlowSlot[slotIndex];
                    foreach (var link in inputFlowSlot.link)
                    {
                        var inController = contexts.FirstOrDefault(x => x.model == link.context);
                        if (inController == null)
                            break;

                        var outputAnchor = inController.flowOutputAnchors.Where(o => o.slotIndex == link.slotIndex).FirstOrDefault();
                        var inputAnchor = outController.flowInputAnchors.Where(o => o.slotIndex == slotIndex).FirstOrDefault();

                        var edgeController = m_FlowEdges.FirstOrDefault(t => t.input == inputAnchor && t.output == outputAnchor);
                        if (edgeController != null)
                            unusedEdges.Remove(edgeController);
                        else
                        {
                            edgeController = new VFXFlowEdgeController(inputAnchor, outputAnchor);
                            m_FlowEdges.Add(edgeController);
                            changed = true;
                        }
                    }
                }
            }

            foreach (var edge in unusedEdges)
            {
                edge.OnDisable();
                m_FlowEdges.Remove(edge);
                changed = true;
            }

            return changed;
        }

        public ReadOnlyCollection<VFXDataEdgeController> dataEdges
        {
            get { return m_DataEdges.AsReadOnly(); }
        }
        public ReadOnlyCollection<VFXFlowEdgeController> flowEdges
        {
            get { return m_FlowEdges.AsReadOnly(); }
        }

        public bool CreateLink(VFXDataAnchorController input, VFXDataAnchorController output, bool revertTypeConstraint = false)
        {
            if (input == null)
            {
                return false;
            }

            if (input.sourceNode.viewController != output.sourceNode.viewController)
            {
                return false;
            }

            if (!input.CanLink(output))
            {
                return false;
            }

            VFXParameter.NodeLinkedSlot resulting = input.CreateLinkTo(output, revertTypeConstraint);

            if (resulting.inputSlot != null && resulting.outputSlot != null)
            {
                VFXParameterNodeController fromController = output.sourceNode as VFXParameterNodeController;
                if (fromController != null)
                {
                    foreach (var anyNode in fromController.parentController.nodes)
                    {
                        if (anyNode.infos.linkedSlots != null)
                            anyNode.infos.linkedSlots.RemoveAll(t => t.inputSlot == resulting.inputSlot && t.outputSlot == resulting.outputSlot);
                    }

                    if (fromController.infos.linkedSlots == null)
                        fromController.infos.linkedSlots = new List<VFXParameter.NodeLinkedSlot>();
                    fromController.infos.linkedSlots.Add(resulting);
                }

                VFXParameterNodeController toController = input.sourceNode as VFXParameterNodeController;
                if (toController != null)
                {
                    foreach (var anyNode in toController.parentController.nodes)
                    {
                        if (anyNode.infos.linkedSlots != null)
                            anyNode.infos.linkedSlots.RemoveAll(t => t.inputSlot == resulting.inputSlot && t.outputSlot == resulting.outputSlot);
                    }

                    var infos = toController.infos;
                    if (infos.linkedSlots == null)
                        infos.linkedSlots = new List<VFXParameter.NodeLinkedSlot>();
                    infos.linkedSlots.Add(resulting);
                }

                DataEdgesMightHaveChanged();
                return true;
            }
            return false;
        }

        public void AddElement(VFXDataEdgeController edge)
        {
            var fromAnchor = edge.output;
            var toAnchor = edge.input;

            CreateLink(toAnchor, fromAnchor);
            edge.OnDisable();
        }

        public void AddElement(VFXFlowEdgeController edge)
        {
            var flowEdge = (VFXFlowEdgeController)edge;

            var outputFlowAnchor = flowEdge.output as VFXFlowAnchorController;
            var inputFlowAnchor = flowEdge.input as VFXFlowAnchorController;

            var contextOutput = outputFlowAnchor.owner;
            var contextInput = inputFlowAnchor.owner;

            contextOutput.LinkTo(contextInput, outputFlowAnchor.slotIndex, inputFlowAnchor.slotIndex);

            edge.OnDisable();
        }

        public void Remove(IEnumerable<Controller> removedControllers, bool explicitDelete = false)
        {
            removedControllers = removedControllers.Except(removedControllers.OfType<VFXContextController>().Where(t => t.model is VFXBlockSubgraphContext)); //refuse to delete VFXBlockSubgraphContext

            var removedContexts = new HashSet<VFXContextController>(removedControllers.OfType<VFXContextController>());

            //remove all blocks that are in a removed context.
            var removed = removedControllers.Where(t => !(t is VFXBlockController) || !removedContexts.Contains((t as VFXBlockController).contextController)).Distinct().ToArray();

            foreach (var controller in removed)
            {
                RemoveElement(controller, explicitDelete);
            }
        }

        bool m_ForceDataEdgeNotification;

        public void RemoveElement(Controller element, bool explicitDelete = false)
        {
            bool HasCustomAttributes(VFXModel model)
            {
                return model is IVFXAttributeUsage attributeUsage &&
                       attributeUsage.usedAttributes.Any(x => graph.attributesManager.IsCustom(x.name));
            }

            VFXModel removedModel = null;
            bool needSyncCustomAttributes = false;

            if (element is VFXContextController contextController)
            {
                VFXContext context = contextController.model;
                removedModel = context;
                needSyncCustomAttributes = HasCustomAttributes(removedModel);
                contextController.NodeGoingToBeRemoved();

                // Remove connections from context
                foreach (var slot in context.inputSlots.Concat(context.outputSlots))
                    slot.UnlinkAll(true, true);

                // Remove connections from blocks
                foreach (VFXBlockController blockPres in (element as VFXContextController).blockControllers)
                {
                    blockPres.slotContainer.activationSlot?.UnlinkAll(true, true);
                    foreach (var slot in blockPres.slotContainer.outputSlots.Concat(blockPres.slotContainer.inputSlots))
                    {
                        slot.UnlinkAll(true, true);
                    }
                }

                // remove flow connections from context
                // TODO update data types
                context.UnlinkAll();
                // Detach from graph
                context.Detach();

                RemoveFromGroupNodes(element as VFXNodeController);

                UnityObject.DestroyImmediate(context, true);
            }
            else if (element is VFXBlockController block)
            {
                removedModel = block.model;
                needSyncCustomAttributes = HasCustomAttributes(removedModel);
                block.NodeGoingToBeRemoved();
                block.contextController.RemoveBlock(block.model);

                UnityObject.DestroyImmediate(block.model, true);
            }
            else if (element is VFXParameterNodeController parameter)
            {
                removedModel = parameter.model;
                needSyncCustomAttributes = HasCustomAttributes(removedModel);
                parameter.NodeGoingToBeRemoved();
                parameter.parentController.model.RemoveNode(parameter.infos);
                RemoveFromGroupNodes(parameter);
                DataEdgesMightHaveChanged();
            }
            else if (element is VFXNodeController or VFXParameterController)
            {
                IVFXSlotContainer container = null;

                if (element is VFXNodeController nodeController)
                {
                    removedModel = nodeController.model;
                    needSyncCustomAttributes = HasCustomAttributes(removedModel);
                    container = removedModel as IVFXSlotContainer;
                    nodeController.NodeGoingToBeRemoved();
                    RemoveFromGroupNodes(nodeController);
                }
                else
                {
                    removedModel = ((VFXParameterController)element).model;
                    needSyncCustomAttributes = HasCustomAttributes(removedModel);
                    container = (IVFXSlotContainer)removedModel;
                    foreach (var parameterNode in m_SyncedModels[removedModel])
                    {
                        RemoveFromGroupNodes(parameterNode);
                    }
                }

                VFXSlot slotToClean = container.activationSlot;
                do
                {
                    if (slotToClean)
                    {
                        slotToClean.UnlinkAll(true, true);
                    }
                    slotToClean = container.inputSlots.Concat(container.outputSlots)
                        .FirstOrDefault(o => o.HasLink(true));
                }
                while (slotToClean != null);

                graph.RemoveChild(container as VFXModel);

                UnityObject.DestroyImmediate(container as VFXModel, true);
                DataEdgesMightHaveChanged();
            }
            else if (element is VFXFlowEdgeController flowEdge)
            {
                var inputAnchor = flowEdge.input as VFXFlowAnchorController;
                var outputAnchor = flowEdge.output as VFXFlowAnchorController;

                if (inputAnchor != null && outputAnchor != null)
                {
                    var contextInput = inputAnchor.owner as VFXContext;
                    var contextOutput = outputAnchor.owner as VFXContext;

                    if (contextInput != null && contextOutput != null)
                        contextInput.UnlinkFrom(contextOutput, outputAnchor.slotIndex, inputAnchor.slotIndex);
                }
            }
            else if (element is VFXDataEdgeController)
            {
                var edge = element as VFXDataEdgeController;
                var to = edge.input as VFXDataAnchorController;

                if (to != null)
                {
                    if (explicitDelete)
                    {
                        to.sourceNode.OnEdgeFromInputGoingToBeRemoved(to);
                        edge.output.sourceNode.OnEdgeFromOutputGoingToBeRemoved(edge.output, edge.input);
                    }
                    var slot = to.model;
                    if (slot != null)
                    {
                        slot.UnlinkAll();
                    }
                    m_ForceDataEdgeNotification = true;
                }
            }
            else if (element is VFXGroupNodeController)
            {
                RemoveGroupNode(element as VFXGroupNodeController);
            }
            else if (element is VFXStickyNoteController)
            {
                RemoveStickyNote(element as VFXStickyNoteController);
            }
            else
            {
                Debug.LogErrorFormat("Unexpected type : {0}", element.GetType().FullName);
            }

            if (needSyncCustomAttributes)
            {
                graph.SyncCustomAttributes();
            }
        }

        private int m_LastFrameVFXReinit = 0;

        private void ReinitIfNeeded(VFXModel model, VFXModel.InvalidationCause cause)
        {
            if (cause == VFXModel.InvalidationCause.kInitValueChanged)
            {
                var window = VFXViewWindow.GetWindow(this.graph, false, false);
                int currentFrame = Time.frameCount;

                if (window &&
                    window.autoReinit &&
                    m_LastFrameVFXReinit != currentFrame) // Prevent multi reinit per frame)
                {
                    var vfx = window.graphView.attachedComponent;

                    if (vfx)
                    {
                        vfx.Reinit();
                        int targetFPS = VFXViewPreference.authoringPrewarmStepCountPerSeconds;
                        if (window.autoReinitPrewarmTime > 0.0f && targetFPS > 0)
                        {
                            bool alreadyHasPrewarm = vfx.visualEffectAsset.GetResource().preWarmStepCount > 0;

                            if (!alreadyHasPrewarm)
                            {
                                float stepTime = 1.0f / targetFPS;
                                uint stepCount = (uint)(window.autoReinitPrewarmTime / stepTime + 1);
                                stepTime = window.autoReinitPrewarmTime / stepCount;
                                vfx.Simulate(stepTime, stepCount);
                            }
                        }
                    }
                    m_LastFrameVFXReinit = currentFrame;
                }
            }
        }

        protected override void ModelChanged(UnityObject obj)
        {
            if (model == null)
            {
                NotifyChange(Change.destroy);
                GraphLost();

                RemoveController(this);
                return;
            }

            // a standard equals will return true is the m_Graph is a destroyed object with the same instance ID ( like with a source control revert )
            if (!object.ReferenceEquals(m_Graph, model.GetOrCreateGraph()))
            {
                if (!object.ReferenceEquals(m_Graph, null))
                {
                    UnRegisterNotification(m_Graph, GraphChanged);
                    UnRegisterNotification(m_UI, UIChanged);
                }
                if (m_Graph != null)
                {
                    GraphLost();
                }
                else
                {
                    Clear();
                }
                m_Graph = model.GetOrCreateGraph();
                m_Graph.SanitizeGraph();

                if (m_Graph != null)
                {
                    RegisterNotification(m_Graph, GraphChanged);

                    AddInvalidateDelegate(m_Graph, InvalidateExpressionGraph);
                    AddInvalidateDelegate(m_Graph, IncremenentGraphUndoRedoState);
                    AddInvalidateDelegate(m_Graph, ReinitIfNeeded);

                    m_UI = m_Graph.UIInfos;

                    RegisterNotification(m_UI, UIChanged);

                    GraphChanged();
                }
            }
        }

        public void AddGroupNode(Vector2 pos)
        {
            PrivateAddGroupNode(pos);

            m_Graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        public void AddStickyNote(Vector2 position, VFXGroupNodeController group)
        {
            var ui = graph.UIInfos;

            var stickyNoteInfo = new VFXUI.StickyNoteInfo
            {
                title = "Title",
                position = new Rect(position, Vector2.one * 100),
                contents = "type something here",
                theme = StickyNoteTheme.Classic.ToString(),
                textSize = StickyNoteFontSize.Small.ToString()
            };

            if (ui.stickyNoteInfos != null)
                ui.stickyNoteInfos = ui.stickyNoteInfos.Concat(Enumerable.Repeat(stickyNoteInfo, 1)).ToArray();
            else
                ui.stickyNoteInfos = new VFXUI.StickyNoteInfo[] { stickyNoteInfo };

            if (group != null)
            {
                LightApplyChanges();

                group.AddStickyNote(m_StickyNoteControllers[ui.stickyNoteInfos.Length - 1]);
            }

            m_Graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        void RemoveGroupNode(VFXGroupNodeController groupNode)
        {
            var ui = graph.UIInfos;

            int index = groupNode.index;

            ui.groupInfos = ui.groupInfos.Where((t, i) => i != index).ToArray();

            groupNode.Remove();
            m_GroupNodeControllers.RemoveAt(index);

            for (int i = index; i < m_GroupNodeControllers.Count; ++i)
            {
                m_GroupNodeControllers[i].index = i;
            }
            m_Graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        void RemoveStickyNote(VFXStickyNoteController stickyNote)
        {
            var ui = graph.UIInfos;

            int index = stickyNote.index;

            ui.stickyNoteInfos = ui.stickyNoteInfos.Where((t, i) => i != index).ToArray();

            stickyNote.Remove();
            m_StickyNoteControllers.RemoveAt(index);

            for (int i = index; i < m_StickyNoteControllers.Count; ++i)
            {
                m_StickyNoteControllers[i].index = i;
            }

            //Patch group nodes, removing this sticky note and fixing ids that are bigger than index
            if (ui.groupInfos != null)
            {
                for (int i = 0; i < ui.groupInfos.Length; ++i)
                {
                    for (int j = 0; j < ui.groupInfos[i].contents.Length; ++j)
                    {
                        if (ui.groupInfos[i].contents[j].isStickyNote)
                        {
                            if (ui.groupInfos[i].contents[j].id == index)
                            {
                                ui.groupInfos[i].contents = ui.groupInfos[i].contents.Where((t, idx) => idx != j).ToArray();
                                j--;
                            }
                            else if (ui.groupInfos[i].contents[j].id > index)
                            {
                                --(ui.groupInfos[i].contents[j].id);
                            }
                        }
                    }
                }
            }

            m_Graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        void RemoveFromGroupNodes(VFXNodeController node)
        {
            foreach (var groupNode in m_GroupNodeControllers)
            {
                if (groupNode.ContainsNode(node))
                {
                    groupNode.RemoveNode(node);
                }
            }
        }

        protected void GraphChanged()
        {
            if (m_Graph == null)
            {
                if (model != null)
                {
                    ModelChanged(model);
                }
                return;
            }

            VFXGraphValidation validation = new VFXGraphValidation(m_Graph);
            validation.ValidateGraph();

            bool groupNodeChanged = false;

            Profiler.BeginSample("VFXViewController.GraphChanged:SyncControllerFromModel");
            SyncControllerFromModel(ref groupNodeChanged);
            Profiler.EndSample();

            Profiler.BeginSample("VFXViewController.GraphChanged:NotifyChange(AnyThing)");
            NotifyChange(AnyThing);
            Profiler.EndSample();

            //if( groupNodeChanged)
            {
                Profiler.BeginSample("VFXViewController.GraphChanged:NotifyChange(Change.groupNode)");
                NotifyChange(Change.groupNode);
                Profiler.EndSample();
            }
        }

        protected void UIChanged()
        {
            if (m_UI == null) return;
            if (m_Graph == null) return; // OnModelChange or OnDisable will take care of that later

            bool groupNodeChanged = false;
            RecreateUI(ref groupNodeChanged);

            NotifyChange(Change.ui);
        }

        public void NotifyParameterControllerChange()
        {
            DataEdgesMightHaveChanged();
            if (!m_Syncing)
                NotifyChange(AnyThing);
        }

        public void RegisterFlowAnchorController(VFXFlowAnchorController controller)
        {
            if (!m_FlowAnchorController.Contains(controller))
                m_FlowAnchorController.Add(controller);
        }

        public void UnregisterFlowAnchorController(VFXFlowAnchorController controller)
        {
            m_FlowAnchorController.Remove(controller);
        }

        public static void CollectAncestorOperator(IVFXSlotContainer operatorInput, HashSet<IVFXSlotContainer> hashParents)
        {
            IEnumerable<VFXSlot> slots = operatorInput.inputSlots;
            if (operatorInput.activationSlot != null)
                slots = slots.Append(operatorInput.activationSlot);

            foreach (var slotInput in slots)
            {
                var linkedSlots = slotInput.AllChildrenWithLink();
                foreach (var linkedSlot in linkedSlots)
                {
                    RecurseCollectAncestorOperator(linkedSlot.refSlot.owner, hashParents);
                }
            }
        }

        public static void RecurseCollectAncestorOperator(IVFXSlotContainer operatorInput, HashSet<IVFXSlotContainer> hashParents)
        {
            if (hashParents.Contains(operatorInput))
                return;

            hashParents.Add(operatorInput);

            IEnumerable<VFXSlot> slots = operatorInput.inputSlots;
            if (operatorInput.activationSlot != null)
                slots = slots.Append(operatorInput.activationSlot);

            foreach (var slotInput in slots)
            {
                var linkedSlots = slotInput.AllChildrenWithLink();
                foreach (var linkedSlot in linkedSlots)
                {
                    RecurseCollectAncestorOperator(linkedSlot.refSlot.owner, hashParents);
                }
            }
        }

        public static void CollectDescendantOperator(IVFXSlotContainer operatorInput, HashSet<IVFXSlotContainer> hashChildren)
        {
            foreach (var slotOutput in operatorInput.outputSlots)
            {
                var linkedSlots = slotOutput.AllChildrenWithLink();
                foreach (var linkedSlot in linkedSlots)
                {
                    foreach (var link in linkedSlot.LinkedSlots)
                    {
                        RecurseCollectDescendantOperator(link.owner, hashChildren);
                    }
                }
            }
        }

        public static void RecurseCollectDescendantOperator(IVFXSlotContainer operatorInput, HashSet<IVFXSlotContainer> hashChildren)
        {
            if (hashChildren.Contains(operatorInput))
                return;

            hashChildren.Add(operatorInput);
            foreach (var slotOutput in operatorInput.outputSlots)
            {
                var linkedSlots = slotOutput.AllChildrenWithLink();
                foreach (var linkedSlot in linkedSlots)
                {
                    foreach (var link in linkedSlot.LinkedSlots)
                    {
                        RecurseCollectDescendantOperator(link.owner, hashChildren);
                    }
                }
            }
        }

        public IEnumerable<VFXDataAnchorController> GetCompatiblePorts(VFXDataAnchorController startAnchorController, NodeAdapter nodeAdapter)
        {
            var cacheLinkData = new VFXDataAnchorController.CanLinkCache();

            var direction = startAnchorController.direction;
            foreach (var slotContainer in AllSlotContainerControllers)
            {
                var sourceSlot = direction == Direction.Input ? slotContainer.outputPorts : slotContainer.inputPorts;
                foreach (var slot in sourceSlot)
                {
                    if (startAnchorController.CanLink(slot, cacheLinkData))
                    {
                        yield return slot;
                    }
                }
            }
        }

        public List<VFXFlowAnchorController> GetCompatiblePorts(VFXFlowAnchorController startAnchorController, NodeAdapter nodeAdapter)
        {
            var res = new List<VFXFlowAnchorController>();

            var startFlowAnchorController = (VFXFlowAnchorController)startAnchorController;
            foreach (var anchorController in m_FlowAnchorController)
            {
                VFXContext owner = anchorController.owner;
                if (owner == null ||
                    startAnchorController == anchorController ||
                    startAnchorController.direction == anchorController.direction ||
                    owner == startFlowAnchorController.owner)
                    continue;

                if (startAnchorController.direction == Direction.Input)
                {
                    if (VFXFlowAnchorController.CanLink(anchorController, startFlowAnchorController))
                        res.Add(anchorController);
                }
                else
                {
                    if (VFXFlowAnchorController.CanLink(startFlowAnchorController, anchorController))
                        res.Add(anchorController);
                }
            }
            return res;
        }

        public void AddVFXModel(Vector2 pos, VFXModel model)
        {
            model.position = pos;
            this.graph.AddChild(model);
        }

        public VFXContext AddVFXContext(Vector2 pos, Variant variant)
        {
            VFXContext model = (VFXContext)variant.CreateInstance();
            AddVFXModel(pos, model);
            return model;
        }

        public VFXOperator AddVFXOperator(Vector2 pos, Variant variant)
        {
            var model = (VFXOperator)variant.CreateInstance();
            AddVFXModel(pos, model);
            return model;
        }

        public VFXParameter AddVFXParameter(Vector2 pos, Variant variant, bool parent = true)
        {
            var parameter = (VFXParameter)variant.CreateInstance();
            if (parent)
                AddVFXModel(pos, parameter);

            Type type = parameter.type;

            parameter.collapsed = true;

            int order = 0;
            if (m_ParameterControllers.Count > 0)
            {
                order = m_ParameterControllers.Keys.Select(t => t.order).Max() + 1;
            }
            parameter.order = order;
            parameter.SetSettingValue("m_ExposedName", $"New {ObjectNames.NicifyVariableName(type.UserFriendlyName())}");

            if (!type.IsPrimitive)
            {
                parameter.value = VFXTypeExtension.GetDefaultField(type);
            }

            return parameter;
        }

        public VFXNodeController GetNewNodeController(VFXModel model)
        {
            List<VFXNodeController> nodeControllers = null;
            if (m_SyncedModels.TryGetValue(model, out nodeControllers))
            {
                return nodeControllers.FirstOrDefault();
            }
            bool groupNodeChanged = false;
            SyncControllerFromModel(ref groupNodeChanged);

            m_SyncedModels.TryGetValue(model, out nodeControllers);

            return nodeControllers.FirstOrDefault();
        }

        public VFXNodeController AddNode(Vector2 tPos, Variant variant, VFXGroupNodeController groupNode)
        {
            VFXModel newNode = null;
            if (variant.modelType.IsSubclassOf(typeof(VFXOperator)))
            {
                newNode = AddVFXOperator(tPos, variant);
            }
            else if (variant.modelType.IsSubclassOf(typeof(VFXContext)))
            {
                newNode = AddVFXContext(tPos, variant);
            }
            else if (variant.modelType == typeof(VFXParameter))
            {
                newNode = AddVFXParameter(tPos, variant);
            }
            if (newNode != null)
            {
                var groupNodeChanged = false;
                SyncControllerFromModel(ref groupNodeChanged);

                m_SyncedModels.TryGetValue(newNode, out var nodeControllers);

                if (newNode is VFXParameter newParameter)
                {
                    // Set an exposed name on a new parameter so that uniqueness is ensured
                    m_ParameterControllers[newParameter].exposedName = $"New {ObjectNames.NicifyVariableName(newParameter.type.UserFriendlyName())}";
                }

                NotifyChange(AnyThing);

                groupNode?.AddNode(nodeControllers.First());

                return nodeControllers[0];
            }

            return null;
        }

        public VFXNodeController AddVFXParameter(Vector2 pos, VFXParameterController parameterController, VFXGroupNodeController groupNode)
        {
            if (parameterController.isOutput && parameterController.hasNodes)
            {
                return parameterController.nodes.First();
            }
            int id = parameterController.model.AddNode(pos);

            LightApplyChanges();

            var nodeController = GetRootNodeController(parameterController.model, id);

            if (groupNode != null)
            {
                if (nodeController != null)
                {
                    groupNode.AddNode(nodeController);
                }
            }

            return nodeController;
        }

        public void Clear()
        {
            foreach (var element in allChildren)
            {
                element.OnDisable();
            }

            m_FlowAnchorController.Clear();
            m_SyncedModels.Clear();
            m_ParameterControllers.Clear();
            m_DataEdges.Clear();
            m_FlowEdges.Clear();
            m_GroupNodeControllers.Clear();
            m_StickyNoteControllers.Clear();
        }

        private Dictionary<VFXModel, List<VFXModel.InvalidateEvent>> m_registeredEvent = new Dictionary<VFXModel, List<VFXModel.InvalidateEvent>>();
        public void AddInvalidateDelegate(VFXModel model, VFXModel.InvalidateEvent evt)
        {
            model.onInvalidateDelegate += evt;
            if (!m_registeredEvent.ContainsKey(model))
            {
                m_registeredEvent.Add(model, new List<VFXModel.InvalidateEvent>());
            }
            m_registeredEvent[model].Add(evt);
        }

        public void RemoveInvalidateDelegate(VFXModel model, VFXModel.InvalidateEvent evt)
        {
            List<VFXModel.InvalidateEvent> evtList;
            if (model != null && m_registeredEvent.TryGetValue(model, out evtList))
            {
                model.onInvalidateDelegate -= evt;
                evtList.Remove(evt);
                if (evtList.Count == 0)
                {
                    m_registeredEvent.Remove(model);
                }
            }
        }

        static Dictionary<VisualEffectResource, VFXViewController> s_Controllers = new Dictionary<VisualEffectResource, VFXViewController>();

        public static VFXViewController GetController(VisualEffectResource resource, bool forceUpdate = false)
        {
            //TRANSITION : delete VFXAsset as it should be in Library
            resource.ValidateAsset();

            VFXViewController controller;
            if (!s_Controllers.TryGetValue(resource, out controller))
            {
                controller = new VFXViewController(resource);
                s_Controllers[resource] = controller;
            }
            else
            {
                if (forceUpdate)
                {
                    controller.ForceReload();
                }
            }

            return controller;
        }

        static void RemoveController(VFXViewController controller)
        {
            if (s_Controllers.ContainsKey(controller.model))
            {
                controller.OnDisable();
                s_Controllers.Remove(controller.model);
            }
        }

        internal VFXViewController(VisualEffectResource vfx) : base(vfx)
        {
            ModelChanged(vfx); // This will initialize the graph from the vfx asset.

            if (m_FlowAnchorController == null)
                m_FlowAnchorController = new List<VFXFlowAnchorController>();

            Undo.undoRedoPerformed += SynchronizeUndoRedoState;
            Undo.willFlushUndoRecord += WillFlushUndoRecord;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(vfx));
            vfx.name = fileName;

            if (m_Graph != null)
                m_Graph.BuildParameterInfo();


            InitializeUndoStack();
            GraphChanged();

            Sanitize();
        }

        void Sanitize()
        {
            VFXParameter[] parameters = m_ParameterControllers.Keys.OrderBy(t => t.order).ToArray();
            if (parameters.Length > 0)
            {
                var existingNames = new HashSet<string>();

                existingNames.Add(parameters[0].exposedName);
                m_ParameterControllers[parameters[0]].order = 0;

                for (int i = 1; i < parameters.Length; ++i)
                {
                    var controller = m_ParameterControllers[parameters[i]];
                    controller.order = i;

                    controller.CheckNameUnique(existingNames);

                    existingNames.Add(parameters[i].exposedName);
                }
            }
        }

        public ReadOnlyCollection<VFXGroupNodeController> groupNodes
        {
            get { return m_GroupNodeControllers.AsReadOnly(); }
        }
        public ReadOnlyCollection<VFXStickyNoteController> stickyNotes
        {
            get { return m_StickyNoteControllers.AsReadOnly(); }
        }

        List<VFXGroupNodeController> m_GroupNodeControllers = new List<VFXGroupNodeController>();
        List<VFXStickyNoteController> m_StickyNoteControllers = new List<VFXStickyNoteController>();

        public bool RecreateUI(ref bool groupNodeChanged)
        {
            bool changed = false;
            var ui = graph.UIInfos;

            if (ui.groupInfos != null)
            {
                HashSet<VFXNodeID> usedNodeIds = new HashSet<VFXNodeID>();
                // first make sure that nodesID are at most in one groupnode.

                for (int i = 0; i < ui.groupInfos.Length; ++i)
                {
                    if (ui.groupInfos[i].contents != null)
                    {
                        for (int j = 0; j < ui.groupInfos[i].contents.Length; ++j)
                        {
                            if (usedNodeIds.Contains(ui.groupInfos[i].contents[j]))
                            {
                                Debug.Log("Element present in multiple groupnodes");
                                --j;
                                ui.groupInfos[i].contents = ui.groupInfos[i].contents.Where((t, k) => k != j).ToArray();
                            }
                            else
                            {
                                usedNodeIds.Add(ui.groupInfos[i].contents[j]);
                            }
                        }
                    }
                }

                for (int i = m_GroupNodeControllers.Count; i < ui.groupInfos.Length; ++i)
                {
                    VFXGroupNodeController groupNodeController = new VFXGroupNodeController(this, ui, i);
                    m_GroupNodeControllers.Add(groupNodeController);
                    changed = true;
                    groupNodeChanged = true;
                }

                while (ui.groupInfos.Length < m_GroupNodeControllers.Count)
                {
                    m_GroupNodeControllers.Last().OnDisable();
                    m_GroupNodeControllers.RemoveAt(m_GroupNodeControllers.Count - 1);
                    changed = true;
                    groupNodeChanged = true;
                }
            }
            if (ui.stickyNoteInfos != null)
            {
                for (int i = m_StickyNoteControllers.Count; i < ui.stickyNoteInfos.Length; ++i)
                {
                    VFXStickyNoteController stickyNoteController = new VFXStickyNoteController(this, ui, i);
                    m_StickyNoteControllers.Add(stickyNoteController);
                    stickyNoteController.ApplyChanges();
                    changed = true;
                }

                while (ui.stickyNoteInfos.Length < m_StickyNoteControllers.Count)
                {
                    m_StickyNoteControllers.Last().OnDisable();
                    m_StickyNoteControllers.RemoveAt(m_StickyNoteControllers.Count - 1);
                    changed = true;
                }
            }

            return changed;
        }

        public void ValidateCategoryList()
        {
            if (!m_Syncing)
            {
                var ui = graph.UIInfos;
                // Validate category list
                var categories = ui.categories ?? new List<VFXUI.CategoryInfo>();

                string[] missingCategories = m_ParameterControllers.Select(t => t.Key.category).Where(t => !string.IsNullOrEmpty(t)).Except(categories.Select(t => t.name)).ToArray();

                HashSet<string> foundCategories = new HashSet<string>();

                for (int i = 0; i < categories.Count; ++i)
                {
                    string category = categories[i].name;
                    if (string.IsNullOrEmpty(category) || foundCategories.Contains(category))
                    {
                        categories.RemoveAt(i);
                        --i;
                    }
                    foundCategories.Add(category);
                }

                if (missingCategories.Length > 0)
                {
                    categories.AddRange(missingCategories.Select(t => new VFXUI.CategoryInfo { name = t }));
                    ui.categories = categories;
                    ui.Modified(true);
                }
            }
        }

        public void ForceReload()
        {
            Clear();
            ModelChanged(model);
            GraphChanged();
        }

        bool m_Syncing;

        public bool SyncControllerFromModel(ref bool groupNodeChanged)
        {
            m_Syncing = true;
            bool changed = false;
            var toRemove = m_SyncedModels.Keys.Except(graph.children).ToList();
            foreach (var m in toRemove)
            {
                RemoveControllersFromModel(m);
                changed = true;
            }

            var toAdd = graph.children.Except(m_SyncedModels.Keys).ToList();
            foreach (var m in toAdd)
            {
                AddControllersFromModel(m);
                changed = true;
            }


            // make sure every parameter instance is created before we look for edges
            foreach (var parameter in m_ParameterControllers.Values)
            {
                parameter.UpdateControllers();
            }

            changed |= RecreateNodeEdges();
            changed |= RecreateFlowEdges();

            changed |= RecreateUI(ref groupNodeChanged);

            m_Syncing = false;
            ValidateCategoryList();
            UpdateSystems();
            return changed;
        }

        Dictionary<VFXParameter, VFXParameterController> m_ParameterControllers = new Dictionary<VFXParameter, VFXParameterController>();

        public IEnumerable<VFXParameterController> parameterControllers => m_ParameterControllers.Values;


        public void ChangeCategory(VFXParameter parameter, string newCategory)
        {
            var parameterController = parameterControllers.Single(x => x.exposedName == parameter.exposedName);
            parameterController.model.category = newCategory;
            graph.Invalidate(VFXModel.InvalidationCause.kSettingChanged);
        }

        public int GetCategoryIndex(string category)
        {
            return graph.UIInfos.categories.FindIndex(x => string.Compare(x.name, category, StringComparison.OrdinalIgnoreCase) == 0);
        }

        public void MoveCategory(string category, int index)
        {
            if (graph.UIInfos.categories == null)
                return;
            int oldIndex = graph.UIInfos.categories.FindIndex(t => t.name == category);

            if (oldIndex == -1 || oldIndex == index)
                return;
            graph.UIInfos.categories.RemoveAt(oldIndex);
            if (index >= 0 && index < graph.UIInfos.categories.Count)
                graph.UIInfos.categories.Insert(index, new VFXUI.CategoryInfo { name = category });
            else
                graph.UIInfos.categories.Add(new VFXUI.CategoryInfo { name = category });

            graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        public bool RenameCategory(string oldName, string newName)
        {
            var category = graph.UIInfos.categories.SingleOrDefault(x => x.name == oldName);
            if (!string.IsNullOrEmpty(category.name))
            {
                if (graph.UIInfos.categories.All(t => t.name != newName))
                {
                    graph.UIInfos.categories.Remove(category);
                    category.name = newName;
                    graph.UIInfos.categories.Add(category);

                    foreach (var parameter in m_ParameterControllers)
                    {
                        if (parameter.Key.category == oldName)
                        {
                            parameter.Key.category = newName;
                        }
                    }


                    graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
                    return true;
                }
                else
                {
                    Debug.LogError("Can't change name, category with the same name already exists");
                }
            }
            else
            {
                Debug.LogError("Can't change name, category not found");
            }

            return false;
        }

        public bool RemoveCategory(string category)
        {
            var index = graph.UIInfos.categories.FindIndex(x => string.Compare(x.name, category, StringComparison.OrdinalIgnoreCase) == 0);

            if (index != -1)
            {
                graph.UIInfos.categories.RemoveAt(index);
                var parametersToRemove = m_ParameterControllers
                    .Where(x => string.Compare(x.Key.category, category, StringComparison.OrdinalIgnoreCase) == 0)
                    .Select(x => x.Value)
                    .ToList();

                Remove(parametersToRemove);

                graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
                return true;
            }

            return false;
        }

        public void SetParametersOrder(VFXParameterController controller, int index)
        {
            var orderedParameters = m_ParameterControllers
                .Where(t => t.Value.isOutput == controller.isOutput && t.Value.model.category == controller.model.category)
                .OrderBy(t => t.Value.order)
                .Select(t => t.Value)
                .ToList();

            int oldIndex = orderedParameters.IndexOf(controller);

            if (oldIndex != -1)
            {
                orderedParameters.RemoveAt(oldIndex);

                if (oldIndex < index)
                    --index;
            }

            if (index < orderedParameters.Count)
                orderedParameters.Insert(index, controller);
            else
                orderedParameters.Add(controller);

            for (var i = 0; i < orderedParameters.Count; ++i)
            {
                orderedParameters[i].order = i;
            }
            NotifyChange(AnyThing);
        }

        public void GetCategoryExpanded(string categoryName, out bool isExpanded)
        {
            var category = graph.UIInfos.categories?.SingleOrDefault(x => x.name == categoryName);
            if (!string.IsNullOrEmpty(category?.name))
            {
                isExpanded = !category.Value.collapsed;
            }
            else
            {
                isExpanded = false;
            }
        }

        public void SetCategoryExpanded(string categoryName, bool expanded)
        {
            if (graph.UIInfos.categories != null)
            {
                for (int i = 0; i < graph.UIInfos.categories.Count; ++i)
                {
                    var category = graph.UIInfos.categories[i];
                    if (category.name == categoryName && category.collapsed != !expanded)
                    {
                        category.collapsed = !expanded;
                        graph.UIInfos.categories[i] = category;
                        NotifyChange(AnyThing);
                        break;
                    }
                }
            }
        }

        private void AddControllersFromModel(VFXModel model)
        {
            List<VFXNodeController> newControllers = new List<VFXNodeController>();
            if (model is VFXOperator)
            {
                if (model is VFXOperatorNumericCascadedUnified)
                    newControllers.Add(new VFXCascadedOperatorController(model as VFXOperator, this));
                else if (model is VFXOperatorNumericUniform)
                {
                    newControllers.Add(new VFXNumericUniformOperatorController(model as VFXOperator, this));
                }
                else if (model is VFXOperatorNumericUnified)
                {
                    if (model is IVFXOperatorNumericUnifiedConstrained)
                        newControllers.Add(new VFXUnifiedConstraintOperatorController(model as VFXOperator, this));
                    else
                        newControllers.Add(new VFXUnifiedOperatorController(model as VFXOperator, this));
                }
                else if (model is VFXOperatorDynamicType)
                {
                    newControllers.Add(new VFXDynamicTypeOperatorController(model as VFXOperator, this));
                }
                else
                    newControllers.Add(new VFXOperatorController(model as VFXOperator, this));
            }
            else if (model is VFXContext)
            {
                newControllers.Add(new VFXContextController(model as VFXContext, this));
            }
            else if (model is VFXParameter)
            {
                VFXParameter parameter = model as VFXParameter;

                if (parameter.isOutput)
                {
                    if (parameter.GetNbInputSlots() < 1)
                    {
                        parameter.AddSlot(VFXSlot.Create(new VFXProperty(typeof(float), "i"), VFXSlot.Direction.kInput));
                    }
                    while (parameter.GetNbInputSlots() > 1)
                    {
                        parameter.RemoveSlot(parameter.inputSlots[1]);
                    }
                    while (parameter.GetNbOutputSlots() > 0)
                    {
                        parameter.RemoveSlot(parameter.outputSlots[0]);
                    }
                }
                else
                {
                    if (parameter.GetNbOutputSlots() < 1)
                    {
                        parameter.AddSlot(VFXSlot.Create(new VFXProperty(typeof(float), "o"), VFXSlot.Direction.kOutput));
                    }
                    while (parameter.GetNbOutputSlots() > 1)
                    {
                        parameter.RemoveSlot(parameter.outputSlots[1]);
                    }
                    while (parameter.GetNbInputSlots() > 0)
                    {
                        parameter.RemoveSlot(parameter.inputSlots[0]);
                    }
                }

                parameter.ValidateNodes();

                m_ParameterControllers[parameter] = new VFXParameterController(parameter, this);

                m_SyncedModels[model] = new List<VFXNodeController>();
            }

            if (newControllers.Count > 0)
            {
                List<VFXNodeController> existingControllers;
                if (m_SyncedModels.TryGetValue(model, out existingControllers))
                {
                    Debug.LogError("adding a model to controllers twice");
                }
                m_SyncedModels[model] = newControllers;
                foreach (var controller in newControllers)
                {
                    controller.ForceUpdate();
                }
            }
        }

        public void AddControllerToModel(VFXModel model, VFXNodeController controller)
        {
            m_SyncedModels[model].Add(controller);
        }

        public void RemoveControllerFromModel(VFXModel model, VFXNodeController controller)
        {
            m_SyncedModels[model].Remove(controller);
        }

        private void RemoveControllersFromModel(VFXModel model)
        {
            List<VFXNodeController> controllers = null;
            if (m_SyncedModels.TryGetValue(model, out controllers))
            {
                foreach (var controller in controllers)
                {
                    controller.OnDisable();
                }
                m_SyncedModels.Remove(model);
            }
            if (model is VFXParameter)
            {
                m_ParameterControllers[model as VFXParameter].OnDisable();
                m_ParameterControllers.Remove(model as VFXParameter);
            }
        }

        public VFXNodeController GetNodeController(VFXModel model, int id)
        {
            if (model is VFXBlock)
            {
                VFXContextController controller = GetRootNodeController(model.GetParent(), 0) as VFXContextController;
                if (controller == null)
                    return null;
                return controller.blockControllers.FirstOrDefault(t => t.model == model);
            }
            else
            {
                return GetRootNodeController(model, id);
            }
        }

        public void ChangeEventName(string oldName, string newName)
        {
            foreach (var context in m_SyncedModels.Keys.OfType<VFXBasicEvent>())
            {
                if (context.eventName == oldName)
                    context.SetSettingValue("eventName", newName);
            }
        }

        public VFXNodeController GetRootNodeController(VFXModel model, int id)
        {
            List<VFXNodeController> controller = null;
            m_SyncedModels.TryGetValue(model, out controller);
            if (controller == null) return null;

            return controller.FirstOrDefault(t => t.id == id);
        }

        public VFXStickyNoteController GetStickyNoteController(int index)
        {
            return m_StickyNoteControllers[index];
        }

        public VFXParameterController GetParameterController(VFXParameter parameter)
        {
            VFXParameterController controller = null;
            m_ParameterControllers.TryGetValue(parameter, out controller);
            return controller;
        }

        VFXUI.GroupInfo PrivateAddGroupNode(Vector2 position)
        {
            var ui = graph.UIInfos;

            var newGroupInfo = new VFXUI.GroupInfo { title = "New Group Node", position = new Rect(position, Vector2.one * 100) };

            if (ui.groupInfos != null)
                ui.groupInfos = ui.groupInfos.Concat(Enumerable.Repeat(newGroupInfo, 1)).ToArray();
            else
                ui.groupInfos = new VFXUI.GroupInfo[] { newGroupInfo };

            return ui.groupInfos.Last();
        }

        public void GroupNodes(VFXNodeController[] nodes) => GroupNodes(nodes, Array.Empty<VFXStickyNoteController>());

        public void GroupNodes(VFXNodeController[] nodes, VFXStickyNoteController[] stickyNoteControllers)
        {
            // If a node from the selection already belongs to a group, remove it from this group
            foreach (var g in groupNodes.ToArray())
            {
                g.RemoveNodes(nodes);
                g.RemoveStickyNotes(stickyNoteControllers);
                if (g.nodes.Count() == 0)
                {
                    RemoveGroupNode(g);
                }

            }

            var info = PrivateAddGroupNode(Vector2.zero);

            info.contents = nodes.Select(t => new VFXNodeID(t.model, t.id))
                .Concat(stickyNoteControllers.Select(x => new VFXNodeID(x.index)))
                .ToArray();

            m_Graph.Invalidate(VFXModel.InvalidationCause.kUIChanged);
        }

        public void PutInSameGroupNodeAs(VFXNodeController target, VFXNodeController example)
        {
            var ui = graph.UIInfos;
            if (ui.groupInfos == null) return;

            foreach (var groupNode in m_GroupNodeControllers)
            {
                if (groupNode.nodes.Contains(example))
                {
                    groupNode.AddNode(target);
                    break;
                }
            }
        }

        List<VFXSystemController> m_Systems = new List<VFXSystemController>();

        public ReadOnlyCollection<VFXSystemController> systems
        {
            get { return m_Systems.AsReadOnly(); }
        }


        public void UpdateSystems()
        {
            try
            {
                VFXContext[] directContexts = graph.children.OfType<VFXContext>().ToArray();

                HashSet<VFXContext> initializes = new HashSet<VFXContext>(directContexts.Where(t => t.contextType == VFXContextType.Init).ToArray());
                HashSet<VFXContext> updates = new HashSet<VFXContext>(directContexts.Where(t => t.contextType == VFXContextType.Update).ToArray());

                List<Dictionary<VFXContext, int>> systems = new List<Dictionary<VFXContext, int>>();


                while (initializes.Count > 0 || updates.Count > 0)
                {
                    int generation = 0;

                    VFXContext currentContext;
                    if (initializes.Count > 0)
                    {
                        currentContext = initializes.First();
                        initializes.Remove(currentContext);
                    }
                    else
                    {
                        currentContext = updates.First();
                        updates.Remove(currentContext);
                    }

                    Dictionary<VFXContext, int> system = new Dictionary<VFXContext, int>();

                    system.Add(currentContext, generation);

                    var allChildren = currentContext.outputFlowSlot.Where(t => t != null).SelectMany(t => t.link.Select(u => u.context)).Where(t => t != null).ToList();
                    while (allChildren.Count() > 0)
                    {
                        ++generation;

                        foreach (var child in allChildren)
                        {
                            initializes.Remove(child);
                            updates.Remove(child);
                            system.Add(child, generation);
                        }

                        var allSubChildren = allChildren.SelectMany(t => t.outputFlowSlot.Where(u => u != null).SelectMany(u => u.link.Select(v => v.context).Where(v => v != null)));
                        var allPreChildren = allChildren.SelectMany(t => t.inputFlowSlot.Where(u => u != null).SelectMany(u => u.link.Select(v => v.context).Where(v => v != null && v.contextType != VFXContextType.Spawner && v.contextType != VFXContextType.SpawnerGPU)));

                        allChildren = allSubChildren.Concat(allPreChildren).Except(system.Keys).ToList();
                    }

                    if (system.Count > 1)
                        systems.Add(system);
                }

                while (m_Systems.Count() < systems.Count())
                {
                    VFXSystemController systemController = new VFXSystemController(graph.UIInfos);
                    m_Systems.Add(systemController);
                }

                while (m_Systems.Count() > systems.Count())
                {
                    VFXSystemController systemController = m_Systems.Last();
                    m_Systems.RemoveAt(m_Systems.Count - 1);
                    systemController.OnDisable();
                }

                for (int i = 0; i < systems.Count(); ++i)
                {
                    var contextToController = systems[i].Keys.Select(t => new KeyValuePair<VFXContextController, VFXContext>((VFXContextController)GetNodeController(t, 0), t)).Where(t => t.Key != null).ToDictionary(t => t.Value, t => t.Key);
                    m_Systems[i].contexts = contextToController.Values.ToArray();
                    VFXContextType type = VFXContextType.None;
                    VFXContext prevContext = null;
                    var orderedContexts = contextToController.Keys.OrderBy(t => t.contextType).ThenBy(t => systems[i][t]).ThenBy(t => t.position.x).ThenBy(t => t.position.y).ToArray();

                    char letter = 'A';
                    foreach (var context in orderedContexts)
                    {
                        if (context.contextType == type)
                        {
                            if (prevContext != null)
                            {
                                letter = 'A';
                                prevContext.letter = letter;
                                prevContext = null;
                            }

                            if (letter == 'Z') // loop back to A in the unlikely event that there are more than 26 contexts
                                letter = 'a';
                            else if (letter == 'z')
                                letter = 'α';
                            else if (letter == 'ω')
                                letter = 'A';
                            context.letter = ++letter;
                        }
                        else
                        {
                            context.letter = '\0';
                            prevContext = context;
                        }
                        type = context.contextType;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private VFXGraph m_Graph;

        private VFXUI m_UI;

        private VFXView m_View; // Don't call directly as it is lazy initialized
    }
}
