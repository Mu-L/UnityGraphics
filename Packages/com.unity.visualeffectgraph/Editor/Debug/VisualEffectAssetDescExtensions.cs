using System.Text;
using UnityEditor.VFX;

static class VisualEffectAssetDescExtensions
{
    internal static string ToDetailedString(this VisualEffectAssetDesc assetDesc)
    {
        var sb = new StringBuilder();

        sb.AppendLine("VisualEffectAssetDesc:");
        sb.AppendLine("  Expression Sheet:");
        if (assetDesc.sheet.expressions != null)
        {
            sb.AppendLine($"    Expressions Count: {assetDesc.sheet.expressions.Length}");
            for (int i = 0; i < assetDesc.sheet.expressions.Length; i++)
            {
                var expr = assetDesc.sheet.expressions[i];
                sb.AppendLine($"      Expression {i}: {expr.op}");
            }
        }
        else
        {
            sb.AppendLine("    Expressions: null");
        }

        if (assetDesc.sheet.expressionsPerSpawnEventAttribute != null)
        {
            sb.AppendLine($"    PerSpawnEventAttribute Count: {assetDesc.sheet.expressionsPerSpawnEventAttribute.Length}");
            for (int i = 0; i < assetDesc.sheet.expressionsPerSpawnEventAttribute.Length; i++)
            {
                var expr = assetDesc.sheet.expressionsPerSpawnEventAttribute[i];
                sb.AppendLine($"      Expression {i}: {expr.op}");
            }
        }
        else
        {
            sb.AppendLine("    PerSpawnEventAttribute: null");
        }

        if (assetDesc.sheet.values != null)
        {
            sb.AppendLine($"    Values Count: {assetDesc.sheet.values.Length}");
            for (int i = 0; i < assetDesc.sheet.values.Length; i++)
            {
                var value = assetDesc.sheet.values[i];
                sb.AppendLine($"      Value {i}: {value} - {value.expressionIndex}");
            }
        }
        else
        {
            sb.AppendLine("    Values: null");
        }

        if (assetDesc.sheet.exposed != null)
        {
            sb.AppendLine($"    Exposed Values Count: {assetDesc.sheet.exposed.Length}");
            for (int i = 0; i < assetDesc.sheet.exposed.Length; i++)
            {
                var value = assetDesc.sheet.exposed[i];
                sb.AppendLine($"      Exposed Value {i}: {value.mapping}");
            }
        }


        if (assetDesc.systemDesc != null)
        {
            sb.AppendLine("  System Descriptions:");
            for (int i = 0; i < assetDesc.systemDesc.Length; i++)
            {
                var systemDesc = assetDesc.systemDesc[i];
                sb.AppendLine($"    System {i}:");
                sb.AppendLine($"      Name: {systemDesc.name}");
                sb.AppendLine($"      Type: {systemDesc.type}");
                sb.AppendLine($"      Flags: {systemDesc.flags}");
                sb.AppendLine($"      Layer: {systemDesc.layer}");

                if (systemDesc.buffers != null)
                {
                    sb.AppendLine($"      Buffers Count: {systemDesc.buffers.Length}");
                    for (int j = 0; j < systemDesc.buffers.Length; j++)
                    {
                        var buffer = systemDesc.buffers[j];
                        sb.AppendLine($"        Buffer {j}: Name={buffer.name}, Index={buffer.index}");
                    }
                }
                else
                {
                    sb.AppendLine("      Buffers: null");
                }

                if (systemDesc.values != null)
                {
                    sb.AppendLine($"      Values Count: {systemDesc.values.Length}");
                    for (int j = 0; j < systemDesc.values.Length; j++)
                    {
                        var value = systemDesc.values[j];
                        sb.AppendLine($"        Value {j}: Name={value.name}, Index={value.index}");
                    }
                }
                else
                {
                    sb.AppendLine("      Values: null");
                }

                if (systemDesc.tasks != null)
                {
                    sb.AppendLine($"      Tasks Count: {systemDesc.tasks.Length}");
                    for (int j = 0; j < systemDesc.tasks.Length; j++)
                    {
                        var task = systemDesc.tasks[j];
                        sb.AppendLine($"        Task {j}: Type={task.type}, ShaderSourceIndex={task.shaderSourceIndex}");
                        sb.AppendLine($"        Processor: {task.processor?.GetType().Name ?? "None"}");

                        //Add task buffers mapping
                        if(task.buffers != null)
                        {
                            sb.AppendLine($"          Task Buffers Count: {task.buffers.Length}");
                            for (int k = 0; k < task.buffers.Length; k++)
                            {
                                var buffer = task.buffers[k];
                                sb.AppendLine($"            Task Buffer {k}: Name={buffer.name}, Index={buffer.index}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("          Task Buffers: null");
                        }

                        if (task.values != null)
                        {
                            sb.AppendLine($"          Task Values Count: {task.values.Length}");
                            for (int k = 0; k < task.values.Length; k++)
                            {
                                var value = task.values[k];
                                sb.AppendLine($"            Task Value {k}: Name={value.name}, Index={value.index}");
                            }
                        }
                        else
                        {
                            sb.AppendLine("          Task Values: null");
                        }
                    }
                }
                else
                {
                    sb.AppendLine("      Tasks: null");
                }
            }
        }
        else
        {
            sb.AppendLine("  System Descriptions: null");
        }

        if (assetDesc.eventDesc != null)
        {
            sb.AppendLine("  Event Descriptions:");
            for (int i = 0; i < assetDesc.eventDesc.Length; i++)
            {
                var eventDesc = assetDesc.eventDesc[i];
                sb.AppendLine($"    Event {i}: Name={eventDesc.name}");
                sb.AppendLine($"      Init Systems Count: {eventDesc.initSystems?.Length ?? 0}");
                sb.AppendLine($"      Start Systems Count: {eventDesc.startSystems?.Length ?? 0}");
                sb.AppendLine($"      Stop Systems Count: {eventDesc.stopSystems?.Length ?? 0}");
            }
        }
        else
        {
            sb.AppendLine("  Event Descriptions: null");
        }

        if (assetDesc.gpuBufferDesc != null)
        {
            sb.AppendLine("  GPU Buffer Descriptions:");
            for (int i = 0; i < assetDesc.gpuBufferDesc.Length; i++)
            {
                var bufferDesc = assetDesc.gpuBufferDesc[i];
                sb.AppendLine($"    Buffer {i}: Target={bufferDesc.target}, Size={bufferDesc.size}, Capacity={bufferDesc.capacity}, Stride={bufferDesc.stride}");
                if (bufferDesc.layout != null && bufferDesc.layout.Length > 0)
                {
                    sb.AppendLine($"      Layout elements Count: {bufferDesc.layout.Length}");
                    foreach (var layoutDesc in bufferDesc.layout)
                    {
                        sb.AppendLine($"        Name={layoutDesc.name}, Type={layoutDesc.type}, Offset (bucket, structure, element) ={layoutDesc.offset.bucket}, {layoutDesc.offset.structure}, {layoutDesc.offset.element}");
                    }
                }
            }
        }
        else
        {
            sb.AppendLine("  GPU Buffer Descriptions: null");
        }

        if (assetDesc.cpuBufferDesc != null)
        {
            sb.AppendLine("  CPU Buffer Descriptions:");
            for (int i = 0; i < assetDesc.cpuBufferDesc.Length; i++)
            {
                var bufferDesc = assetDesc.cpuBufferDesc[i];
                sb.AppendLine($"    Buffer {i}: Capacity={bufferDesc.capacity}, Stride={bufferDesc.stride}");
                foreach (var layoutDesc in bufferDesc.layout)
                {
                    sb.AppendLine($"        Name={layoutDesc.name}, Type={layoutDesc.type}, Offset (bucket, structure, element) ={layoutDesc.offset.bucket}, {layoutDesc.offset.structure}, {layoutDesc.offset.element}");
                }
            }
        }
        else
        {
            sb.AppendLine("  CPU Buffer Descriptions: null");
        }

        if (assetDesc.temporaryBufferDesc != null)
        {
            sb.AppendLine("  Temporary GPU Buffer Descriptions:");
            for (int i = 0; i < assetDesc.temporaryBufferDesc.Length; i++)
            {
                var tempBufferDesc = assetDesc.temporaryBufferDesc[i];
                var bufferDesc = tempBufferDesc.desc;
                sb.AppendLine($"    Buffer {i}: FrameCount={tempBufferDesc.frameCount},Target={bufferDesc.target}, Size={bufferDesc.size}, Capacity={bufferDesc.capacity}, Stride={bufferDesc.stride}");
            }
        }
        else
        {
            sb.AppendLine("  Temporary GPU Buffer Descriptions: null");
        }

        if (assetDesc.shaderSourceDesc != null)
        {
            sb.AppendLine("  Shader Source Descriptions:");
            for (int i = 0; i < assetDesc.shaderSourceDesc.Length; i++)
            {
                var shaderDesc = assetDesc.shaderSourceDesc[i];
                sb.AppendLine($"    Shader {i}: Name={shaderDesc.name}, Compute={shaderDesc.compute}");
            }
        }
        else
        {
            sb.AppendLine("  Shader Source Descriptions: null");
        }

        sb.AppendLine($"  Renderer Settings: ShadowCastingMode={assetDesc.rendererSettings.shadowCastingMode}, MotionVectorGenerationMode={assetDesc.rendererSettings.motionVectorGenerationMode}");
        sb.AppendLine($"  Instancing Disabled Reason: {assetDesc.instancingDisabledReason}");

        return sb.ToString();
    }
}
