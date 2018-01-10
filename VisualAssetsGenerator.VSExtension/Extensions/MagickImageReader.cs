using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;
using ImageMagick;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using Color = System.Windows.Media.Color;

namespace VisualAssetGenerator.Extensions
{
    internal class MagickImageReader
    {
        public static readonly IEnumerable<string> SupportedFormats;

        static MagickImageReader()
        {
            SupportedFormats = new[] { ".emf", ".eps", ".svg", ".wmf", ".xps" };
            var currentPackageDir = Path.GetDirectoryName(typeof(MagickImageReader).GetAssemblyLocalPath());
            var ghostscriptDirectory = Path.Combine(currentPackageDir, "Ghostscript");
            MagickNET.SetGhostscriptDirectory(ghostscriptDirectory);
        }

        public static async Task<Image> LoadAsync(string path, IEnumerable<IImageConstraint> constraints)
        {
            var stream = await LoadStreamAsync(path, constraints);

            return Image.FromStream(stream);
        }

        public static async Task<Stream> LoadStreamAsync(string path, IEnumerable<IImageConstraint> constraints = null)
        {
            var sizeConstraint = constraints?.OfType<SizeConstraint>().FirstOrDefault();

            if (".xps".Equals(Path.GetExtension(path), StringComparison.InvariantCultureIgnoreCase))
                return GetXpsStream(path, sizeConstraint);

            return await GetMagickStream(path, sizeConstraint);
        }

        private static async Task<Stream> GetMagickStream(string path, SizeConstraint sizeConstraint)
        {
            using (var mi = GetMagickImage(path, sizeConstraint?.Size))
            {
                var stream = new MemoryStream();
                mi.Write(stream);
                await stream.FlushAsync();
                stream.Position = 0;

                return stream;
            }
        }

        private static Stream GetXpsStream(string path, SizeConstraint sizeConstraint)
        {
            using (var xpsDoc = new XpsDocument(path, FileAccess.Read))
            {
                var docSeq = xpsDoc.GetFixedDocumentSequence();

                if (docSeq == null || docSeq.DocumentPaginator.PageCount == 0)
                    return null;

                var firstPage = docSeq.DocumentPaginator.GetPage(0);

                var size = sizeConstraint?.Size ?? new Size
                {
                    Height = (int) firstPage.Size.Height,
                    Width = (int) firstPage.Size.Width
                };

                var scaledVisual = GetScaledVisual(firstPage, size);

                var renderTarget = new RenderTargetBitmap(scaledVisual.Item2.Width, scaledVisual.Item2.Height, 96, 96, PixelFormats.Default);
                renderTarget.Render(scaledVisual.Item1);

                //var resizedBitmap = (sizeConstraint?.Size.Width != (int)firstPage.Size.Width 
                //                    || sizeConstraint.Size.Height != (int)firstPage.Size.Height)
                //                    && sizeConstraint != null
                //                    ? GetResizedBitmap(renderTarget, sizeConstraint.Size)
                //                    : renderTarget;


                var encoder = new PngBitmapEncoder
                {
                    Frames = {BitmapFrame.Create(renderTarget)}
                };

                var stream = new MemoryStream();
                encoder.Save(stream);
                stream.Flush();
                stream.Position = 0;
                return stream;
            }
        }

        private static Tuple<Visual, Size> GetScaledVisual(DocumentPage page, Size newSize)
        {
            var scaleWidth = newSize.Width / page.Size.Width;
            var scaleHeight = newSize.Height / page.Size.Height;
            var scale = Math.Min(scaleHeight, scaleWidth);
            
            var scaledSize = new Size((int)(page.Size.Width*scale),(int)(page.Size.Height*scale));

            var root = new ContainerVisual
            {
                Children = { page.Visual },
                Transform = new ScaleTransform(scale, scale)
            };

            return new Tuple<Visual, Size>(root, scaledSize);
        }

        private static BitmapSource GetResizedBitmap(BitmapSource renderTarget, Size size)
        {
            var scaleHeight = size.Height / (float)renderTarget.Height;
            var scaleWidth = size.Width / (float)renderTarget.Width;

            var scale = Math.Min(scaleHeight, scaleWidth);

            return new TransformedBitmap(renderTarget, new ScaleTransform(scale, scale));
        }

        private static MagickImage GetMagickImage(string filePath, Size? size)
        {
            var readSettings = new MagickReadSettings
            {
                BackgroundColor = MagickColors.Transparent
            };

            if (size?.IsEmpty == false)
            {
                readSettings.Width = size.Value.Width;
                readSettings.Height = size.Value.Height;
            }

            var magickImage = new MagickImage(new FileInfo(filePath), readSettings)
            {
                Format = MagickFormat.Png,
                Quality = 100
            };

            return magickImage;
        }        
    }    
}
