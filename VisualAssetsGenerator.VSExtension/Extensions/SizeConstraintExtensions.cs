using System;
using System.Reflection;
using System.Windows;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using VisualAssetGenerator.Model;

namespace VisualAssetGenerator.Extensions
{
    internal static class SizeConstraintExtensions
    {
        private static readonly FieldInfo PaddingField = typeof(SizeConstraint).GetField($"<{nameof(SizeConstraint.Padding)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void UpdatePadding(this SizeConstraint constraint, double horizontalContentFraction,
            double verticalContentFraction)
        {
            if (horizontalContentFraction < 0.0 || horizontalContentFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(horizontalContentFraction));
            if (verticalContentFraction < 0.0 || verticalContentFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(verticalContentFraction));

            var contentWidth = constraint.Size.Width * horizontalContentFraction;
            var paddingWidth = (int) Math.Round((constraint.Size.Width - contentWidth) / 2.0);
            //var contentHeight = constraint.Size.Height * verticalContentFraction;
            //var paddingHeight = (int) Math.Round((constraint.Size.Height - contentHeight) / 2.0);

            PaddingField.SetValue(constraint, new Thickness(paddingWidth, 0, paddingWidth, 0));
        }

        public static void UpdatePadding(this SizeConstraint constraint, SizeConstraintData data)
        {
            UpdatePadding(constraint, (double)data.ContentWidth / 100, (double)data.ContentWidth / 100);
        }
    }
}