using GeopoliticalMonitor.Application.DesktopBridge;
using GeopoliticalMonitor.Application.Firms;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace GeopoliticalMonitor.Desktop;

// NASA FIRMS key entry. The key is typed only into the native PasswordBox below and goes only to the Windows
// credential store; React receives key status (configured, source, format warning) and never the key.
public sealed partial class MainWindow
{
    private const string FirmsKeyFormatWarningText = "This key is not 32 hexadecimal characters, which NASA FIRMS keys normally are. You can still save it.";

    private bool firmsKeyDialogOpen;

    private async Task HandleFirmsKeyRequestAsync(WebView2 sender, DesktopBridgeRequest request)
    {
        if (firmsKeyService is null)
        {
            PostFirmsKeyStatus(sender, new FirmsKeyStatus(false, FirmsKeySource.None, false, true), FirmsKeyChangeOutcome.StoreUnavailable);
            return;
        }

        FirmsKeyChangeOutcome? outcome = request.Type switch
        {
            DesktopBridgeProtocol.FirmsKeySetMessageType => await ShowFirmsKeyDialogAsync(firmsKeyService),
            DesktopBridgeProtocol.FirmsKeyRemoveMessageType => await ConfirmRemoveFirmsKeyAsync(firmsKeyService),
            _ => null,
        };
        PostFirmsKeyStatus(sender, firmsKeyService.GetStatus(), outcome);
    }

    private void PostFirmsKeyStatus(WebView2 sender, FirmsKeyStatus status, FirmsKeyChangeOutcome? outcome)
    {
        if (!lifetimeCancellation.IsCancellationRequested && sender.CoreWebView2 is not null)
        {
            sender.CoreWebView2.PostWebMessageAsJson(DesktopBridgeProtocol.CreateFirmsKeyStatusResponseJson(status, outcome));
        }
    }

    private async Task<FirmsKeyChangeOutcome> ShowFirmsKeyDialogAsync(FirmsKeyService service)
    {
        if (firmsKeyDialogOpen)
        {
            return FirmsKeyChangeOutcome.Busy;
        }

        var passwordBox = new PasswordBox
        {
            Header = "MAP_KEY",
            MaxLength = FirmsMapKey.MaximumLength,
            PasswordRevealMode = PasswordRevealMode.Hidden,
        };
        var warningText = new TextBlock
        {
            Text = FirmsKeyFormatWarningText,
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 230, 190, 120)),
            Visibility = Visibility.Collapsed,
        };
        var content = new StackPanel { Spacing = 12 };
        content.Children.Add(new TextBlock
        {
            Text = "Paste your NASA FIRMS MAP_KEY. It is used only for requests to NASA FIRMS and takes effect at the next refresh.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 212, 214, 218)),
        });
        content.Children.Add(passwordBox);
        content.Children.Add(warningText);
        content.Children.Add(new TextBlock
        {
            Text = "The key is stored in Windows Credential Manager for this Windows account on this computer. Other programs running as this Windows user can read it. It is not part of the database or its backups.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 180, 182, 187)),
        });

        var dialog = new ContentDialog
        {
            Title = "NASA FIRMS key",
            Content = content,
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            IsPrimaryButtonEnabled = false,
            XamlRoot = RootGrid.XamlRoot,
        };
        passwordBox.PasswordChanged += (_, _) =>
        {
            dialog.IsPrimaryButtonEnabled = FirmsMapKey.Normalize(passwordBox.Password) is not null;
            warningText.Visibility = FirmsKeyService.ShouldWarn(passwordBox.Password) ? Visibility.Visible : Visibility.Collapsed;
        };
        dialog.Opened += (_, _) => passwordBox.Focus(FocusState.Programmatic);

        firmsKeyDialogOpen = true;
        try
        {
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary
                ? service.SetKey(passwordBox.Password)
                : FirmsKeyChangeOutcome.Cancelled;
        }
        catch (Exception)
        {
            // Another ContentDialog is already open, or the window is closing.
            return FirmsKeyChangeOutcome.Busy;
        }
        finally
        {
            passwordBox.Password = string.Empty;
            firmsKeyDialogOpen = false;
        }
    }

    private async Task<FirmsKeyChangeOutcome> ConfirmRemoveFirmsKeyAsync(FirmsKeyService service)
    {
        if (firmsKeyDialogOpen)
        {
            return FirmsKeyChangeOutcome.Busy;
        }

        var dialog = new ContentDialog
        {
            Title = "Remove NASA FIRMS key",
            Content = "This removes the key stored in the app from Windows Credential Manager. The GEOPOLITICALMONITOR_FIRMS_MAP_KEY environment variable, if set, is not changed and is used instead.",
            PrimaryButtonText = "Remove key",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = RootGrid.XamlRoot,
        };

        firmsKeyDialogOpen = true;
        try
        {
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary ? service.RemoveAppKey() : FirmsKeyChangeOutcome.Cancelled;
        }
        catch (Exception)
        {
            return FirmsKeyChangeOutcome.Busy;
        }
        finally
        {
            firmsKeyDialogOpen = false;
        }
    }
}
