# [![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7&style=for-the-badge)](https://stand-with-ukraine.pp.ua) [Stand with the people of Ukraine: How to Help](https://stand-with-ukraine.pp.ua)

<img src="https://yevhencherkes.gallerycdn.vsassets.io/extensions/yevhencherkes/assetgeneratorextended/0.86/1616275157709/Microsoft.VisualStudio.Services.Icons.Default" width="100" height="100" />

# Visual Assets Generator

[![Marketplace](https://img.shields.io/visual-studio-marketplace/v/YevhenCherkes.AssetGeneratorExtended.svg?label=VS%20marketplace&style=for-the-badge)](https://marketplace.visualstudio.com/items?itemName=YevhenCherkes.AssetGeneratorExtended)
[![Downloads](https://img.shields.io/visual-studio-marketplace/d/YevhenCherkes.AssetGeneratorExtended?label=VS%20downloads&style=for-the-badge)](https://marketplace.visualstudio.com/items?itemName=YevhenCherkes.AssetGeneratorExtended)

Enriches builtin Visual Studio Assets Generator by additional vector image formats (**emf, eps, psd, svg, wmf, xps**) and ability to override recommended padding (see the **"Content Width"** section in the image below).

Inspired by [UWP Visual Assets Generator by Peter_R_](https://marketplace.visualstudio.com/items?itemName=PeterR.UWPVisualAssetsGenerator)

![AssetGeneratorExtensionDemo](https://user-images.githubusercontent.com/13467759/205864968-8e332b14-6708-4e74-b7ac-2c1e89a23f17.png)

**Known Issues:**

This tool uses [Magick.NET](https://github.com/dlemstra/Magick.NET) for rendering SVG files.
Sometimes it renders SVG incorrectly, and it can be fixed by inlining CSS styles with [Svg For UWP Converter](https://marketplace.visualstudio.com/items?itemName=YevhenCherkes.svgforuwpextension).

**Privacy Notice:** No personal data is collected at all.

This tool has been working well for my own personal needs, but outside that its future depends on your feedback. Feel free to [open an issue](https://github.com/ycherkes/VisualAssetsGenerator/issues).

[![PayPal](https://img.shields.io/badge/Donate-PayPal-ffd700.svg?labelColor=0057b7&style=for-the-badge)](https://www.paypal.com/donate/?business=KXGF7CMW8Y8WJ&no_recurring=0&item_name=Help+Visual+Assets+Generator+become+better%21)

**Additional Resources:**

1. [Microsoft guidelines for App icons and logos](https://msdn.microsoft.com/en-us/windows/uwp/controls-and-patterns/tiles-and-notifications-app-assets)

2. [Magick.NET. The .NET library for ImageMagick](https://github.com/dlemstra/Magick.NET/tree/main/docs)

3. [Previous versions](https://github.com/ycherkes/VisualAssetsGenerator/releases)
