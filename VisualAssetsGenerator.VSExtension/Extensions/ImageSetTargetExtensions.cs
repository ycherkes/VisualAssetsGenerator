using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using VisualAssetGenerator.Model;

namespace VisualAssetGenerator.Extensions
{
    static class ImageSetTargetExtensions
    {

        public static IEnumerable<ImageSetTarget> FindTargets(this ImageSetTarget root, SizeConstraintData constraint)
        {
            var result = root.Children
                             .Where(x => x.ImageType == constraint.ImageType)
                             .SelectMany(x => x.Children)
                             .Where(x => string.IsNullOrEmpty(constraint.QualifierName) 
                                         && x.Children.Count == 0
                                         || x.QualifierName == constraint.QualifierName)
                             .SelectMany(x => x.Children.Count == 0 ? new[] {x} : x.Children);

            return result;
        }

    }
}
