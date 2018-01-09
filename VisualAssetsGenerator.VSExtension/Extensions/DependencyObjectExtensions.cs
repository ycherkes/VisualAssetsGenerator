using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace VisualAssetGenerator.Extensions
{
    public static class DependencyObjectExtensions
    {
        public static IEnumerable<T> FindVisualChildren<T>(this DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); ++i)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T variable)
                    yield return variable;
                foreach (var visualChild in child.FindVisualChildren<T>())
                    yield return visualChild;
            }
        }

        public static T FindVisualAncestor<T>(this DependencyObject dependencyObject) where T : class
        {
            var reference = dependencyObject;
            do
            {
                reference = VisualTreeHelper.GetParent(reference);
            }
            while (reference != null && !(reference is T));
            return reference as T;
        }

    }
}
