using System.Diagnostics;
using System.Reflection;
using GeopoliticalMonitor.Application;
using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Events;
using GeopoliticalMonitor.Application.Firms;
using GeopoliticalMonitor.Application.Gdacs;
using GeopoliticalMonitor.Application.Preferences;
using GeopoliticalMonitor.Application.ReliefWeb;
using GeopoliticalMonitor.Application.Watchlists;
using GeopoliticalMonitor.Application.XBrowser;
using GeopoliticalMonitor.Infrastructure.ReliefWeb;
using GeopoliticalMonitor.Infrastructure.Persistence;
using GeopoliticalMonitor.Infrastructure.Gdacs;
using GeopoliticalMonitor.Infrastructure.Firms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

namespace GeopoliticalMonitor.Desktop;

public sealed partial class MainWindow : Window
{
    private const string PersistenceUnavailableMessage = "Local preference persistence is unavailable.";
    private const string EventDataUnavailableMessage = "Unable to load local event data.";

    private readonly SolidColorBrush splitterNormalBrush = new(Color.FromArgb(255, 21, 21, 21));
    private readonly SolidColorBrush splitterGlowTransparentBrush = new(Color.FromArgb(0, 255, 122, 24));
    private readonly XPostLinkCopyService postLinkCopyService = new(new WindowsClipboardWriter());
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private SolidColorBrush currentAccentBrush = new(Color.FromArgb(255, 255, 122, 24));
    private SolidColorBrush currentAccentTextBrush = new(Color.FromArgb(255, 255, 179, 71));
    private AccentThemePreferenceService? accentThemePreferenceService;
    private XPaneWidthPreferenceService? xPaneWidthPreferenceService;
    private XPaneSidePreferenceService? xPaneSidePreferenceService;
    private EventCatalogService? eventCatalogService;
    private EventSourceBridgeHandler? eventSourceBridgeHandler;
    private GdacsRefreshBridgeHandler? gdacsRefreshBridgeHandler;
    private HttpClient? gdacsHttpClient;
    private FirmsRefreshBridgeHandler? firmsRefreshBridgeHandler;
    private HttpClient? firmsHttpClient;
    private ReliefWebBridgeHandler? reliefWebBridgeHandler;
    private WatchlistBridgeHandler? watchlistBridgeHandler;
    private AlertBridgeHandler? alertBridgeHandler;
    private HttpClient? reliefWebHttpClient;
    private string? xBrowserUserDataFolder;
    private Task? xInitializationTask;
    private XBrowserRuntimeState xRuntimeState = XBrowserRuntimeState.NotStarted;
    private XDiagnosticAdBlockService? xProductionAdBlockService;
    private XProductionExtensionHostState xProductionExtensionHostState = XProductionExtensionHostState.XUnavailable;
    private string? xProductionExtensionStartupFailure;
    private bool xProductionRequiresTrustedReenrollment;
    private XBrowserExtensionCapabilityTestResult xExtensionCapabilityTest = XBrowserExtensionCapabilityTestResult.NotTested(GetWebView2SdkVersion(), GetAvailableWebView2RuntimeVersion());
    private XPaneSide xPaneSide = XPaneSide.Right;
    private string persistenceStatus = "SQLite local application data";
    private string? persistenceInitializationError;
    private double expandedXPaneWidth = XBrowserDefaults.DefaultPaneWidth;
    private bool persistenceInitialized;
    private bool xPaneWidthRestored;
    private bool isXPaneCollapsed;
    private bool isDraggingXPaneSplitter;
    private bool isSplitterPointerOver;

    private sealed record XExtensionDiagnosticSession(
        WebView2 WebView,
        XDiagnosticAdBlockService AdBlockService,
        string RuntimeVersion);

    private sealed record XExtensionDiagnosticTestRun(
        XBrowserExtensionCapabilityTestResult Result,
        XExtensionDiagnosticSession? Session);

    public MainWindow()
    {
        InitializeComponent();
        Title = ProductIdentity.DisplayName;
        Closed += OnClosed;
        XPaneSplitterLine.Background = splitterNormalBrush;
        XPaneSplitterGlow.Background = splitterGlowTransparentBrush;
        ApplyNativeAccent(AccentTheme.Orange);
        ApplyXPaneSide(XPaneSide.Right, notifyReact: false, updateWidth: false);
        UpdateXNavigationButtons();
        UpdateXPostLinkButton();
        _ = InitializeWebViewsAsync();
    }

    private async Task InitializeWebViewsAsync()
    {
        var pathProvider = new LocalApplicationDataPathProvider();
        InitializeFirmsProvider();
        await InitializePersistenceAsync(pathProvider);
        await RestoreXPaneSideAsync();
        await RestoreXPaneWidthAsync();
        await InitializeAppWebViewAsync(pathProvider.GetAppWebViewUserDataFolder());

        xBrowserUserDataFolder = pathProvider.GetXBrowserUserDataFolder();
        xInitializationTask = InitializeXWebViewAsync(xBrowserUserDataFolder);
        await xInitializationTask;
    }

    private void InitializeFirmsProvider()
    {
        firmsHttpClient = FirmsObservationFeed.CreateProductionHttpClient();
        var options = new FirmsClientOptions(
            Environment.GetEnvironmentVariable("GEOPOLITICALMONITOR_FIRMS_MAP_KEY", EnvironmentVariableTarget.Process));
        var feed = new FirmsObservationFeed(firmsHttpClient, new FirmsCsvParser(), options);
        firmsRefreshBridgeHandler = new FirmsRefreshBridgeHandler(new FirmsRefreshService(feed));
    }

    private async Task InitializeAppWebViewAsync(string userDataFolder)
    {
        Debug.Assert(!XWebViewEnvironmentPolicy.ForPrivilegedApp().BrowserExtensionsEnabled);
        var appEnvironment = await CreateDefaultWebViewEnvironmentAsync(userDataFolder).ConfigureAwait(true);
        await AppWebView.EnsureCoreWebView2Async(appEnvironment);

        var assetsPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        AppWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            PrivilegedWebViewSecurityPolicy.TrustedHost,
            assetsPath,
            CoreWebView2HostResourceAccessKind.DenyCors);

        AppWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;
        AppWebView.NavigationStarting += OnAppNavigationStarting;
        AppWebView.WebMessageReceived += OnWebMessageReceived;
        AppWebView.Source = PrivilegedWebViewSecurityPolicy.StartPage;
    }

    private async Task InitializeXWebViewAsync(string userDataFolder)
    {
        if (xRuntimeState == XBrowserRuntimeState.Initializing)
        {
            return;
        }

        xRuntimeState = XBrowserRuntimeState.Initializing;
        xProductionAdBlockService = null;
        xProductionExtensionHostState = XProductionExtensionHostState.XUnavailable;
        xProductionExtensionStartupFailure = null;
        xProductionRequiresTrustedReenrollment = false;
        var startupDecision = XWebViewEnvironmentPolicy.ForCurrentWinUi3Host();
        Debug.Assert(startupDecision.AttemptExtensionEnvironment);
        Debug.Assert(startupDecision.Environment.BrowserExtensionsEnabled);
        UpdateXNavigationButtons();
        UpdateXPostLinkButton();
        HideXError();

        try
        {
            var pathProvider = new LocalApplicationDataPathProvider();
            var managedSourcePath = pathProvider.GetProductionAdBlockManagedSourceDirectory();
            var sourceInspector = new FileXDiagnosticAdBlockSourceInspector();
            var payloadStore = new FileXAdBlockManagedPayloadStore(managedSourcePath, sourceInspector);
            var bindingStore = new FileXDiagnosticAdBlockBindingStore(pathProvider.GetProductionAdBlockBindingPath());
            var activationRecovered = await payloadStore.RecoverInterruptedActivationAsync().ConfigureAwait(true);
            var binding = await bindingStore.GetAsync().ConfigureAwait(true);
            XDiagnosticAdBlockSourceValidation? managedValidation = null;
            var managedPayloadExists = Directory.Exists(managedSourcePath);
            if (managedPayloadExists)
            {
                managedValidation = await sourceInspector.InspectAsync(managedSourcePath).ConfigureAwait(true);
            }

            var trustDecision = XProductionAdBlockStartupTrustPolicy.Evaluate(
                managedPayloadExists,
                bindingStore.HasPersistedState || !activationRecovered,
                binding,
                managedValidation);
            CoreWebView2Environment xEnvironment;
            if (!trustDecision.AttemptExtensionEnvironment)
            {
                xProductionRequiresTrustedReenrollment = true;
                xProductionExtensionHostState = XProductionExtensionHostState.FallbackUnavailable;
                xProductionExtensionStartupFailure = trustDecision.Message;
                xEnvironment = await CreateDefaultWebViewEnvironmentAsync(userDataFolder).ConfigureAwait(true);
            }
            else
            {
                try
                {
                    xEnvironment = await CreateExtensionEnabledXEnvironmentAsync(userDataFolder).ConfigureAwait(true);
                    xProductionExtensionHostState = XProductionExtensionHostState.Available;
                }
                catch (Exception ex)
                {
                    xProductionExtensionHostState = XProductionExtensionHostState.FallbackUnavailable;
                    xProductionExtensionStartupFailure = GetSafeDiagnosticExceptionMessage(ex, userDataFolder);
                    Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                        "Production X extension environment creation failed; using default environment",
                        ex,
                        userDataFolder));
                    xEnvironment = await CreateDefaultWebViewEnvironmentAsync(userDataFolder).ConfigureAwait(true);
                }
            }

            await XWebView.EnsureCoreWebView2Async(xEnvironment);
            ConfigureXCoreWebView();
            if (xProductionExtensionHostState == XProductionExtensionHostState.Available)
            {
                xProductionAdBlockService = new XDiagnosticAdBlockService(
                    new CoreWebView2ExtensionProfileAdapter(XWebView.CoreWebView2.Profile, XWebView.DispatcherQueue),
                    bindingStore,
                    sourceInspector,
                    XDiagnosticAdBlockBinding.ProductionXProfileScope,
                    managedSourcePath);
                await xProductionAdBlockService.SecureForAuthenticatedNavigationAsync().ConfigureAwait(true);
            }

            NavigateXHome();
            xRuntimeState = XBrowserRuntimeState.Ready;
            UpdateXNavigationButtons();
            UpdateXPostLinkButton();
        }
        catch (Exception ex)
        {
            xProductionAdBlockService = null;
            xProductionExtensionHostState = XProductionExtensionHostState.XUnavailable;
            xRuntimeState = XBrowserRuntimeState.Failed;
            Debug.WriteLine($"Known-good X WebView initialization failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
            ShowXError("X website pane could not initialize.");
            UpdateXNavigationButtons();
            UpdateXPostLinkButton();
        }
    }

    private static async Task<CoreWebView2Environment> CreateExtensionEnabledXEnvironmentAsync(string userDataFolder)
    {
        Directory.CreateDirectory(userDataFolder);
        var options = new CoreWebView2EnvironmentOptions
        {
            AreBrowserExtensionsEnabled = true,
        };
        var previousUserDataFolderOverride = Environment.GetEnvironmentVariable(
            "WEBVIEW2_USER_DATA_FOLDER",
            EnvironmentVariableTarget.Process);
        try
        {
            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                null,
                EnvironmentVariableTarget.Process);
            return await CoreWebView2Environment.CreateWithOptionsAsync(
                string.Empty,
                userDataFolder,
                options);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                previousUserDataFolderOverride,
                EnvironmentVariableTarget.Process);
        }
    }

    private void ConfigureXCoreWebView()
    {
        ApplyXSecuritySettings(XWebView.CoreWebView2.Settings);
        XWebView.CoreWebView2.NavigationStarting -= OnXNavigationStarting;
        XWebView.CoreWebView2.NavigationCompleted -= OnXNavigationCompleted;
        XWebView.CoreWebView2.SourceChanged -= OnXSourceChanged;
        XWebView.CoreWebView2.HistoryChanged -= OnXHistoryChanged;
        XWebView.CoreWebView2.NewWindowRequested -= OnXNewWindowRequested;
        XWebView.CoreWebView2.DownloadStarting -= OnXDownloadStarting;
        XWebView.CoreWebView2.ContextMenuRequested -= OnXContextMenuRequested;
        XWebView.CoreWebView2.NavigationStarting += OnXNavigationStarting;
        XWebView.CoreWebView2.NavigationCompleted += OnXNavigationCompleted;
        XWebView.CoreWebView2.SourceChanged += OnXSourceChanged;
        XWebView.CoreWebView2.HistoryChanged += OnXHistoryChanged;
        XWebView.CoreWebView2.NewWindowRequested += OnXNewWindowRequested;
        XWebView.CoreWebView2.DownloadStarting += OnXDownloadStarting;
        XWebView.CoreWebView2.ContextMenuRequested += OnXContextMenuRequested;
    }

    private static async Task<CoreWebView2Environment> CreateDefaultWebViewEnvironmentAsync(string userDataFolder)
    {
        Directory.CreateDirectory(userDataFolder);
        var createWithUserDataFolder = typeof(CoreWebView2Environment).GetMethod(
            nameof(CoreWebView2Environment.CreateAsync),
            [typeof(string), typeof(string), typeof(CoreWebView2EnvironmentOptions)]);

        if (createWithUserDataFolder is not null)
        {
            var task = (Task<CoreWebView2Environment>)createWithUserDataFolder.Invoke(null, [null, userDataFolder, null])!;
            return await task.ConfigureAwait(true);
        }

        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder, EnvironmentVariableTarget.Process);
        return await CoreWebView2Environment.CreateAsync();
    }

    private async Task<XExtensionDiagnosticTestRun> RunXExtensionCapabilityTestAsync()
    {
        var sdkVersion = GetWebView2SdkVersion();
        var runtimeVersion = GetAvailableWebView2RuntimeVersion();
        var pathProvider = new LocalApplicationDataPathProvider();
        var diagnosticUserDataFolder = pathProvider.GetXBrowserExtensionDiagnosticUserDataFolder();
        CoreWebView2Environment environment;

        try
        {
            Directory.CreateDirectory(diagnosticUserDataFolder);
            var options = new CoreWebView2EnvironmentOptions
            {
                AreBrowserExtensionsEnabled = true,
            };
            var previousUserDataFolderOverride = Environment.GetEnvironmentVariable(
                "WEBVIEW2_USER_DATA_FOLDER",
                EnvironmentVariableTarget.Process);
            try
            {
                Environment.SetEnvironmentVariable(
                    "WEBVIEW2_USER_DATA_FOLDER",
                    null,
                    EnvironmentVariableTarget.Process);
                environment = await CoreWebView2Environment.CreateWithOptionsAsync(
                    string.Empty,
                    diagnosticUserDataFolder,
                    options);
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    "WEBVIEW2_USER_DATA_FOLDER",
                    previousUserDataFolderOverride,
                    EnvironmentVariableTarget.Process);
            }

            runtimeVersion = environment.BrowserVersionString;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                "Extension capability environment creation failed",
                ex,
                diagnosticUserDataFolder));
            return new(
                XBrowserExtensionCapabilityTestResult.EnvironmentCreationFailed(
                    sdkVersion,
                    runtimeVersion,
                    ex,
                    GetSafeDiagnosticExceptionMessage(ex, diagnosticUserDataFolder)),
                Session: null);
        }

        var diagnosticWebView = new WebView2
        {
            Width = 1,
            Height = 1,
            Opacity = 0,
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        RootGrid.Children.Add(diagnosticWebView);

        try
        {
            try
            {
                await diagnosticWebView.EnsureCoreWebView2Async(environment);
                if (diagnosticWebView.CoreWebView2 is null)
                {
                    throw new InvalidOperationException("WinUI WebView2 initialization completed without a CoreWebView2 instance.");
                }

                ApplyXSecuritySettings(diagnosticWebView.CoreWebView2.Settings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    "Extension capability WinUI initialization failed",
                    ex,
                    diagnosticUserDataFolder));
                CloseXExtensionDiagnosticWebView(diagnosticWebView);
                return new(
                    XBrowserExtensionCapabilityTestResult.WinUiInitializationFailed(
                        sdkVersion,
                        runtimeVersion,
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, diagnosticUserDataFolder)),
                    Session: null);
            }

            try
            {
                var extensionProfile = new CoreWebView2ExtensionProfileAdapter(
                    diagnosticWebView.CoreWebView2.Profile,
                    diagnosticWebView.DispatcherQueue);
                var adBlockService = new XDiagnosticAdBlockService(
                    extensionProfile,
                    new FileXDiagnosticAdBlockBindingStore(pathProvider.GetDiagnosticAdBlockBindingPath()),
                    new FileXDiagnosticAdBlockSourceInspector());
                var snapshot = await adBlockService.GetSnapshotAsync();
                return new(
                    XBrowserExtensionCapabilityTestResult.Confirmed(
                        sdkVersion,
                        runtimeVersion,
                        snapshot.Extensions,
                        snapshot.AdBlockStatus),
                    new XExtensionDiagnosticSession(diagnosticWebView, adBlockService, runtimeVersion));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    "Extension capability profile API failed",
                    ex,
                    diagnosticUserDataFolder));
                CloseXExtensionDiagnosticWebView(diagnosticWebView);
                return new(
                    XBrowserExtensionCapabilityTestResult.ExtensionApiFailed(
                        sdkVersion,
                        runtimeVersion,
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, diagnosticUserDataFolder)),
                    Session: null);
            }
        }
        catch
        {
            CloseXExtensionDiagnosticWebView(diagnosticWebView);
            throw;
        }
    }

    private void CloseXExtensionDiagnosticWebView(WebView2 diagnosticWebView)
    {
        try
        {
            diagnosticWebView.Close();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Extension diagnostic cleanup failed: {ex.GetType().FullName} 0x{ex.HResult:X8}");
        }

        RootGrid.Children.Remove(diagnosticWebView);
    }

    private static string GetSafeDiagnosticExceptionMessage(Exception exception, params string[] privatePaths)
        => SafeDiagnosticExceptionFormatter.GetSafeMessage(exception, privatePaths);

    private static string GetWebView2SdkVersion()
    {
        var assembly = typeof(CoreWebView2Environment).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informationalVersion;
    }

    private static string GetAvailableWebView2RuntimeVersion()
    {
        try
        {
            return CoreWebView2Environment.GetAvailableBrowserVersionString(null);
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.GetType().Name} 0x{ex.HResult:X8})";
        }
    }

    private static void ApplyXSecuritySettings(CoreWebView2Settings settings)
    {
        var policy = XBrowserSecurityConfiguration.IsolatedDefault;
        settings.AreHostObjectsAllowed = policy.AreHostObjectsAllowed;
        settings.IsWebMessageEnabled = policy.IsWebMessageEnabled;
        settings.IsPasswordAutosaveEnabled = policy.IsPasswordAutosaveEnabled;
        settings.IsGeneralAutofillEnabled = policy.IsGeneralAutofillEnabled;
#if !DEBUG
        settings.AreDevToolsEnabled = false;
#endif
    }

    private async Task InitializePersistenceAsync(LocalApplicationDataPathProvider pathProvider)
    {
        try
        {
            var databaseInitializer = new LocalDatabaseInitializer(pathProvider.GetDatabasePath());
            await databaseInitializer.InitializeAsync().ConfigureAwait(false);

            var preferenceStore = new SqliteAccentThemePreferenceStore(databaseInitializer.CreateDbContext);
            accentThemePreferenceService = new AccentThemePreferenceService(preferenceStore);
            xPaneWidthPreferenceService = new XPaneWidthPreferenceService(new SqliteXPaneWidthPreferenceStore(databaseInitializer.CreateDbContext));
            xPaneSidePreferenceService = new XPaneSidePreferenceService(new SqliteXPaneSidePreferenceStore(databaseInitializer.CreateDbContext));

            var eventStore = new SqliteEventStore(databaseInitializer.CreateDbContext);
            eventCatalogService = new EventCatalogService(eventStore);
            eventSourceBridgeHandler = new EventSourceBridgeHandler(eventCatalogService);
            watchlistBridgeHandler = new WatchlistBridgeHandler(
                new WatchlistService(new SqliteWatchlistStore(databaseInitializer.CreateDbContext)));
            alertBridgeHandler = new AlertBridgeHandler(
                new AlertService(new SqliteAlertStore(databaseInitializer.CreateDbContext)));
            gdacsHttpClient = GdacsEventFeed.CreateProductionHttpClient();
            var gdacsFeed = new GdacsEventFeed(gdacsHttpClient, new GdacsGeoJsonParser());
            gdacsRefreshBridgeHandler = new GdacsRefreshBridgeHandler(new GdacsRefreshService(gdacsFeed, eventStore));
            reliefWebHttpClient = ReliefWebReportClient.CreateProductionHttpClient();
            var reliefWebClient = new ReliefWebReportClient(
                reliefWebHttpClient,
                new ReliefWebReportParser(),
                new ReliefWebClientOptions(AppName: null));
            reliefWebBridgeHandler = new ReliefWebBridgeHandler(
                new ReliefWebReportDiscoveryService(reliefWebClient, eventStore, eventStore));
            await new DemoDataSeeder(eventCatalogService).SeedAsync().ConfigureAwait(false);

            persistenceStatus = "SQLite local application data";
            persistenceInitializationError = null;
        }
        catch (Exception)
        {
            accentThemePreferenceService = null;
            xPaneWidthPreferenceService = null;
            xPaneSidePreferenceService = null;
            eventCatalogService = null;
            eventSourceBridgeHandler = null;
            gdacsRefreshBridgeHandler = null;
            reliefWebBridgeHandler = null;
            watchlistBridgeHandler = null;
            alertBridgeHandler = null;
            gdacsHttpClient?.Dispose();
            gdacsHttpClient = null;
            reliefWebHttpClient?.Dispose();
            reliefWebHttpClient = null;
            persistenceStatus = "SQLite local application data unavailable";
            persistenceInitializationError = PersistenceUnavailableMessage;
        }
        finally
        {
            persistenceInitialized = true;
        }
    }

    private static void OnAppNavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        if (!PrivilegedWebViewSecurityPolicy.IsTrustedOrigin(args.Uri))
        {
            args.Cancel = true;
        }
    }

    private void OnXNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        switch (XBrowserNavigationPolicy.ClassifyTopLevelNavigation(args.Uri))
        {
            case XBrowserNavigationDecision.AllowInPane:
                HideXError();
                return;
            case XBrowserNavigationDecision.OpenExternally:
                args.Cancel = true;
                OpenInSystemBrowser(args.Uri);
                return;
            default:
                args.Cancel = true;
                ShowXError("X navigation was blocked because it was not a permitted HTTPS X website page.");
                return;
        }
    }

    private void OnXNavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (args.IsSuccess)
        {
            xRuntimeState = XBrowserRuntimeState.Ready;
            HideXError();
        }
        else
        {
            xRuntimeState = XBrowserRuntimeState.Failed;
            ShowXError($"X website could not load ({args.WebErrorStatus}). Use Retry or Open External.");
        }

        UpdateXNavigationButtons();
        UpdateXPostLinkButton();
    }

    private void OnXSourceChanged(CoreWebView2 sender, CoreWebView2SourceChangedEventArgs args)
    {
        XCopyStatusText.Text = string.Empty;
        UpdateXNavigationButtons();
        UpdateXPostLinkButton();
    }

    private void OnXHistoryChanged(object? sender, object args)
    {
        UpdateXNavigationButtons();
    }

    private void OnXNewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        switch (XBrowserNavigationPolicy.ClassifyTopLevelNavigation(args.Uri))
        {
            case XBrowserNavigationDecision.AllowInPane:
                XWebView.CoreWebView2.Navigate(args.Uri);
                break;
            case XBrowserNavigationDecision.OpenExternally:
                OpenInSystemBrowser(args.Uri);
                break;
        }
    }

    private static void OnXDownloadStarting(CoreWebView2 sender, CoreWebView2DownloadStartingEventArgs args)
    {
        args.Cancel = true;
    }

    private void OnXContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
    {
        if (!XContextMenuPostLinkPolicy.TryCreateCopyPostLinkCommand(args.ContextMenuTarget.LinkUri, out var normalizedUrl))
        {
            return;
        }

        var copyPostLinkItem = sender.Environment.CreateContextMenuItem("Copy X Post Link", null, CoreWebView2ContextMenuItemKind.Command);
        copyPostLinkItem.CustomItemSelected += async (_, _) => await CopyXPostLinkAsync(normalizedUrl);
        args.MenuItems.Insert(0, copyPostLinkItem);
    }

    private async void OnWebMessageReceived(WebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        await PrivilegedBridgeMessageProcessor.TryProcessAsync(
            args.Source,
            args.WebMessageAsJson,
            request => DispatchPrivilegedBridgeRequestAsync(sender, request));
    }

    private async Task DispatchPrivilegedBridgeRequestAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        switch (request.Type)
        {
            case DesktopBridgeProtocol.AppReadyMessageType:
                SendAppInfo(sender);
                break;
            case DesktopBridgeProtocol.PreferencesGetMessageType:
                await SendAccentPreferenceAsync(sender);
                break;
            case DesktopBridgeProtocol.PreferencesSetMessageType:
                await SaveAccentPreferenceAsync(sender, request.PreferenceValue);
                break;
            case DesktopBridgeProtocol.EventsListMessageType:
                await SendEventsAsync(sender);
                break;
            case DesktopBridgeProtocol.EventsGetMessageType:
                await SendEventAsync(sender, request.EventId);
                break;
            case DesktopBridgeProtocol.EventSourcesGetMessageType:
                await SendEventSourcesAsync(sender, request.EventId);
                break;
            case DesktopBridgeProtocol.EventSourcesAttachXPostMessageType:
                await AttachXPostSourceAsync(sender, request.EventId, request.XPostUrl);
                break;
            case DesktopBridgeProtocol.GdacsRefreshMessageType:
                await RefreshGdacsAsync(sender);
                break;
            case DesktopBridgeProtocol.FirmsRefreshMessageType:
                await RefreshFirmsAsync(sender, request);
                break;
            case DesktopBridgeProtocol.ReliefWebSearchMessageType:
                await SearchReliefWebAsync(sender, request);
                break;
            case DesktopBridgeProtocol.ReliefWebAttachMessageType:
                await AttachReliefWebAsync(sender, request);
                break;
            case DesktopBridgeProtocol.WatchlistsListMessageType:
            case DesktopBridgeProtocol.WatchlistsCreateMessageType:
            case DesktopBridgeProtocol.WatchlistsRenameMessageType:
            case DesktopBridgeProtocol.WatchlistsDeleteMessageType:
            case DesktopBridgeProtocol.WatchlistMembersGetMessageType:
            case DesktopBridgeProtocol.WatchlistMembershipsGetForEventMessageType:
            case DesktopBridgeProtocol.WatchlistMembersSetMessageType:
                await SendWatchlistResponseAsync(sender, request);
                break;
            case DesktopBridgeProtocol.AlertsSummaryMessageType:
            case DesktopBridgeProtocol.AlertRulesListMessageType:
            case DesktopBridgeProtocol.AlertRulesSetMessageType:
            case DesktopBridgeProtocol.AlertsListMessageType:
            case DesktopBridgeProtocol.AlertsMarkReadMessageType:
                await SendAlertResponseAsync(sender, request);
                break;
        }
    }

    private async Task SendWatchlistResponseAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        var response = watchlistBridgeHandler is null
            ? DesktopBridgeProtocol.CreateWatchlistErrorResponseJson(
                request.Type,
                "PersistenceUnavailable",
                "Unable to update local watchlists.",
                request.WatchlistId,
                request.EventId)
            : await watchlistBridgeHandler.HandleAsync(request, lifetimeCancellation.Token).ConfigureAwait(true);
        sender.CoreWebView2.PostWebMessageAsJson(response);
    }

    private async Task SendAlertResponseAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        var response = alertBridgeHandler is null
            ? DesktopBridgeProtocol.CreateAlertErrorResponseJson(
                request.Type,
                "PersistenceUnavailable",
                "Alerts are unavailable. Existing Events and Watchlists were not changed.",
                request.WatchlistId,
                request.AlertId)
            : await alertBridgeHandler.HandleAsync(request, lifetimeCancellation.Token).ConfigureAwait(true);
        sender.CoreWebView2.PostWebMessageAsJson(response);
    }

    private void SendAppInfo(WebView2 sender)
    {
        var responseJson = DesktopBridgeProtocol.CreateAppInfoResponseJson(
            new DesktopBridgeAppInfo(
                ProductIdentity.DisplayName,
                "WinUI 3",
                "WebView2 desktop host",
                persistenceStatus,
                false,
                xPaneSide));

        sender.CoreWebView2.PostWebMessageAsJson(responseJson);
    }

    private void NotifyXPaneSideChanged()
    {
        AppWebView.CoreWebView2?.PostWebMessageAsJson(DesktopBridgeProtocol.CreateXPaneSideValueResponseJson(xPaneSide));
    }

    private async Task SendEventsAsync(WebView2 sender)
    {
        if (eventCatalogService is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
            return;
        }

        try
        {
            var events = await eventCatalogService.GetEventsAsync();
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsListResultResponseJson(events));
        }
        catch (Exception)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
        }
    }

    private async Task SendEventAsync(WebView2 sender, string? eventId)
    {
        if (eventCatalogService is null || eventId is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
            return;
        }

        try
        {
            var geopoliticalEvent = await eventCatalogService.GetEventByIdAsync(eventId);
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventGetResultResponseJson(geopoliticalEvent));
        }
        catch (Exception)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
        }
    }

    private async Task SendEventSourcesAsync(WebView2 sender, string? eventId)
    {
        if (eventCatalogService is null || eventId is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
            return;
        }

        try
        {
            var sources = await eventCatalogService.GetSourcesForEventAsync(eventId);
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventSourcesResultResponseJson(eventId, sources));
        }
        catch (Exception)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateEventsErrorResponseJson(EventDataUnavailableMessage));
        }
    }

    private async Task AttachXPostSourceAsync(WebView2 sender, string? eventId, string? xPostUrl)
    {
        if (eventSourceBridgeHandler is null || eventId is null || xPostUrl is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(
                DesktopBridgeProtocol.CreateEventSourcesAttachXPostErrorResponseJson(
                    eventId ?? string.Empty,
                    "Unable to attach the X post source."));
            return;
        }

        var response = await eventSourceBridgeHandler.AttachXPostAsync(eventId, xPostUrl);
        sender.CoreWebView2.PostWebMessageAsJson(response);
    }

    private async Task RefreshGdacsAsync(WebView2 sender)
    {
        if (gdacsRefreshBridgeHandler is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(
                DesktopBridgeProtocol.CreateGdacsRefreshErrorResponseJson(
                    GdacsRefreshStatus.PersistenceFailure,
                    "GDACS refresh is unavailable."));
            return;
        }

        var response = await gdacsRefreshBridgeHandler.RefreshAsync(lifetimeCancellation.Token);
        if (!lifetimeCancellation.IsCancellationRequested && sender.CoreWebView2 is not null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(response);
        }
    }

    private async Task RefreshFirmsAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        if (firmsRefreshBridgeHandler is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(
                DesktopBridgeProtocol.CreateFirmsRefreshErrorResponseJson(
                    FirmsRefreshStatus.RequestFailed,
                    "NASA FIRMS refresh is unavailable. Existing layer data was preserved."));
            return;
        }

        var response = await firmsRefreshBridgeHandler.RefreshAsync(request, lifetimeCancellation.Token);
        if (!lifetimeCancellation.IsCancellationRequested && sender.CoreWebView2 is not null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(response);
        }
    }

    private async Task SearchReliefWebAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        if (reliefWebBridgeHandler is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(
                DesktopBridgeProtocol.CreateReliefWebSearchErrorResponseJson(
                    request.EventId ?? string.Empty,
                    ReliefWebSearchStatus.PersistenceFailure,
                    "ReliefWeb search is unavailable."));
            return;
        }

        var response = await reliefWebBridgeHandler.SearchAsync(request, lifetimeCancellation.Token);
        if (!lifetimeCancellation.IsCancellationRequested && sender.CoreWebView2 is not null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(response);
        }
    }

    private async Task AttachReliefWebAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        if (reliefWebBridgeHandler is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(
                DesktopBridgeProtocol.CreateReliefWebAttachErrorResponseJson(
                    request.EventId ?? string.Empty,
                    request.ReliefWebReportId ?? string.Empty,
                    ReliefWebAttachStatus.PersistenceFailure,
                    "Unable to attach the ReliefWeb report."));
            return;
        }

        var response = await reliefWebBridgeHandler.AttachAsync(request, lifetimeCancellation.Token);
        if (!lifetimeCancellation.IsCancellationRequested && sender.CoreWebView2 is not null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(response);
        }
    }

    private async Task SendAccentPreferenceAsync(WebView2 sender)
    {
        if (accentThemePreferenceService is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceErrorResponseJson(persistenceInitializationError ?? PersistenceUnavailableMessage));
            return;
        }

        try
        {
            var accentTheme = await accentThemePreferenceService.GetAccentThemeAsync();
            ApplyNativeAccent(accentTheme);
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceValueResponseJson(accentTheme));
        }
        catch (Exception)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceErrorResponseJson(PersistenceUnavailableMessage));
        }
    }

    private async Task SaveAccentPreferenceAsync(WebView2 sender, string? preferenceValue)
    {
        if (accentThemePreferenceService is null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceErrorResponseJson(persistenceInitializationError ?? PersistenceUnavailableMessage));
            return;
        }

        if (!AccentThemePreferenceService.TryParseAccentTheme(preferenceValue, out var accentTheme))
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceErrorResponseJson("Unsupported preference value."));
            return;
        }

        try
        {
            var savedAccentTheme = await accentThemePreferenceService.SaveAccentThemeAsync(accentTheme);
            ApplyNativeAccent(savedAccentTheme);
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceUpdatedResponseJson(savedAccentTheme));
        }
        catch (Exception)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreatePreferenceErrorResponseJson(PersistenceUnavailableMessage));
        }
    }

    private void OnXBackClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        if (XWebView.CoreWebView2?.CanGoBack == true)
        {
            XWebView.CoreWebView2.GoBack();
        }
    }

    private void OnXForwardClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        if (XWebView.CoreWebView2?.CanGoForward == true)
        {
            XWebView.CoreWebView2.GoForward();
        }
    }

    private void OnXHomeClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        NavigateXHome();
    }

    private void OnXRefreshClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        if (XWebView.CoreWebView2 is null)
        {
            NavigateXHome();
            return;
        }

        HideXError();
        XWebView.CoreWebView2.Reload();
    }

    private async void OnXRetryClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        if (xInitializationTask is not null && !xInitializationTask.IsCompleted)
        {
            return;
        }

        if (XWebView.CoreWebView2 is not null)
        {
            xRuntimeState = XBrowserRuntimeState.Initializing;
            NavigateXHome();
            UpdateXNavigationButtons();
            UpdateXPostLinkButton();
            return;
        }

        if (string.IsNullOrWhiteSpace(xBrowserUserDataFolder))
        {
            xRuntimeState = XBrowserRuntimeState.Failed;
            ShowXError("X website pane could not retry because its local profile path is unavailable.");
            UpdateXNavigationButtons();
            UpdateXPostLinkButton();
            return;
        }

        xInitializationTask = InitializeXWebViewAsync(xBrowserUserDataFolder);
        await xInitializationTask;
    }

    private void OnXOpenExternalClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        var currentUri = XWebView.Source?.ToString();
        OpenInSystemBrowser(XBrowserNavigationPolicy.IsAllowedInPane(currentUri) ? currentUri : XBrowserDefaults.HomeUrl);
    }

    private async void OnXCopyPostLinkClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        await CopyXPostLinkAsync(XWebView.Source?.ToString());
    }
    private async Task CopyXPostLinkAsync(string? url)
    {
        var result = await postLinkCopyService.CopyIfValidAsync(url);
        if (result.Copied)
        {
            XCopyStatusText.Text = "Post link copied";
            UpdateXPostLinkButton();
            return;
        }

        XCopyStatusText.Text = "Open a post first";
        UpdateXPostLinkButton();
    }

    private async void OnXClearSessionClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        if (XWebView.CoreWebView2 is null)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Clear X session?",
            Content = $"This clears the isolated X website cookies and browsing data for {ProductIdentity.DisplayName}. Local events and preferences are not affected.",
            PrimaryButtonText = "Clear Session",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        XWebView.CoreWebView2.CookieManager.DeleteAllCookies();
        await XWebView.CoreWebView2.Profile.ClearBrowsingDataAsync();
        NavigateXHome();
    }

    private async void OnXExtensionsClicked(object sender, RoutedEventArgs args)
    {
        HideXOverflowPanel();
        await ShowXProductionAdBlockDialogAsync();
    }

    private void OnXOverflowClicked(object sender, RoutedEventArgs args)
    {
        XOverflowPanel.Visibility = XOverflowPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private void HideXOverflowPanel()
    {
        XOverflowPanel.Visibility = Visibility.Collapsed;
    }

    private async Task ShowXProductionAdBlockDialogAsync()
    {
        var pathProvider = new LocalApplicationDataPathProvider();
        var managedSourcePath = pathProvider.GetProductionAdBlockManagedSourceDirectory();
        var sourceInspector = new FileXDiagnosticAdBlockSourceInspector();
        var bindingStore = new FileXDiagnosticAdBlockBindingStore(pathProvider.GetProductionAdBlockBindingPath());
        var payloadStore = new FileXAdBlockManagedPayloadStore(managedSourcePath, sourceInspector);
        var statusText = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 212, 214, 218)),
        };
        var refreshButton = CreateXExtensionDialogButton("Refresh");
        var installButton = CreateXExtensionDialogButton("Install AdBlock...");
        var bindButton = CreateXExtensionDialogButton("Reconcile");
        var enableButton = CreateXExtensionDialogButton("Enable");
        var disableButton = CreateXExtensionDialogButton("Disable");
        var removeButton = CreateXExtensionDialogButton("Remove");
        var diagnosticButton = CreateXExtensionDialogButton("Test Extension Support");
        XDiagnosticAdBlockSnapshot? snapshot = null;
        var actionInProgress = false;
        var openDiagnostic = false;
        string? operationMessage = null;

        var managementActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        managementActions.Children.Add(enableButton);
        managementActions.Children.Add(disableButton);
        managementActions.Children.Add(removeButton);
        var setupActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        setupActions.Children.Add(installButton);
        setupActions.Children.Add(bindButton);
        setupActions.Children.Add(refreshButton);
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 360,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = statusText,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Install accepts only the reviewed Chrome Web Store AdBlock 6.47.0 payload. The app authenticates the complete runtime tree, stages and re-verifies it in private local storage, and manages only that trusted production X-profile instance.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 182, 187)),
        });
        content.Children.Add(setupActions);
        content.Children.Add(managementActions);
        content.Children.Add(diagnosticButton);

        var dialog = new ContentDialog
        {
            Title = "X Ad Blocker",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot,
        };

        void RefreshPresentation()
        {
            var hasManagedSource = Directory.Exists(managedSourcePath);
            statusText.Text = XProductionAdBlockPolicy.Format(
                xProductionExtensionHostState,
                snapshot,
                hasManagedSource,
                xProductionExtensionStartupFailure,
                operationMessage);
            var actions = XProductionAdBlockPolicy.GetActions(
                xProductionExtensionHostState,
                snapshot,
                hasManagedSource,
                actionInProgress);
            refreshButton.IsEnabled = actions.CanRefresh;
            installButton.IsEnabled = actions.CanInstall || (xProductionRequiresTrustedReenrollment && !actionInProgress);
            bindButton.IsEnabled = actions.CanBind;
            enableButton.IsEnabled = actions.CanEnable;
            disableButton.IsEnabled = actions.CanDisable;
            removeButton.IsEnabled = actions.CanRemove;
            diagnosticButton.IsEnabled = actions.CanRunDiagnostic;
        }

        async Task ExecuteAsync(
            string stage,
            Func<XDiagnosticAdBlockService, Task<XDiagnosticAdBlockSnapshot>> operation,
            params string[] privatePaths)
        {
            var service = xProductionAdBlockService;
            if (service is null)
            {
                return;
            }

            actionInProgress = true;
            operationMessage = null;
            RefreshPresentation();
            try
            {
                snapshot = await operation(service);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    $"Production AdBlock operation failed at {stage}",
                    ex,
                    privatePaths));
                snapshot = new(
                    snapshot?.Extensions ?? [],
                    XDiagnosticAdBlockStatus.Failed(
                        XDiagnosticAdBlockState.OperationFailed,
                        stage,
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, privatePaths)));
            }
            finally
            {
                actionInProgress = false;
                RefreshPresentation();
            }
        }

        refreshButton.Click += async (_, _) =>
            await ExecuteAsync("Profile.GetBrowserExtensionsAsync", service => service.GetSnapshotAsync());

        installButton.Click += async (_, _) =>
        {
            if (xProductionAdBlockService is null && !xProductionRequiresTrustedReenrollment)
            {
                return;
            }

            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            actionInProgress = true;
            operationMessage = null;
            RefreshPresentation();
            try
            {
                var validation = await sourceInspector.InspectAsync(folder.Path);
                if (!validation.IsValid || validation.Identity is null)
                {
                    snapshot = new(
                        snapshot?.Extensions ?? [],
                        XDiagnosticAdBlockStatus.InvalidFolder(validation.ErrorMessage ?? "The selected folder is invalid."));
                    return;
                }

                var prepared = await payloadStore.PrepareAsync(validation.Identity);
                if (!prepared.Success || prepared.Identity is null)
                {
                    snapshot = new(
                        snapshot?.Extensions ?? [],
                        XDiagnosticAdBlockStatus.InvalidFolder(prepared.ErrorMessage ?? "The managed payload could not be prepared."));
                    return;
                }

                if (xProductionAdBlockService is null)
                {
                    await bindingStore.ClearAsync();
                    xProductionRequiresTrustedReenrollment = false;
                    operationMessage = "The trusted AdBlock payload was enrolled without binding a live extension. Restart, then use Reconcile (or Install if no instance remains) to perform a fresh verified WebView2 install transaction.";
                    return;
                }

                snapshot = await xProductionAdBlockService.InstallAsync(prepared.Identity.SourceFolderPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    "Production AdBlock installation failed",
                    ex,
                    folder.Path,
                    managedSourcePath));
                snapshot = new(
                    snapshot?.Extensions ?? [],
                    XDiagnosticAdBlockStatus.Failed(
                        XDiagnosticAdBlockState.InstallationFailed,
                        "Production install",
                        ex,
                        GetSafeDiagnosticExceptionMessage(ex, folder.Path, managedSourcePath)));
            }
            finally
            {
                actionInProgress = false;
                RefreshPresentation();
            }
        };

        bindButton.Click += async (_, _) =>
            await ExecuteAsync(
                "Production binding validation",
                service => service.BindInstalledAsync(managedSourcePath),
                managedSourcePath);
        enableButton.Click += async (_, _) =>
            await ExecuteAsync("CoreWebView2BrowserExtension.EnableAsync(true)", service => service.SetEnabledAsync(true));
        disableButton.Click += async (_, _) =>
            await ExecuteAsync("CoreWebView2BrowserExtension.EnableAsync(false)", service => service.SetEnabledAsync(false));
        removeButton.Click += async (_, _) =>
        {
            var wasManaged = snapshot?.AdBlockStatus.IsManagedInstance == true;
            await ExecuteAsync("CoreWebView2BrowserExtension.RemoveAsync", service => service.RemoveAsync());
            if (wasManaged && snapshot?.AdBlockStatus.State == XDiagnosticAdBlockState.NoManagedInstance)
            {
                var cleanup = await payloadStore.RemoveAsync();
                operationMessage = cleanup.Success
                    ? "AdBlock and its application-managed payload were removed."
                    : cleanup.ErrorMessage;
                RefreshPresentation();
            }
        };
        diagnosticButton.Click += (_, _) =>
        {
            openDiagnostic = true;
            dialog.Hide();
        };
        dialog.Opened += async (_, _) =>
        {
            RefreshPresentation();
            if (xProductionAdBlockService is not null)
            {
                await ExecuteAsync("Profile.GetBrowserExtensionsAsync", service => service.GetSnapshotAsync());
            }
        };
        dialog.Closing += (_, args) => args.Cancel = actionInProgress;

        RefreshPresentation();
        await dialog.ShowAsync();
        if (openDiagnostic)
        {
            await ShowXExtensionDiagnosticDialogAsync();
        }
    }

    private async Task ShowXExtensionDiagnosticDialogAsync()
    {
        var statusText = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromArgb(255, 212, 214, 218)) };
        var testButton = CreateXExtensionDialogButton("Test Extension Support");
        var installButton = CreateXExtensionDialogButton("Install AdBlock...");
        var bindButton = CreateXExtensionDialogButton("Bind Installed AdBlock...");
        var enableButton = CreateXExtensionDialogButton("Enable");
        var disableButton = CreateXExtensionDialogButton("Disable");
        var removeButton = CreateXExtensionDialogButton("Remove");
        XExtensionDiagnosticSession? diagnosticSession = null;
        var actionInProgress = false;

        var installActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        installActions.Children.Add(installButton);
        installActions.Children.Add(bindButton);
        var managementActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        managementActions.Children.Add(enableButton);
        managementActions.Children.Add(disableButton);
        managementActions.Children.Add(removeButton);
        var actions = new StackPanel { Spacing = 8 };
        actions.Children.Add(installActions);
        actions.Children.Add(managementActions);

        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new ScrollViewer
        {
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = statusText,
        });
        content.Children.Add(new TextBlock
        {
            Text = "This explicit validation uses only the isolated diagnostic profile. Select an official unpacked AdBlock Chrome MV3 root containing manifest.json. WebView2 keeps using the selected source folder, so leave it in place until the extension is removed. Production X remains unchanged.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 182, 187)),
        });
        content.Children.Add(testButton);
        content.Children.Add(actions);

        var dialog = new ContentDialog
        {
            Title = "Diagnostic AdBlock Validation",
            Content = content,
            CloseButtonText = "Close",
            XamlRoot = RootGrid.XamlRoot,
        };

        void Refresh()
        {
            statusText.Text = XBrowserExtensionCapabilityTestPolicy.Format(xExtensionCapabilityTest);
            var profileAvailable = diagnosticSession is not null && !actionInProgress;
            var actionState = XBrowserExtensionCapabilityTestPolicy.GetActions(
                xExtensionCapabilityTest,
                hasLiveDiagnosticSession: profileAvailable);
            testButton.IsEnabled = !actionInProgress && actionState.CanRunTest;
            installButton.IsEnabled = actionState.CanInstall;
            bindButton.IsEnabled = actionState.CanBind;
            enableButton.IsEnabled = actionState.CanEnable;
            disableButton.IsEnabled = actionState.CanDisable;
            removeButton.IsEnabled = actionState.CanRemove;
        }

        void ApplySnapshot(XDiagnosticAdBlockSnapshot snapshot)
        {
            if (diagnosticSession is null)
            {
                return;
            }

            xExtensionCapabilityTest = XBrowserExtensionCapabilityTestResult.Confirmed(
                GetWebView2SdkVersion(),
                diagnosticSession.RuntimeVersion,
                snapshot.Extensions,
                snapshot.AdBlockStatus);
        }

        void ApplyOperationStatus(XDiagnosticAdBlockStatus status)
        {
            if (diagnosticSession is null)
            {
                return;
            }

            xExtensionCapabilityTest = XBrowserExtensionCapabilityTestResult.Confirmed(
                GetWebView2SdkVersion(),
                diagnosticSession.RuntimeVersion,
                xExtensionCapabilityTest.Extensions ?? [],
                status);
        }

        async Task ExecuteOperationAsync(
            string stage,
            XDiagnosticAdBlockState failureState,
            Func<XDiagnosticAdBlockService, Task<XDiagnosticAdBlockSnapshot>> operation,
            params string[] privatePaths)
        {
            if (diagnosticSession is null)
            {
                return;
            }

            var hasThreadAccess = diagnosticSession.WebView.DispatcherQueue.HasThreadAccess;
            Debug.WriteLine($"Diagnostic AdBlock operation '{stage}': liveSession=True, ownerThread={hasThreadAccess}");
            actionInProgress = true;
            Refresh();
            try
            {
                ApplySnapshot(await operation(diagnosticSession.AdBlockService));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    $"Diagnostic AdBlock operation failed at {stage}",
                    ex,
                    privatePaths));
                ApplyOperationStatus(XDiagnosticAdBlockStatus.Failed(
                    failureState,
                    stage,
                    ex,
                    GetSafeDiagnosticExceptionMessage(ex, privatePaths)));
            }
            finally
            {
                actionInProgress = false;
                Refresh();
            }
        }

        testButton.Click += async (_, _) =>
        {
            if (!XBrowserExtensionCapabilityTestPolicy.ShouldRun(XBrowserExtensionCapabilityTestTrigger.UserRequested))
            {
                return;
            }

            actionInProgress = true;
            if (diagnosticSession is not null)
            {
                CloseXExtensionDiagnosticWebView(diagnosticSession.WebView);
                diagnosticSession = null;
            }

            xExtensionCapabilityTest = XBrowserExtensionCapabilityTestResult.Testing(
                GetWebView2SdkVersion(),
                GetAvailableWebView2RuntimeVersion());
            Refresh();
            try
            {
                var testRun = await RunXExtensionCapabilityTestAsync();
                xExtensionCapabilityTest = testRun.Result;
                diagnosticSession = testRun.Session;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(SafeDiagnosticExceptionFormatter.FormatForDebug(
                    "Extension capability test failed unexpectedly",
                    ex));
                xExtensionCapabilityTest = XBrowserExtensionCapabilityTestResult.ExtensionApiFailed(
                    GetWebView2SdkVersion(),
                    GetAvailableWebView2RuntimeVersion(),
                    ex,
                    GetSafeDiagnosticExceptionMessage(ex));
            }
            finally
            {
                actionInProgress = false;
                Refresh();
            }
        };

        installButton.Click += async (_, _) =>
        {
            if (diagnosticSession is null)
            {
                return;
            }

            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            await ExecuteOperationAsync(
                "Profile.AddBrowserExtensionAsync",
                XDiagnosticAdBlockState.InstallationFailed,
                service => service.InstallAsync(folder.Path),
                folder.Path);
        };
        bindButton.Click += async (_, _) =>
        {
            if (diagnosticSession is null)
            {
                return;
            }

            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
            picker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null)
            {
                return;
            }

            await ExecuteOperationAsync(
                "Diagnostic binding validation",
                XDiagnosticAdBlockState.OperationFailed,
                service => service.BindInstalledAsync(folder.Path),
                folder.Path);
        };
        enableButton.Click += async (_, _) =>
        {
            await ExecuteOperationAsync(
                "CoreWebView2BrowserExtension.EnableAsync(true)",
                XDiagnosticAdBlockState.OperationFailed,
                service => service.SetEnabledAsync(enabled: true));
        };
        disableButton.Click += async (_, _) =>
        {
            await ExecuteOperationAsync(
                "CoreWebView2BrowserExtension.EnableAsync(false)",
                XDiagnosticAdBlockState.OperationFailed,
                service => service.SetEnabledAsync(enabled: false));
        };
        removeButton.Click += async (_, _) =>
        {
            await ExecuteOperationAsync(
                "CoreWebView2BrowserExtension.RemoveAsync",
                XDiagnosticAdBlockState.OperationFailed,
                service => service.RemoveAsync());
        };

        dialog.Closing += (_, args) => args.Cancel = actionInProgress;

        try
        {
            Refresh();
            await dialog.ShowAsync();
        }
        finally
        {
            if (diagnosticSession is not null)
            {
                CloseXExtensionDiagnosticWebView(diagnosticSession.WebView);
            }
        }
    }

    private static Button CreateXExtensionDialogButton(string text)
    {
        return new Button
        {
            Content = text,
            MinHeight = 32,
            Padding = new Thickness(10, 4, 10, 4),
        };
    }

    private async void OnMoveXPaneSideClicked(object sender, RoutedEventArgs args)
    {
        var nextSide = xPaneSide == XPaneSide.Right ? XPaneSide.Left : XPaneSide.Right;
        ApplyXPaneSide(nextSide, notifyReact: true, updateWidth: true);

        if (xPaneSidePreferenceService is null)
        {
            return;
        }

        try
        {
            await xPaneSidePreferenceService.SaveXPaneSideAsync(nextSide);
        }
        catch (Exception)
        {
            // Side changes are usable for the current session even if preference persistence is unavailable.
        }
    }

    private void OnCollapseXPaneClicked(object sender, RoutedEventArgs args)
    {
        var collapseState = XPaneLayout.Collapse(GetXPaneColumn().Width.Value, expandedXPaneWidth, GetAvailableLayoutWidth());
        expandedXPaneWidth = collapseState.RememberedWidth;
        isXPaneCollapsed = true;
        XPane.Visibility = Visibility.Collapsed;
        XPaneSplitter.Visibility = Visibility.Collapsed;
        SplitterColumn.Width = new GridLength(0);
        GetXPaneColumn().Width = new GridLength(0);
        GetXPaneColumn().MinWidth = 0;
        GetMainAppColumn().Width = new GridLength(1, GridUnitType.Star);
        GetMainAppColumn().MinWidth = XBrowserDefaults.MinimumMainPaneWidth;
        ShowXPaneButton.Visibility = Visibility.Visible;
        RootGrid.InvalidateMeasure();
    }

    private void OnShowXPaneClicked(object sender, RoutedEventArgs args)
    {
        isXPaneCollapsed = false;
        SplitterColumn.Width = new GridLength(XBrowserDefaults.SplitterWidth);
        XPaneSplitter.Visibility = Visibility.Visible;
        XPane.Visibility = Visibility.Visible;
        ShowXPaneButton.Visibility = Visibility.Collapsed;
        ApplyXPaneSide(xPaneSide, notifyReact: true, updateWidth: false);
        ApplyXPaneWidth(XPaneLayout.Reopen(expandedXPaneWidth, GetAvailableLayoutWidth()), updatePreferredWidth: false);
        RootGrid.InvalidateMeasure();
    }

    private async void OnRootGridLoaded(object sender, RoutedEventArgs args)
    {
        await RestoreXPaneWidthAsync();
    }

    private void OnRootGridSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (isXPaneCollapsed || !xPaneWidthRestored)
        {
            return;
        }

        ApplyXPaneWidth(expandedXPaneWidth, updatePreferredWidth: false);
    }

    private void OnXPaneSplitterPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        isSplitterPointerOver = true;
        SetSplitterActive(true);
    }

    private void OnXPaneSplitterPointerExited(object sender, PointerRoutedEventArgs args)
    {
        isSplitterPointerOver = false;
        if (!isDraggingXPaneSplitter)
        {
            SetSplitterActive(false);
        }
    }

    private void OnXPaneSplitterPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        var pointerPoint = args.GetCurrentPoint(XPaneSplitter);
        if (!pointerPoint.Properties.IsLeftButtonPressed)
        {
            return;
        }

        isDraggingXPaneSplitter = true;
        XPaneSplitter.CapturePointer(args.Pointer);
        SetSplitterActive(true);
        ResizeXPaneFromPointer(args.GetCurrentPoint(RootGrid).Position.X);
        args.Handled = true;
    }

    private void OnXPaneSplitterPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!isDraggingXPaneSplitter || !args.GetCurrentPoint(XPaneSplitter).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ResizeXPaneFromPointer(args.GetCurrentPoint(RootGrid).Position.X);
        args.Handled = true;
    }

    private void OnXPaneSplitterPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        FinishXPaneDrag(persistWidth: true);
        XPaneSplitter.ReleasePointerCapture(args.Pointer);
        args.Handled = true;
    }

    private void OnXPaneSplitterPointerCanceled(object sender, PointerRoutedEventArgs args)
    {
        FinishXPaneDrag(persistWidth: false);
    }

    private void OnXPaneSplitterPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        FinishXPaneDrag(persistWidth: true);
    }

    private void ResizeXPaneFromPointer(double pointerX)
    {
        var desiredWidth = xPaneSide == XPaneSide.Left
            ? pointerX - (XBrowserDefaults.SplitterWidth / 2)
            : RootGrid.ActualWidth - pointerX - (XBrowserDefaults.SplitterWidth / 2);
        ApplyXPaneWidth(desiredWidth, updatePreferredWidth: true);
    }

    private void ApplyXPaneWidth(double width, bool updatePreferredWidth)
    {
        var clampedWidth = XPaneLayout.ClampPaneWidth(width, GetAvailableLayoutWidth());
        var xColumn = GetXPaneColumn();
        var appColumn = GetMainAppColumn();
        xColumn.MinWidth = 0;
        xColumn.Width = new GridLength(clampedWidth);
        appColumn.MinWidth = XBrowserDefaults.MinimumMainPaneWidth;
        appColumn.Width = new GridLength(1, GridUnitType.Star);
        if (updatePreferredWidth)
        {
            expandedXPaneWidth = clampedWidth;
        }
    }

    private async Task RestoreXPaneSideAsync()
    {
        if (!persistenceInitialized)
        {
            return;
        }

        try
        {
            var restoredSide = xPaneSidePreferenceService is null
                ? XPaneSide.Right
                : await xPaneSidePreferenceService.GetXPaneSideAsync().ConfigureAwait(true);
            ApplyXPaneSide(restoredSide, notifyReact: false, updateWidth: false);
        }
        catch (Exception)
        {
            ApplyXPaneSide(XPaneSide.Right, notifyReact: false, updateWidth: false);
        }
    }

    private async Task RestoreXPaneWidthAsync()
    {
        if (xPaneWidthRestored || !persistenceInitialized || RootGrid.ActualWidth <= 0)
        {
            return;
        }

        try
        {
            expandedXPaneWidth = xPaneWidthPreferenceService is null
                ? XPaneLayout.GetDefaultPaneWidth(GetAvailableLayoutWidth())
                : await xPaneWidthPreferenceService.GetPreferredXPaneWidthAsync(GetAvailableLayoutWidth()).ConfigureAwait(true);
        }
        catch (Exception)
        {
            expandedXPaneWidth = XPaneLayout.GetDefaultPaneWidth(GetAvailableLayoutWidth());
        }

        ApplyXPaneWidth(expandedXPaneWidth, updatePreferredWidth: false);
        xPaneWidthRestored = true;
    }

    private async void PersistXPaneWidthAsync(double width)
    {
        if (xPaneWidthPreferenceService is null)
        {
            return;
        }

        try
        {
            expandedXPaneWidth = await xPaneWidthPreferenceService.SaveXPaneWidthAsync(width, GetAvailableLayoutWidth()).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // A failed width write must not interrupt the browser pane or app WebView.
        }
    }

    private void FinishXPaneDrag(bool persistWidth)
    {
        if (!isDraggingXPaneSplitter)
        {
            return;
        }

        isDraggingXPaneSplitter = false;
        SetSplitterActive(isSplitterPointerOver);
        if (persistWidth && !isXPaneCollapsed)
        {
            PersistXPaneWidthAsync(expandedXPaneWidth);
        }
    }

    private double GetAvailableLayoutWidth()
    {
        return Math.Max(0, RootGrid.ActualWidth);
    }

    private ColumnDefinition GetXPaneColumn()
    {
        return xPaneSide == XPaneSide.Left ? AppColumn : XPaneColumn;
    }

    private ColumnDefinition GetMainAppColumn()
    {
        return xPaneSide == XPaneSide.Left ? XPaneColumn : AppColumn;
    }

    private void ApplyXPaneSide(XPaneSide side, bool notifyReact, bool updateWidth)
    {
        xPaneSide = side;
        var xIsLeft = xPaneSide == XPaneSide.Left;
        Grid.SetColumn(XPane, xIsLeft ? 0 : 2);
        Grid.SetColumn(AppWebView, xIsLeft ? 2 : 0);
        Grid.SetColumn(ShowXPaneButton, xIsLeft ? 2 : 0);
        Grid.SetColumn(XPaneSplitter, 1);
        XPane.BorderThickness = xIsLeft ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0);
        XMoveSideButton.Content = xIsLeft ? "Move Right" : "Move Left";
        ToolTipService.SetToolTip(XMoveSideButton, xIsLeft ? "Move X pane to the right side" : "Move X pane to the left side");

        if (!isXPaneCollapsed)
        {
            GetMainAppColumn().Width = new GridLength(1, GridUnitType.Star);
            GetMainAppColumn().MinWidth = XBrowserDefaults.MinimumMainPaneWidth;
            if (updateWidth)
            {
                ApplyXPaneWidth(expandedXPaneWidth, updatePreferredWidth: false);
            }
        }

        if (notifyReact)
        {
            NotifyXPaneSideChanged();
        }
    }

    private void SetSplitterActive(bool isActive)
    {
        XPaneSplitterLine.Background = isActive ? currentAccentBrush : splitterNormalBrush;
        XPaneSplitterLine.Width = isActive ? 2 : 1;
        XPaneSplitterGlow.Background = isActive ? currentAccentBrush : splitterGlowTransparentBrush;
        XPaneSplitterGlow.Opacity = isActive ? 0.18 : 0;
    }

    private void NavigateXHome()
    {
        HideXError();
        if (XWebView.CoreWebView2 is not null)
        {
            XWebView.CoreWebView2.Navigate(XBrowserDefaults.HomeUrl);
        }
        else
        {
            XWebView.Source = new Uri(XBrowserDefaults.HomeUrl);
        }
    }

    private static void OpenInSystemBrowser(string? uri)
    {
        if (!XBrowserNavigationPolicy.IsSafeExternalNavigation(uri))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri!) { UseShellExecute = true });
    }

    private void UpdateXNavigationButtons()
    {
        var core = XWebView.CoreWebView2;
        var state = new XToolbarCommandState(
            xRuntimeState,
            CanGoBack: core?.CanGoBack == true,
            CanGoForward: core?.CanGoForward == true,
            CurrentUrl: XWebView.Source?.ToString());
        XBackButton.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Back, state);
        XForwardButton.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Forward, state);
        XHomeButton.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Home, state);
        XRefreshButton.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.Refresh, state);
        XOpenExternalMenuItem.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.OpenExternal, state);
        XClearSessionMenuItem.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.ClearSession, state);
        XExtensionsMenuItem.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.AdBlocker, state);
        XRetryButton.IsEnabled = xRuntimeState == XBrowserRuntimeState.Failed;
    }
    private void UpdateXPostLinkButton()
    {
        var state = new XToolbarCommandState(
            xRuntimeState,
            CanGoBack: XWebView.CoreWebView2?.CanGoBack == true,
            CanGoForward: XWebView.CoreWebView2?.CanGoForward == true,
            CurrentUrl: XWebView.Source?.ToString());
        XCopyPostLinkMenuItem.IsEnabled = XToolbarCommandStatePolicy.IsEnabled(XToolbarCommand.CopyPostLink, state);
    }
    private void ShowXError(string message)
    {
        XStatusText.Text = $"{message} Open X in your browser if the embedded website is unavailable.";
        XErrorBanner.Visibility = Visibility.Visible;
    }

    private void HideXError()
    {
        XStatusText.Text = string.Empty;
        XErrorBanner.Visibility = Visibility.Collapsed;
    }

    private void ApplyNativeAccent(AccentTheme accentTheme)
    {
        var accentColor = accentTheme switch
        {
            AccentTheme.Blue => Color.FromArgb(255, 34, 211, 255),
            AccentTheme.Red => Color.FromArgb(255, 255, 77, 94),
            AccentTheme.Green => Color.FromArgb(255, 61, 255, 135),
            AccentTheme.Yellow => Color.FromArgb(255, 255, 228, 92),
            _ => Color.FromArgb(255, 255, 122, 24),
        };
        var accentTextColor = accentTheme switch
        {
            AccentTheme.Blue => Color.FromArgb(255, 110, 231, 255),
            AccentTheme.Red => Color.FromArgb(255, 255, 138, 146),
            AccentTheme.Green => Color.FromArgb(255, 156, 255, 185),
            AccentTheme.Yellow => Color.FromArgb(255, 255, 240, 138),
            _ => Color.FromArgb(255, 255, 179, 71),
        };

        currentAccentBrush = new SolidColorBrush(accentColor);
        currentAccentTextBrush = new SolidColorBrush(accentTextColor);
        XHeaderAccentBar.Background = currentAccentBrush;
        XHeaderEyebrow.Foreground = currentAccentTextBrush;
        XCopyStatusText.Foreground = currentAccentTextBrush;
        if (isDraggingXPaneSplitter || isSplitterPointerOver)
        {
            SetSplitterActive(true);
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        lifetimeCancellation.Cancel();
        gdacsHttpClient?.Dispose();
        reliefWebHttpClient?.Dispose();
        firmsHttpClient?.Dispose();
        lifetimeCancellation.Dispose();
        XWebView.Close();
        AppWebView.Close();
    }
}
