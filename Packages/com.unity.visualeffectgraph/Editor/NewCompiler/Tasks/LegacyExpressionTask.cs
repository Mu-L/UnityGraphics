using Unity.GraphCommon.LowLevel.Editor;

namespace UnityEditor.VFX
{
    class LegacyExpressionTask : ITask
    {
        public VFXExpression Expression { get; private set; }
        public static UniqueDataKey Value { get; } = new("Out");

        public LegacyExpressionTask(VFXExpression expression)
        {
            Expression = expression;
        }

        public bool GetDataUsage(IDataKey dataKey, out DataPathSet readUsage, out DataPathSet writeUsage)
        {
            if (dataKey is IndexDataKey indexDataKey)
            {
                if (indexDataKey.Index < Expression.parents.Length)
                {
                    readUsage = new DataPathSet();
                    writeUsage = new DataPathSet();
                    readUsage.Add(DataPath.Empty);
                    return true;
                }
            }

            if (dataKey == Value)
            {
                readUsage = new DataPathSet();
                writeUsage = new DataPathSet();
                writeUsage.Add(DataPath.Empty);
                return true;
            }

            readUsage = null;
            writeUsage = null;
            return false;
        }
    }
}
