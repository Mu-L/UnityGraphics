using System.Collections.Generic;

namespace Unity.GraphCommon.LowLevel.Editor
{
    /// <summary>
    /// Unordered data collection with elements of different types, referenced by any data identifier.
    /// </summary>
    /*public*/ class UnorderedData : IDataDescription
    {
        Dictionary<IDataKey, IDataDescription> m_DataKeyToDataDescriptions = new();
        Dictionary<IDataDescription, IDataKey> m_DataDescriptionToDataKey = new();

        /// <summary>
        /// Adds a data element, providing the data identifier and the data description.
        /// </summary>
        /// <param name="dataKey">The data identifier for this element.</param>
        /// <param name="data">The data description for this element.</param>
        /// <returns>True if the data element was added, false otherwise (for instance, if it was already present).</returns>
        public bool AddSubdata(IDataKey dataKey, IDataDescription data)
        {
            return m_DataKeyToDataDescriptions.TryAdd(dataKey, data) && m_DataDescriptionToDataKey.TryAdd(data, dataKey);
        }

        /// <inheritdoc cref="IDataDescription"/>
        public IDataDescription GetSubdata(IDataKey dataKey)
        {
            return m_DataKeyToDataDescriptions.GetValueOrDefault(dataKey);
        }

        /// <summary>
        /// Get the IDataKey associated with the sub data dataDescription.
        /// </summary>
        /// <param name="dataDescription">The sub data to query the key from.</param>
        /// <returns> The IDataKey associated with the sub data dataDescription if it exists, null otherwise.</returns>
        public IDataKey GetSubDataKey(IDataDescription dataDescription)
        {
            return m_DataDescriptionToDataKey.GetValueOrDefault(dataDescription);
        }

        /// <summary>
        /// Enumerates all the subdata descriptions included in this data description.
        /// </summary>
        public IEnumerable<IDataDescription> SubDataDescriptions => m_DataKeyToDataDescriptions.Values;

        /// <summary>
        /// Enumerates all the subdata descriptions included in this data description.
        /// </summary>
        public IEnumerable<KeyValuePair<IDataKey, IDataDescription>> SubDatas => m_DataKeyToDataDescriptions;

        /// <inheritdoc cref="IDataDescription"/>
        public bool IsCompatible(IDataDescription other)
        {
            return other is StructuredData or UnorderedData;
        }
    }
}

