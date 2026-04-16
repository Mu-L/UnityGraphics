using System.Collections.Generic;
using Unity.GraphCommon.LowLevel.Editor;
using UnityEngine;

namespace UnityEditor.VFX
{
    /// <summary>
    /// Represents a task generated using a template and a list of snippets.
    /// </summary>
    /*public*/ class SpawnerTask : ITask
    {
        /// <summary>
        /// Gets the name of the template associated with the task.
        /// </summary>
        public string TemplateName { get; }
        public VFXTaskType SpawnerType { get; }

        IDataKey m_SpawnDataKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="TemplatedTask"/> class using the specified template name
        /// and task snippets.
        /// </summary>
        /// <param name="templateName">The name of the template associated with the task.</param>
        /// <param name="spawnerType">The spawner task type.</param>
        /// <param name="spawnDataKey"> The data key for the spawn data</param>
        public SpawnerTask(string templateName, VFXTaskType spawnerType, IDataKey spawnDataKey)
        {
            SpawnerType = spawnerType;
            TemplateName = templateName;
            m_SpawnDataKey = spawnDataKey;
        }

        /// <inheritdoc />
        public bool GetDataUsage(IDataKey dataKey, out DataPathSet readUsage, out DataPathSet writeUsage)
        {
            if (dataKey.Equals(m_SpawnDataKey))
            {
                readUsage = new DataPathSet();
                writeUsage = new DataPathSet();
                writeUsage.Add(DataPath.Empty);
                writeUsage.Add(new DataPath(SpawnData.SourceAttributeDataKey));
                return true;
            }
            readUsage = null;
            writeUsage = null;
            return false;
        }
    }
}
