using System;

namespace VisualAssetGenerator.Extensions
{
    public static class AssemblyExtensions
    {
        public static string GetAssemblyLocalPath(this Type type)
        {
            var codebase = type.Assembly.CodeBase;
            var uri = new Uri(codebase, UriKind.Absolute);
            return uri.LocalPath;
        }
    }
}
