using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using VisualAssetGenerator.Model;

namespace VisualAssetGenerator.Extensions
{
    internal static class SizeConstraintExtensions
    {
        private static readonly FieldInfo PaddingField = typeof(SizeConstraint).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Single(x => x.FieldType == typeof(Thickness));

        public static void UpdatePadding(this SizeConstraint constraint, double horizontalContentFraction,
            double verticalContentFraction)
        {
            if (horizontalContentFraction < 0.0 || horizontalContentFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(horizontalContentFraction));
            if (verticalContentFraction < 0.0 || verticalContentFraction > 1.0)
                throw new ArgumentOutOfRangeException(nameof(verticalContentFraction));

            var contentWidth = constraint.Size.Width * horizontalContentFraction;
            var paddingWidth = (int) Math.Round((constraint.Size.Width - contentWidth) / 2.0);
            var contentHeight = constraint.Size.Height * verticalContentFraction;
            var paddingHeight = (int) Math.Round((constraint.Size.Height - contentHeight) / 2.0);

            PaddingField.SetValue(constraint, new Thickness(paddingWidth, paddingHeight, paddingWidth, paddingHeight));
        }

        public static void UpdatePadding(this SizeConstraint constraint, SizeConstraintData data)
        {
            UpdatePadding(constraint, (double)data.ContentFraction / 100, (double)data.ContentFraction / 100);
        }
    }
}