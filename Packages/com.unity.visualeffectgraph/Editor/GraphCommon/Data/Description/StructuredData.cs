using System.Collections.Generic;

namespace Unity.GraphCommon.LowLevel.Editor
{
    /// <summary>
    /// Ordered data collection with elements of different types, referenced by any data identifier.
    /// </summary>
    /*public*/ class StructuredData : IDataDescription
    {
        Dictionary<IDataKey, int> m_DataIndirection = new();
        List<IDataDescription> m_Datas = new();

        /// <summary>
        /// Adds a data element, providing the data identifier and the data description.
        /// </summary>
        /// <param name="dataKey">The data identifier for this element.</param>
        /// <param name="data">The data description for this element.</param>
        /// <returns>True if the data element was added, false otherwise (for instance, if it was already present).</returns>
        public bool AddSubdata(IDataKey dataKey, IDataDescription data)
        {
            bool added = m_DataIndirection.TryAdd(dataKey, m_Datas.Count);
            if(added)
                m_Datas.Add(data);
            return added;
        }

        /// <inheritdoc cref="IDataDescription"/>
        public IDataDescription GetSubdata(IDataKey dataKey)
        {
            if(m_DataIndirection.TryGetValue(dataKey, out int index))
                return m_Datas[index];
            return null;
        }

        /// <summary>
        /// Enumerates all the subdata descriptions included in this data description, in order of addition.
        /// </summary>
        public IEnumerable<IDataDescription> SubDataDescriptions => m_Datas;
    }
}
