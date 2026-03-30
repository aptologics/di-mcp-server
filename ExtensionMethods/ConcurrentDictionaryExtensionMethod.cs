using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Reflection;

namespace DIMCPServer.ExtensionMethods
{
    public static class ConcurrentDictionaryExtensionMethod
    {
        public static void PopulateToolMethodMap<T>(this ConcurrentDictionary<string, MethodInfo[]> toolMethodMap, string category)
        {
            var toolInterfaceMethods = GetToolMethodsForType<T>();
            
            toolMethodMap.TryAdd(category, toolInterfaceMethods);
        }

        public static void PopulatePromptMethodMap<T>(this ConcurrentDictionary<string, MethodInfo[]> promptMethodMap, string category)
        {
            var promptMethods = GetPromptMethodsForType(typeof(T));
            
            promptMethodMap.TryAdd(category, promptMethods);
        }

        /// <summary>
        /// Gets MCP tool methods for a specific type using reflection.
        /// </summary>
        public static MethodInfo[] GetToolMethodsForType<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>()
        {
            return typeof(T)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length != 0)
                .ToArray();
        }

        /// <summary>
        /// Gets MCP prompt methods for a specific type using reflection.
        /// </summary>
        public static MethodInfo[] GetPromptMethodsForType(
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] Type promptType)
        {
            return promptType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.GetCustomAttributes(typeof(McpServerPromptAttribute), false).Length != 0)
                .ToArray();
        }
    }
}
