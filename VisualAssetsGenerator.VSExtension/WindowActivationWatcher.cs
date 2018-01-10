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
using Castle.DynamicProxy;
using EnvDTE;
using ExposedObject;
using Microsoft.Internal.VisualStudio.Shell;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.AppxManifestDesigner.Designer;
using Microsoft.VisualStudio.AppxManifestDesigner.Designer.ImageSet;
using Microsoft.VisualStudio.DesignTools.ImageSet;
using Microsoft.VisualStudio.DesignTools.ImageSet.Telemetry;
using Microsoft.VisualStudio.DesignTools.ImageSet.View;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VisualAssetGenerator.Extensions;
using VisualAssetGenerator.Model;
using Image = System.Drawing.Image;
using SizeConstraintControl = VisualAssetGenerator.Controls.SizeConstraintControl;


namespace VisualAssetGenerator
{
    // NOTE: This class will hook into the selection events and be responsible for monitoring selection changes
    internal sealed class WindowActivationWatcher : IVsSelectionEvents, IDisposable
    {
        private uint _monSelCookie;
        private IServiceProvider _serviceProvider;
        private static ImageSetTarget _imageSetTarget;
        private static SizeConstraintControl _sizeControl;
        private static ImageSetViewModel _imageSetViewModel;

        internal WindowActivationWatcher(IServiceProvider serviceProvider)
        {
            Validate.IsNotNull(serviceProvider, "serviceProvider");
            _serviceProvider = serviceProvider;

            var monSel = (IVsMonitorSelection) _serviceProvider.GetService(typeof(SVsShellMonitorSelection));
            // NOTE: We can ignore the return code here as there really isn't anything reasonable we could do to deal with failure, 
            // and it is essentially a no-fail method.
            monSel?.AdviseSelectionEvents(this, out _monSelCookie);
        }

        public void Dispose()
        {
            if (_monSelCookie != 0U && _serviceProvider != null)
            {
                var monSel = (IVsMonitorSelection) _serviceProvider.GetService(typeof(SVsShellMonitorSelection));
                if (monSel != null)
                {
                    // NOTE: We can ignore the return code here as there really isn't anything reasonable we could do to deal with failure, 
                    // and it is essentially a no-fail method.
                    monSel.UnadviseSelectionEvents(_monSelCookie);
                    _monSelCookie = 0U;
                }
            }

            _serviceProvider = null;
        }

        public int OnElementValueChanged(uint elementid, object varValueOld, object varValueNew)
        {
            var elementId = (VSConstants.VSSELELEMID) elementid;
            if (elementId != VSConstants.VSSELELEMID.SEID_WindowFrame || varValueNew == null) return VSConstants.S_OK;

            // NOTE: We have a selection change to a non-null value, this means someone has switched the active document / toolwindow (or the shell has done
            // so automatically since they closed the previously active one).
            var windowFrame = (IVsWindowFrame) varValueNew;

            if (!ErrorHandler.Succeeded(windowFrame.GetProperty((int) __VSFPROPID.VSFPROPID_Type, out var untypedProperty))) return VSConstants.S_OK;
            var typedProperty = (FrameType) (int) untypedProperty;

            if (typedProperty != FrameType.Document) return VSConstants.S_OK;

            if (!ErrorHandler.Succeeded(windowFrame.GetProperty((int) __VSFPROPID.VSFPROPID_pszMkDocument, out untypedProperty))) return VSConstants.S_OK;

            //var docPath = (string)untypedProperty;

            if (!ErrorHandler.Succeeded(windowFrame.GetProperty((int) __VSFPROPID.VSFPROPID_Caption, out untypedProperty))) return VSConstants.S_OK;

            var caption = (string) untypedProperty;

            if ("Package.appxmanifest".Equals(caption, StringComparison.CurrentCultureIgnoreCase))
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
            var dte = (DTE) ServiceProvider.GlobalProvider.GetService(typeof(DTE));

            if (dte.ActiveDocument == null) return;

            var uiObject = Exposed.From(((dynamic) dte.ActiveDocument).ActiveWindow.Object).EditorControl;

            var mduc = (ManifestDesignerUserControlProxy) uiObject;

            if (!mduc.IsLoading)
            {
                Mduc_Loaded(mduc);
                return;
            }

            var pd = DependencyPropertyDescriptor.FromProperty(ManifestDesignerUserControlProxy.IsLoadingProperty, typeof(ManifestDesignerUserControlProxy));
            pd.AddValueChanged(mduc, Mduc_Loaded);
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
            var manifestDesignerUserControl = (ManifestDesignerUserControl) exposed.contentPresenter.Content;

            if (Exposed.From(manifestDesignerUserControl).imageSetModel == null)
            {
                manifestDesignerUserControl.Loaded += ManifestDesignerUserControl_Loaded;
                return;
            }

            ManifestDesignerUserControl_Loaded(manifestDesignerUserControl);
        }

