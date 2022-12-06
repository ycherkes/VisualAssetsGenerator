using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System;
using System.IO;

namespace VisualAssetGenerator.Extensions
{
    static class DteExtensions
    {
        public static string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            try
            {
                var solution = (ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as DTE2)?.Solution;
                if (!string.IsNullOrEmpty(solution?.FullName))
                {
                    return Path.GetDirectoryName(solution.FullName);
                }
            }
            catch (Exception) { }

            return null;
        }
    }
}
