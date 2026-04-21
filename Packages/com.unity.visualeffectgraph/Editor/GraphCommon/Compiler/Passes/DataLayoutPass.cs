using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphCommon.LowLevel.Editor
{
    class DataLayoutPass : CompilationPass
    {
        public bool Execute(ref CompilationContext context)
        {
            DataLayoutContainer dataLayoutContainer = context.data.GetOrCreate<DataLayoutContainer>();

            foreach (var dataContainer in context.graph.DataContainers)
            {
                if (dataContainer.RootDataView.DataDescription is UnorderedData or StructuredData)
                {
                    ConcreteLayout concreteLayout = GenerateBufferLayout(dataContainer.RootDataView.DataDescription);
                    dataLayoutContainer.TryAddLayout(dataContainer.Id, concreteLayout);
                }
            }

            foreach (var dataView in context.graph.DataViews)
            {
                if (dataView.DataDescription is UnorderedData unorderedData)
                {
                    //if Unordered, override dataContainer.RootDataView.DataDescription with a StructuredData.
                    //Unordered is treated like an abstract data description that can be converted to a structured one with the right layout.

                    StructuredData structuredData = new StructuredData();
                    foreach (var (key, data) in unorderedData.SubDatas)
                    {
                        structuredData.AddSubdata(key, data);
                    }
                    context.graph.OverrideDataDescription(dataView.Id, structuredData);
                }
            }
            return true;
        }

        ConcreteLayout GenerateBufferLayout(IDataDescription dataDescription)
        {
            ConcreteLayout concreteLayout;
            switch (dataDescription)
            {
                case UnorderedData unorderedData:
                    concreteLayout = ConcreteLayout.FromUnordered(GenerateChildLayouts(unorderedData.SubDataDescriptions));
                    break;
                case StructuredData structuredData:
                    concreteLayout = ConcreteLayout.FromStructured(GenerateChildLayouts(structuredData.SubDataDescriptions));
                    break;
                case ValueData valueData:
                    concreteLayout = new ConcreteLayout(valueData);
                    break;
                default:
                    // If the data description is of an unknown type, assert with a message and return null
                    Debug.Assert(false, $"Unknown data description type for buffer layout: {dataDescription.GetType()}");
                    return null;
            }
            return concreteLayout;
        }

        List<ConcreteLayout> GenerateChildLayouts(IEnumerable<IDataDescription> children)
        {
            var layouts = new List<ConcreteLayout>();
            foreach (var child in children)
                layouts.Add(GenerateBufferLayout(child));
            return layouts;
        }
    }

    class ConcreteLayout
    {
        int Size { get; set; }
        Dictionary<ValueData, int> m_ValueDataOffsets = new();

        public ConcreteLayout(ValueData valueData)
        {
            m_ValueDataOffsets.Add(valueData, 0);
            Size = DataLayoutHelper.ValueSize(valueData);
        }

        public ConcreteLayout()
        {
            Size = 0;
        }

        public static ConcreteLayout FromStructured(List<ConcreteLayout> children)
        {
            var layout = new ConcreteLayout();
            foreach (var child in children)
                layout.AppendSubLayout(child);
            return layout;
        }

        public static ConcreteLayout FromUnordered(List<ConcreteLayout> children)
        {
            // Bucket-pack children into 4-word aligned buckets
            var buckets = new List<List<ConcreteLayout>>();
            var bucketSizes = new List<int>();

            foreach (var child in children)
            {
                bool added = false;
                for (int i = 0; i < buckets.Count; i++)
                {
                    if (bucketSizes[i] + child.Size <= 4 /*kAlignment*/)
                    {
                        buckets[i].Add(child);
                        bucketSizes[i] += child.Size;
                        added = true;
                        break;
                    }
                }
                if (!added)
                {
                    buckets.Add(new List<ConcreteLayout> { child });
                    bucketSizes.Add(child.Size);
                }
            }

            var layout = new ConcreteLayout();
            foreach (var bucket in buckets)
            {
                foreach (var child in bucket)
                    layout.AppendSubLayout(child);
                layout.PadSize();
            }
            return layout;
        }

        void AppendSubLayout(ConcreteLayout subLayout)
        {
            int currentSize = Size;
            foreach (var kvp in subLayout.m_ValueDataOffsets)
            {
                m_ValueDataOffsets.Add(kvp.Key, kvp.Value + currentSize);
            }
            Size += subLayout.Size;
        }

        void PadSize()
        {
            if(Size % 4 != 0)
            {
                Size += 4 - (Size % 4);
            }
        }

        public override string ToString()
        {
            string result = $"ConcreteLayout(Size: {Size}, ValueDataOffsets: {{";
            foreach (var kvp in m_ValueDataOffsets)
                result += $"{kvp.Key.Type.Name}: {kvp.Value}, ";
            result += "})";
            return result;
        }

        public uint GetBufferSize()
        {
            return (uint)Size;
        }

        public int GetValueOffset(ValueData valueData)
        {
            if (m_ValueDataOffsets.Count == 0)
            {
                throw new Exception("Value data offsets have not been initialized.");
            }
            return m_ValueDataOffsets.GetValueOrDefault(valueData, -1);
        }
    }

    class DataLayoutContainer
    {
        Dictionary<DataContainerId, ConcreteLayout> m_DataValueLayouts = new();

        internal bool TryGetLayout(DataContainerId dataContainerId, out ConcreteLayout layout)
        {
            return m_DataValueLayouts.TryGetValue(dataContainerId, out layout);
        }

        public bool TryAddLayout(DataContainerId dataContainerId, ConcreteLayout concreteLayout)
        {
            return m_DataValueLayouts.TryAdd(dataContainerId, concreteLayout);
        }
    }

    static class DataLayoutHelper
    {
        public static int ValueSize(ValueData valueData)
        {
            return System.Runtime.InteropServices.Marshal.SizeOf(valueData.Type) / sizeof(uint);
        }
    }
}
