using Castle.DynamicProxy;
using EnvDTE;
using ExposedObject;
using Microsoft;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.AppxManifestDesigner.Designer;
using Microsoft.VisualStudio.AppxManifestDesigner.Designer.ImageSet;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using Microsoft.VisualStudio.DesignTools.ImageSet.View;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using VisualAssetGenerator.Extensions;
using VisualAssetGenerator.Model;
using Image = System.Drawing.Image;
using SizeConstraintControl = VisualAssetGenerator.Controls.SizeConstraintControl;
using Task = System.Threading.Tasks.Task;


namespace VisualAssetGenerator
{
    // NOTE: This class will hook into the selection events and be responsible for monitoring selection changes
    internal sealed class WindowActivationWatcher : IVsSelectionEvents, IDisposable
    {
        private IVsMonitorSelection _monitorSelection;
        private readonly uint _monSelCookie;
        private static ImageSetTarget _imageSetTarget;
        private static SizeConstraintControl _sizeControl;
        private static ImageSetViewModel _imageSetViewModel;

        internal WindowActivationWatcher(IVsMonitorSelection monitorSelection)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Validate.IsNotNull(monitorSelection, "monitorSelection");

            _monitorSelection = monitorSelection;

            _monitorSelection?.AdviseSelectionEvents(this, out _monSelCookie);
        }

        private Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var assemblies = new[]
            {
                "Microsoft.VisualStudio.AppxPackage",
                "Microsoft.VisualStudio.AppxManifestDesigner.UAP",
                "Microsoft.VisualStudio.DesignTools.ImageSet"
            }.ToHashSet();

            var assemblyName = args.Name.Split(',').First();

            if (!assemblies.Contains(assemblyName)) return null;

