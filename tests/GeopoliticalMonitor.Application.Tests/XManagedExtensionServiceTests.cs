using GeopoliticalMonitor.Application.XBrowser;

namespace GeopoliticalMonitor.Application.Tests;

public sealed class XManagedExtensionServiceTests
{
    [Fact]
    public async Task ReportsNotInstalledWhenNoManagedExtensionIdExists()
    {
        var service = new XManagedExtensionService(new FakeExtensionProfile(), new FakeManagedExtensionIdStore());

        var status = await service.GetStatusAsync();

        Assert.Equal(XBrowserExtensionStatusKind.NotInstalled, status.Kind);
    }

    [Fact]
    public async Task InstallStoresManagedExtensionIdAndReportsEnabledStatus()
    {
        var profile = new FakeExtensionProfile();
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);

        var status = await service.InstallAsync("C:/stable/extension");

        Assert.Equal(XBrowserExtensionStatusKind.Installed, status.Kind);
        Assert.Equal(XAdBlockExtensionPolicy.ChromeExtensionId, store.ExtensionId);
        Assert.True(status.IsEnabled);
    }

    [Fact]
    public async Task InstallIsIdempotentWhenApprovedAdBlockIsAlreadyInstalled()
    {
        var profile = new FakeExtensionProfile();
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);

        await service.InstallAsync("C:/stable/extension");
        var status = await service.InstallAsync("C:/stable/extension");

        Assert.Equal(XBrowserExtensionStatusKind.Installed, status.Kind);
        Assert.Equal(1, profile.AddCount);
    }

    [Fact]
    public async Task ExistingApprovedAdBlockIsAdoptedFromPersistedProfile()
    {
        var profile = new FakeExtensionProfile
        {
            Extension = new XBrowserExtensionInfo(XAdBlockExtensionPolicy.ChromeExtensionId, "AdBlock", IsEnabled: false),
        };
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);

        var status = await service.GetStatusAsync();

        Assert.Equal(XBrowserExtensionStatusKind.Installed, status.Kind);
        Assert.False(status.IsEnabled);
        Assert.Equal(XAdBlockExtensionPolicy.ChromeExtensionId, store.ExtensionId);
    }

    [Fact]
    public async Task RejectsAndRemovesAnUnapprovedExtension()
    {
        var profile = new FakeExtensionProfile
        {
            ExtensionToAdd = new XBrowserExtensionInfo("not-adblock", "Different Extension", IsEnabled: true),
        };
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.InstallAsync("C:/stable/extension"));

        Assert.Contains("approved AdBlock by eyeo", exception.Message);
        Assert.Null(store.ExtensionId);
        Assert.Equal(1, profile.RemoveCount);
    }

    [Fact]
    public async Task DisableAndEnableUseProfileWithoutRemovingExtension()
    {
        var profile = new FakeExtensionProfile();
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);
        await service.InstallAsync("C:/stable/extension");

        var disabled = await service.SetEnabledAsync(false);
        var enabled = await service.SetEnabledAsync(true);

        Assert.False(disabled.IsEnabled);
        Assert.True(enabled.IsEnabled);
        Assert.Equal(0, profile.RemoveCount);
    }

    [Fact]
    public async Task RemoveUsesProfileAndClearsManagedExtensionId()
    {
        var profile = new FakeExtensionProfile();
        var store = new FakeManagedExtensionIdStore();
        var service = new XManagedExtensionService(profile, store);
        await service.InstallAsync("C:/stable/extension");

        var status = await service.RemoveAsync();

        Assert.Equal(XBrowserExtensionStatusKind.NotInstalled, status.Kind);
        Assert.Null(store.ExtensionId);
        Assert.Equal(1, profile.RemoveCount);
    }

    [Fact]
    public async Task MissingManagedExtensionClearsStaleId()
    {
        var store = new FakeManagedExtensionIdStore { ExtensionId = "missing" };
        var service = new XManagedExtensionService(new FakeExtensionProfile(), store);

        var status = await service.GetStatusAsync();

        Assert.Equal(XBrowserExtensionStatusKind.NotInstalled, status.Kind);
        Assert.Null(store.ExtensionId);
    }

    private sealed class FakeExtensionProfile : IXBrowserExtensionProfile
    {
        public XBrowserExtensionInfo? Extension { get; set; }
        public XBrowserExtensionInfo ExtensionToAdd { get; set; } = new(
            XAdBlockExtensionPolicy.ChromeExtensionId,
            "AdBlock",
            IsEnabled: true);
        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }

        public Task<IReadOnlyList<XBrowserExtensionInfo>> GetInstalledExtensionsAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<XBrowserExtensionInfo> extensions = Extension is null ? [] : [Extension];
            return Task.FromResult(extensions);
        }

        public Task<XBrowserExtensionInfo> AddBrowserExtensionAsync(string extensionFolderPath, CancellationToken cancellationToken = default)
        {
            AddCount++;
            Extension = ExtensionToAdd;
            return Task.FromResult(Extension);
        }

        public Task SetExtensionEnabledAsync(string extensionId, bool enabled, CancellationToken cancellationToken = default)
        {
            if (Extension is not null && Extension.Id == extensionId)
            {
                Extension = Extension with { IsEnabled = enabled };
            }

            return Task.CompletedTask;
        }

        public Task RemoveExtensionAsync(string extensionId, CancellationToken cancellationToken = default)
        {
            RemoveCount++;
            if (Extension?.Id == extensionId)
            {
                Extension = null;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeManagedExtensionIdStore : IXManagedExtensionIdStore
    {
        public string? ExtensionId { get; set; }

        public Task<string?> GetManagedExtensionIdAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ExtensionId);
        }

        public Task SaveManagedExtensionIdAsync(string extensionId, CancellationToken cancellationToken = default)
        {
            ExtensionId = extensionId;
            return Task.CompletedTask;
        }

        public Task ClearManagedExtensionIdAsync(CancellationToken cancellationToken = default)
        {
            ExtensionId = null;
            return Task.CompletedTask;
        }
    }
}