        private static void ManifestDesignerUserControl_Loaded(object sender, RoutedEventArgs e)
        {
            ((ManifestDesignerUserControl) sender).Loaded -= ManifestDesignerUserControl_Loaded;

            ManifestDesignerUserControl_Loaded(sender);
        }

        private static void ManifestDesignerUserControl_Loaded(object sender)
        {
            var manifestDesignerUserControl = (ManifestDesignerUserControl) sender;

            var imageSetViewModel = _imageSetViewModel = (ImageSetViewModel)Exposed.From(manifestDesignerUserControl).imageSetViewModel;

            //imageSetViewModel.PropertyChanged += ImageSetViewModel_PropertyChanged1;

            var visualAssetsControl = (VisualAssetsControl)Exposed.From(manifestDesignerUserControl).visualAssetsControl;
            if (!visualAssetsControl.IsLoaded)
            {
                visualAssetsControl.Loaded -=  VisualAssetsControl_Loaded;
                visualAssetsControl.Loaded += VisualAssetsControl_Loaded;
            }

            var imageSetModel = Exposed.From(manifestDesignerUserControl).imageSetModel;
            
            var imageSetTargetViewModels = (IList<ImageSetTargetViewModel>)imageSetViewModel.ImageTypeTargets;

            //var rootManager = Exposed.From(imageSetModel).rootManager;
            //Exposed.From(rootManager).Initialize();
            //var targetTreeFactory = Exposed.From(rootManager).requiredTreeFactory;
            _imageSetTarget = (ImageSetTarget)imageSetModel.Root;

            //UpdatePaddings(imageSetTarget, imageSetTargetViewModels, imageSetViewModel);
            //var res1 = imageSetViewModel.Assets.FindTargets(firstSizeConstraint);

            //imageSetTargetViewModels.SelectMany(x => x. )

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

            var filePicker = (IFilePicker) Exposed.From(imageSetModel).filePicker;

            if (filePicker.GetType().Name == "IFilePickerProxy") return;

            //foreach (var vm in imageSetTargetViewModels)
            //{
            //    vm.PropertyChanged -= Vm_PropertyChanged;
            //    vm.PropertyChanged += Vm_PropertyChanged;
            //}

            imageSetTargetViewModels.Single(x => x.ImageType == null).PropertyChanged += ImageSetTargetViewModel_PropertyChanged;
            imageSetViewModel.PropertyChanged += ImageSetViewModel_PropertyChanged;

            var proxy = (IFilePicker) new ProxyGenerator().CreateInterfaceProxyWithTarget(typeof(IFilePicker),
                                                                                          filePicker,
                                                                                          new DialogFilterInterceptor());

            var filePickerField = typeof(ImageSetModel).GetField("filePicker", BindingFlags.Instance | BindingFlags.NonPublic);

            if(filePickerField == null) return;

            filePickerField.SetValue(imageSetModel, proxy);

            var imageReaderFactory = Exposed.From(imageGenerator).imageReaderFactory;
            //var imageReaderFactoryType = Exposed.From(imageReaderFactory.GetType());
            //var supportdedVectorImageExtensions = (ICollection<string>)imageReaderFactoryType.SupportedVectorImageExtensions;

            //var toAddInSupported = MagickImageReader.SupportedFormats
            //                                        .Except(supportdedVectorImageExtensions)
            //                                        .ToArray();

            //foreach (var format in toAddInSupported)
            //{
            //    supportdedVectorImageExtensions.Add(format);
            //}

            if (!(Exposed.From(imageReaderFactory).imageReaders is IDictionary readers)) return;

            //foreach (var reader in (from readerKey in readers.Keys.OfType<string>().Select((x, i) => new { x, i })
            //                        join readerValues in readers.Values.OfType<object>().Select((x, i) => new { x, i })
            //                        on readerKey.i equals readerValues.i
            //                        where MagickImageReader.SupportedFormats.Contains(readerKey.x) && readerValues.x.GetType().Name != "IImageReaderProxy"
            //                        select readerKey.x).ToArray())
            //{
            //    readers.Remove(reader);
            //}

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
                                                     type);

