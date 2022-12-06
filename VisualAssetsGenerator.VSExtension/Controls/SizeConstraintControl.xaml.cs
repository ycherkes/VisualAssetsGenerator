using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VisualAssetGenerator.Model;

namespace VisualAssetGenerator.Controls
{
    /// <summary>
    /// Interaction logic for SizeConstraintControl.xaml
    /// </summary>
    public partial class SizeConstraintControl : UserControl
    {
        private readonly WritableSettingsStore _settingsStore;
        private bool _isLoading;
        public virtual event NotifyCollectionChangedEventHandler DataChanged;

        public SizeConstraintControl()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            InitializeComponent();
            ViewModel.CollectionChanged += CollectionChanged;
            lbContentFraction.PreviewMouseWheel += OnPreviewMouseWheel;
            var settingsManager = new ShellSettingsManager(ServiceProvider.GlobalProvider);
            _settingsStore = settingsManager.GetWritableSettingsStore(SettingsScope.UserSettings);
        }

        public void LoadData()
        {
            if (!_settingsStore.CollectionExists("External Tools"))
                _settingsStore.CreateCollection("External Tools");

            var serializer = new JavaScriptSerializer();
            var defaultValue = serializer.Serialize(ViewModel);

            var serializedModel = _settingsStore.GetString("External Tools", "UWPAssetGenerator-SizeConstraints", defaultValue);


            _isLoading = true;

            var deserializedModel = serializer.Deserialize<IEnumerable<SizeConstraintData>>(serializedModel);

            ViewModel.Load(deserializedModel);

            _isLoading = false;
        }

        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            e.Handled = true;
            var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent,
                Source = sender
            };
            var parent = ((Control)sender).Parent as UIElement;
            parent?.RaiseEvent(eventArg);
        }

        private void CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            DataChanged?.Invoke(sender, e);

            if (_isLoading) return;

            var serializer = new JavaScriptSerializer();
            var serializedValue = serializer.Serialize(ViewModel);

            _settingsStore.SetString("External Tools", "UWPAssetGenerator-SizeConstraints", serializedValue);
        }

        private void ButtonSetDefaultValues_OnClick(object sender, RoutedEventArgs e)
        {
            ViewModel.Reset((DataContext as ICollectionView)?.Filter);
        }

        internal void ApplyFilter(Predicate<SizeConstraintData> predicate)
        {
            var source = CollectionViewSource.GetDefaultView(ViewModel);
            source.Filter = x => predicate((SizeConstraintData)x);
            DataContext = source;
            source.Refresh();
        }
    }
}
