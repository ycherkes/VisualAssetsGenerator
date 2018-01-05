using System;
using System.Collections.Generic;
using System.Linq;
using VisualAssetGenerator.Utils;

namespace VisualAssetGenerator.Model
{
    internal class SizeConstraints: TrulyObservableCollection<SizeConstraintData>
    {
        private static readonly IList<SizeConstraintData> Data;

        static SizeConstraints()
        {
            Data = new[]
            {
                new SizeConstraintData
                {
                    ImageType = "SmallTile",
                    Title = "Small Tile",
                    ContentFraction = 50
                },
                new SizeConstraintData
                {
                    ImageType = "MediumTile",
                    Title = "Medium Tile",
                    ContentFraction = 33
                },
                new SizeConstraintData
                {
                    ImageType = "WideTile",
                    Title = "Wide Tile",
                    ContentFraction = 33
                },
                new SizeConstraintData
                {
                    ImageType = "LargeTile",
                    Title = "Large Tile",
                    ContentFraction = 33
                },
                new SizeConstraintData
                {
                    ImageType = "AppIcon",
                    Title = "App Icon",
                    ContentFraction = 75
                },
                new SizeConstraintData
                {
                    ImageType = "AppIcon",
                    Title = "Alternate App Icon",
                    QualifierName = "AlternateForm",
                    ContentFraction = 100
                },
                new SizeConstraintData
                {
                    ImageType = "SplashScreen",
                    Title = "Splash Screen",
                    ContentFraction = 33
                },
                new SizeConstraintData
                {

                    ImageType = "BadgeLogo",
                    Title = "Badge Logo",
                    ContentFraction = 100
                },
                new SizeConstraintData
                {
                    ImageType = "PackageLogo",
                    Title = "Package Logo",
                    ContentFraction = 100
                },
            };
        }

        public SizeConstraints()
            :base(Data.Select(x => x.Clone()))
        {}

        public void Reset(Predicate<SizeConstraintData> filter)
        {
            var  currStd = (from curr in this.Where(x => filter == null || filter(x)).Select(x => new { Constraint = x, Key = new {x.ImageType, x.QualifierName }})
                            join std in Data.Select(x => new { Constraint = x, Key = new {x.ImageType, x.QualifierName }}) 
                            on curr.Key equals std.Key
                            select new
                            {
                                Standard = std.Constraint,
                                Current = curr.Constraint
                            }).ToList();

            currStd.ForEach(x =>
            {
                x.Current.ContentFraction = x.Standard.ContentFraction;
            });
        }

        public void Load(IEnumerable<SizeConstraintData> data)
        {
            var currStd = (from curr in this.Select(x => new { Constraint = x, Key = new { x.ImageType, x.QualifierName } })
                join loaded in data.Select(x => new { Constraint = x, Key = new { x.ImageType, x.QualifierName } })
                    on curr.Key equals loaded.Key
                select new
                {
                    Loaded = loaded.Constraint,
                    Current = curr.Constraint
                }).ToList();

            currStd.Where(x => x.Current.ContentFraction != x.Loaded.ContentFraction).ToList().ForEach(x =>
            {
                x.Current.ContentFraction = x.Loaded.ContentFraction;
            });
        }
    }
}
