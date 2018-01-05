using System;
using System.Diagnostics;
using System.Reflection;
using EnvDTE;

namespace VisualAssetGenerator.Extensions
{
    /// <summary>
    /// The <see cref="DebugHelper"/>
    /// class is used to help with debugging tasks for Visual Studio addin development.
    /// </summary>
    /// <remarks>
    /// This class was created by Rory Primrose for the 
    /// <a href="http://www.codeplex.com/NeovolveX" target="_blank">NeovolveX</a>
    /// project.
    /// </remarks>
    public static class DebugHelper
    {
        /// <summary>
        /// Identifies the internal object types.
        /// </summary>
        /// <param name="item">The item.</param>
        [Conditional("DEBUG")]
        public static void IdentifyInternalObjectTypes(UIHierarchyItem item)
        {
            if (item == null)
            {
                Debug.WriteLine("No item provided.");

                return;
            }

            if (item.Object == null)
            {
                Debug.WriteLine("No item object is available.");

                return;
            }

            // Loop through all the assemblies in the current app domain
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

            // Loop through each assembly
            foreach (var t in loadedAssemblies)
            {
                // Assume that the assembly to check against is EnvDTE.dll
                IdentifyInternalObjectTypes(item, t);
            }
        }

        /// <summary>
        /// Identifies the internal object types.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="assemblyToCheck">The assembly to check.</param>
        [Conditional("DEBUG")]
        public static void IdentifyInternalObjectTypes(UIHierarchyItem item, Assembly assemblyToCheck)
        {
            // Get the types that are publically available
            var exportedTypes = assemblyToCheck.GetExportedTypes();

            // Loop through each type
            foreach (var t in exportedTypes)
            {
                // Check if the object instance is of this type
                if (t.IsInstanceOfType(item.Object))
                {
                    Debug.WriteLine(t.FullName);
                }
            }
        }
    }
}