            return AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(x => x.GetName().Name == assemblyName);
        }

        public void Dispose()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (_monSelCookie != 0U && _monitorSelection != null)
            {
                _monitorSelection.UnadviseSelectionEvents(_monSelCookie);
            }

            _monitorSelection = null;

            AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomain_AssemblyResolve;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var elementId = (VSConstants.VSSELELEMID)elementid;
            if (elementId != VSConstants.VSSELELEMID.SEID_WindowFrame || varValueNew == null) return VSConstants.S_OK;

            // NOTE: We have a selection change to a non-null value, this means someone has switched the active document / toolwindow (or the shell has done
            // so automatically since they closed the previously active one).
            var windowFrame = (IVsWindowFrame)varValueNew;

            if (!ErrorHandler.Succeeded(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_Type, out var untypedProperty))) return VSConstants.S_OK;
            var typedProperty = (FrameType)(int)untypedProperty;

            if (typedProperty != FrameType.Document) return VSConstants.S_OK;

            if (!ErrorHandler.Succeeded(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_Caption, out untypedProperty))) return VSConstants.S_OK;

            var caption = (string)untypedProperty;

            //return VSConstants.S_OK;

            if ("Package.appxmanifest".Equals(caption, StringComparison.InvariantCultureIgnoreCase))
                RegisterVectorReader();

            return VSConstants.S_OK;
        }

        public int OnCmdUIContextChanged(uint dwCmdUICookie, int fActive)
        {
            // NOTE: We don't care about UI context changes like package loading, command visibility, etc.
            return VSConstants.S_OK;
        }

        public int OnSelectionChanged(
            IVsHierarchy pHierOld,
            uint itemidOld,
            IVsMultiItemSelect pMISOld,
            ISelectionContainer pSCOld,
            IVsHierarchy pHierNew,
            uint itemidNew,
            IVsMultiItemSelect pMISNew,
            ISelectionContainer pSCNew)
        {
            // NOTE: We don't care about selection changes like the solution explorer
            return VSConstants.S_OK;
        }

        private static void RegisterVectorReader()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = ServiceProvider.GlobalProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;

            Assumes.Present(dte);

            if (dte?.ActiveDocument == null) return;

            var uiObject = Exposed.From(((dynamic)dte.ActiveDocument).ActiveWindow.Object).Content;

            if (!uiObject.IsLoading)
            {
                Mduc_Loaded((object)uiObject);
                return;
            }

            var pd = DependencyPropertyDescriptor.FromProperty(ManifestDesignerUserControlProxy.IsLoadingProperty, typeof(ManifestDesignerUserControlProxy));
            pd.AddValueChanged((object)uiObject, Mduc_Loaded);
        }

        private static void Mduc_Loaded(object sender, EventArgs e)
        {
            var pd = DependencyPropertyDescriptor.FromProperty(ManifestDesignerUserControlProxy.IsLoadingProperty, typeof(ManifestDesignerUserControlProxy));
            pd.RemoveValueChanged(sender, Mduc_Loaded);

            Mduc_Loaded(sender);
        }

        private static void Mduc_Loaded(object sender)
        {
            var exposed = Exposed.From(sender);
            var manifestDesignerUserControl = (FrameworkElement)exposed.contentPresenter.Content;

            if (Exposed.From(manifestDesignerUserControl).imageSetModel == null)
            {
                manifestDesignerUserControl.Loaded += ManifestDesignerUserControl_Loaded;
                return;
            }

            _ = ManifestDesignerUserControl_LoadedAsync(manifestDesignerUserControl);
        }

        private static void ManifestDesignerUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)sender).Loaded -= ManifestDesignerUserControl_Loaded;

            if (Exposed.From(sender).AppxDocData?.HasLoaded != true)
            {
                ((IManifestDocDataInternal)Exposed.From(sender).AppxDocData).Loaded += ManifestDocData_Loaded(sender);
                return;
            }

            _ = ManifestDesignerUserControl_LoadedAsync(sender);
        }

        private static EventHandler ManifestDocData_Loaded(object sender)
        {
            return (s, _) => ManifestDocData_Loaded(s, sender);
        }

        private static void ManifestDocData_Loaded(object sender, object s)
        {
            ((IManifestDocDataInternal)sender).Loaded -= ManifestDocData_Loaded(s);

            _ = ManifestDesignerUserControl_LoadedAsync(s);
        }

        private static async Task ManifestDesignerUserControl_LoadedAsync(object sender)
        {
            var imageSetViewModel = _imageSetViewModel = (ImageSetViewModel)Exposed.From(sender).imageSetViewModel;

            if (imageSetViewModel == null) return;

            var visualAssetsControl = (FrameworkElement)Exposed.From(sender).visualAssetsControl;

            if (!visualAssetsControl.IsLoaded)
            {
                visualAssetsControl.Loaded -= VisualAssetsControl_Loaded;
                visualAssetsControl.Loaded += VisualAssetsControl_Loaded;
            }

            var imageSetModel = Exposed.From(sender).imageSetModel;

            var imageSetTargetViewModels = (IList<ImageSetTargetViewModel>)imageSetViewModel.ImageTypeTargets;

            _imageSetTarget = (ImageSetTarget)imageSetModel.Root;

            var imageGenerator = Exposed.From(imageSetModel).imageGenerator;

            if (imageGenerator.GetType().Name == "IImageGeneratorProxy") return;

            var imageGeneratorInterface = typeof(IImageConstraint).Assembly
                                                                  .GetTypes()
                                                                  .First(x => x.Name == "IImageGenerator");

            var imageGeneratorProxy = new ProxyGenerator().CreateInterfaceProxyWithTarget(imageGeneratorInterface,
                                                                                          imageGenerator,
                                                                                          new ImageGeneratorInterceptor());

            var imageGeneratorField = typeof(ImageSetModel).GetField("imageGenerator", BindingFlags.Instance | BindingFlags.NonPublic);

            imageGeneratorField.SetValue(imageSetModel, imageGeneratorProxy);

            var filePicker = (IFilePicker)Exposed.From(imageSetModel).filePicker;

            if (filePicker.GetType().Name == "IFilePickerProxy") return;

            imageSetTargetViewModels.Single(x => x.ImageType == null).PropertyChanged += ImageSetTargetViewModel_PropertyChanged;
            imageSetViewModel.PropertyChanged += ImageSetViewModel_PropertyChanged;

            var proxy = (IFilePicker)new ProxyGenerator().CreateInterfaceProxyWithTarget(typeof(IFilePicker),
                                                                                         filePicker,
                                                                                         new DialogFilterInterceptor());

            var filePickerField = typeof(ImageSetModel).GetField("filePicker", BindingFlags.Instance | BindingFlags.NonPublic);

            if (filePickerField == null) return;

            filePickerField.SetValue(imageSetModel, proxy);

            var imageReaderFactory = Exposed.From(imageGenerator).imageReaderFactory;

            if (!(Exposed.From(imageReaderFactory).imageReaders is IDictionary readers)) return;

            var formatsToAdd = MagickImageReader.SupportedFormats
                                                .Except(readers.Keys.OfType<string>())
                                                .ToArray();

            if (formatsToAdd.Any())
            {
                var type = typeof(IImageConstraint).Assembly
                                                   .GetTypes()
                                                   .First(x => x.Name == "IImageReader"); //.GetType($"{assembly.GetName().Name}.IImageReader");


                var wrapper = DelegateWrapper.WrapAs((Func<string, IEnumerable<IImageConstraint>, Task<Image>>)MagickImageReader.LoadAsync,
                                                     (Func<string, IEnumerable<IImageConstraint>, Task<Stream>>)MagickImageReader.LoadStreamAsync,
                                                     (Func<IEnumerable<string>>)(() => formatsToAdd),
                                                     new DelegateWrapper.MethodByNameAndEnumReturnType("get_ImageGraphicsType", "ImageGraphicsType", "Vector"),
                                                     type);

                foreach (var format in formatsToAdd)
                {
                    readers.Add(format, wrapper);
                }
            }

            var imageReaderFactoryField = Exposed.From(imageSetTargetViewModels.First()).previewImageLoader
                                                                                        ?.GetType()
                                                                                        .GetField("imageReaderFactory", BindingFlags.Instance | BindingFlags.NonPublic);

            if (imageReaderFactoryField == null) return;

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            foreach (var imageSetTargetViewModel in imageSetTargetViewModels)
            {
                var exposedModel = Exposed.From(imageSetTargetViewModel);
                var previewImageLoader = exposedModel.previewImageLoader;
                imageReaderFactoryField.SetValue(previewImageLoader, imageReaderFactory);
                if (!string.IsNullOrEmpty(exposedModel.SourceText)
                    && formatsToAdd.Any(x => x.Equals(Path.GetExtension(exposedModel.SourceText), StringComparison.InvariantCultureIgnoreCase))
                    && exposedModel.SourceImage == null)
                {
                    exposedModel.UpdateImagePreviewAsync();
                }
            }
        }

        private static void ImageSetViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ImageSetViewModel.SelectedImageTypeTarget)) return;

            ApplyFilter((ImageSetViewModel)sender);
        }

        private static void ApplyFilter(ImageSetViewModel sender)
        {
            var targetViewModel = sender?.SelectedImageTypeTarget;
            ApplyFilter(targetViewModel);
        }

        private static void ImageSetTargetViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(ImageSetTargetViewModel.AssetsText)) return;
            var model = sender as ImageSetTargetViewModel;
            ApplyFilter(model);
        }

        private static void ApplyFilter(ImageSetTargetViewModel model)
        {
            _sizeControl?.ApplyFilter(x => string.IsNullOrEmpty(model?.ImageType)
                                           && model?.Assets.Skip(1).Any(y => Eqs(y, x)) == true
                                            || x.ImageType == model?.ImageType);
        }

        private static bool Eqs(dynamic selectableItem, SizeConstraintData sizeConstraintData)
        {
            return selectableItem.IsSelected
                   && (selectableItem.ImageSetTargets as IEnumerable<ImageSetTarget>)?.Any(x => x.ImageType == sizeConstraintData.ImageType) == true;
        }

        private static void VisualAssetsControl_Loaded(object sender, RoutedEventArgs e)
        {
            var control = (VisualAssetsControl)sender;
            control.Loaded -= VisualAssetsControl_Loaded;

            var imageSetView = (ImageSetView)Exposed.From(control).ImageSetView;

            var assetGeneratorControl = (AssetGeneratorControl)Exposed.From(imageSetView).AssetGeneratorControl;

            if (!assetGeneratorControl.IsLoaded)
            {
                assetGeneratorControl.Loaded += AssetGeneratorControl_Loaded;
            }
        }

        private static void AssetGeneratorControl_Loaded(object sender, RoutedEventArgs e)
        {
            var control = (AssetGeneratorControl)sender;
            control.Loaded -= AssetGeneratorControl_Loaded;
            var generateButton = control.FindVisualChildren<Button>().FirstOrDefault(x => x.Name == "GenerateButton");

            var parent = (Grid)(generateButton?.Parent as FrameworkElement)?.Parent;

            if (parent == null) return;

            parent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            parent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _sizeControl = new SizeConstraintControl();

            var expander = control.FindVisualAncestor<Expander>();
            _sizeControl.ContentFractionExpander.Style = expander.Style;

            _sizeControl.DataChanged += SizeConstraints_CollectionChanged;

            _sizeControl.LoadData();

            _sizeControl.SetValue(Grid.RowProperty, 1);
            _sizeControl.SetValue(Grid.ColumnSpanProperty, 3);
            _sizeControl.Width = 550;
            _sizeControl.HorizontalAlignment = HorizontalAlignment.Left;
            _sizeControl.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            parent.Children.Add(_sizeControl);
            ApplyFilter(_imageSetViewModel);
        }

        private static void SizeConstraints_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (SizeConstraintData newItem in e.NewItems)
            {
                UpdatePaddings(_imageSetTarget, newItem);
            }
        }

        private static void UpdatePaddings(ImageSetTarget imageSetTarget, SizeConstraintData changedConstraint)
        {
            var targets = imageSetTarget.FindTargets(changedConstraint);

            foreach (var target in targets)
            {
                var sizeConstraint = target.Constraints.OfType<SizeConstraint>().ToArray();

                foreach (var constraint in sizeConstraint)
                {
                    constraint.UpdatePadding(changedConstraint);
                }
            }
        }

        // NOTE: Values obtained from https://docs.microsoft.com/en-us/dotnet/api/microsoft.visualstudio.shell.interop.__vsfpropid.vsfpropid_type, specifically the part for VSFPROPID_Type.
        private enum FrameType
        {
            Document = 1
        }
    }
}