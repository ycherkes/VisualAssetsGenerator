using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace VisualAssetGenerator
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = false)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)] // Info on this package for Help/About
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(UIContextGuids.NoSolution, PackageAutoLoadFlags.None)]
    [Guid(PackageGuidString)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1650:ElementDocumentationMustBeSpelledCorrectly", Justification = "pkgdef, VS and vsixmanifest are valid VS terms")]
    public sealed class VisualAssetsGeneratorPackage : Package
    {
        /// <summary>
        /// SvgConverterToolWindowPackage GUID string.
        /// </summary>
        public const string PackageGuidString = "42FBAF77-485E-4700-8357-A066BE1DF84C";
        private WindowActivationWatcher _windowActivationWatcher;


        protected override void Initialize()
        {
            var monSel = (IVsMonitorSelection)GetService(typeof(SVsShellMonitorSelection));
            _windowActivationWatcher = new WindowActivationWatcher(monSel);
            base.Initialize();
        }

        //protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        //{
        //    await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
        //    var monSel = (IVsMonitorSelection)await GetServiceAsync(typeof(SVsShellMonitorSelection));
        //    _windowActivationWatcher = new WindowActivationWatcher(monSel);
        //    await base.InitializeAsync(cancellationToken, progress);
        //}
    }
}
