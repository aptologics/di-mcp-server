using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Reflection;

namespace DI.MCP.Server.ExtensionMethods
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

        public static void PopulateResourceMethodMap<T>(this ConcurrentDictionary<string, MethodInfo[]> resourceMethodMap, string category)
        {
            var resourceMethods = GetResourceMethodsForType(typeof(T));

            resourceMethodMap.TryAdd(category, resourceMethods);
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

        /// <summary>
        /// Gets MCP resource methods for a specific type using reflection.
        /// Includes both static and instance methods to support all resource types.
        /// </summary>
        public static MethodInfo[] GetResourceMethodsForType(
            [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] Type resourceType)
        {
            return resourceType
                .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(McpServerResourceAttribute), false).Length != 0)
                .ToArray();
        }
    }
}