                foreach (var format in formatsToAdd)
                {
                    readers.Add(format, wrapper);
                }
            }

            var imageReaderFactoryField = Exposed.From(imageSetTargetViewModels.First()).previewImageLoader
                                                                                        ?.GetType()
                                                                                        .GetField("imageReaderFactory", BindingFlags.Instance | BindingFlags.NonPublic);

            if(imageReaderFactoryField == null) return;

            foreach (var imageSetTargetViewModel in imageSetTargetViewModels)
            {
                var exposedModel = Exposed.From(imageSetTargetViewModel);
                var previewImageLoader = exposedModel.previewImageLoader;
                imageReaderFactoryField.SetValue(previewImageLoader, imageReaderFactory);
                if (!string.IsNullOrEmpty(imageSetTargetViewModel.SourceText)
                    && formatsToAdd.Any(x => x.Equals(Path.GetExtension(imageSetTargetViewModel.SourceText), StringComparison.InvariantCultureIgnoreCase))
                    && imageSetTargetViewModel.SourceImage == null)
                {
                    var text = imageSetTargetViewModel.SourceText;
                    imageSetTargetViewModel.SourceText = null;
                    imageSetTargetViewModel.SourceText = text;
                    manifestDesignerUserControl.Dispatcher.InvokeAsync(() => exposedModel.PropertyChanged("SourceText"));
                    //manifestDesignerUserControl.Dispatcher.InvokeAsync(() => exposedModel.UpdateImagePreviewAsync());
                }
            }
        }

        //private static void ImageSetViewModel_PropertyChanged1(object sender, PropertyChangedEventArgs e)
        //{
            
        //}

        //private static void Vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        //{
        //    if (e.PropertyName == "SourceImage")
        //    {
        //        //var model = Exposed.From(sender);
        //        //((ImageSetTarget)model.ImageSetTarget).Source = new ImageSetSource(model.SourceText);
        //    }
        //}

        private static void ImageSetViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName != nameof(ImageSetViewModel.SelectedImageTypeTarget)) return;

            ApplyFilter((ImageSetViewModel)sender);
        }

        private static void ApplyFilter(ImageSetViewModel sender)
        {
            var targetViewModel = sender?.SelectedImageTypeTarget;
            ApplyFilter(targetViewModel);
        }

        private static void ImageSetTargetViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if(e.PropertyName != nameof(ImageSetTargetViewModel.AssetsText)) return;
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
            var control = (VisualAssetsControl) sender;
            control.Loaded -= VisualAssetsControl_Loaded;

            var imageSetView = (ImageSetView) Exposed.From(control).ImageSetView;

            var assetGeneratorControl = (AssetGeneratorControl) Exposed.From(imageSetView).AssetGeneratorControl;

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

            var parent = (Grid) (generateButton?.Parent as FrameworkElement)?.Parent;

            if (parent == null) return;

            parent.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});
            parent.RowDefinitions.Add(new RowDefinition{Height = GridLength.Auto});

            //var expanderStyle = Application.Current.TryFindResource(typeof(Expander)) as Style;
            //var separatorStyle = Application.Current.TryFindResource(typeof(Separator)) as Style;

            _sizeControl = new SizeConstraintControl();

            var expander = control.FindVisualAncestor<Expander>();
            _sizeControl.ContentFractionExpander.Style = expander.Style;

            //_sizeControl.ContentFractionExpander.Style = expanderStyle;

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
            Document = 1,
//            ToolWindow = 2
        }
    }
}