using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace MiniMappingway.Utility
{
    public static class ObjectTableExtensions
    {
        public static int FindIndexById(this IObjectTable table, ulong gameObjectId)
        {
            for (var index = 0; index < table.Length; index++)
            {
                if (table[index]?.GameObjectId == gameObjectId) return index;
            }
            return -1;
        }
    }
}
