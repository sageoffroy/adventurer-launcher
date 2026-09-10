using System.Diagnostics;
using System.Windows;
using AdventurerLauncher.Models;
using AdventurerLauncher.Services;
using Microsoft.Win32;

namespace AdventurerLauncher;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly RemoteContentService _remoteContent = new();
    private UpdateService? _updateService;
    private LauncherSettings _settings = new();
    private LauncherManifest? _manifest;
    private List<LauncherFile> _pendingFiles = [];

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = await _settingsService.LoadAsync();
            _updateService = new UpdateService(_remoteContent.HttpClient);

            if (UpdateService.IsValidGameFolder(_settings.GamePath))
                GamePathText.Text = _settings.GamePath;
            else
                _settings.GamePath = string.Empty;

            await RefreshRemoteStateAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "No se pudo consultar la actualización.";
            MessageBox.Show(ex.Message, "Aventureros de Azeroth", MessageBoxButton.OK, MessageBoxImage.Warning);
            RefreshButtons();
        }
    }

    private async Task RefreshRemoteStateAsync()
    {
        StatusText.Text = "Buscando actualizaciones...";

        var config = await _remoteContent.LoadConfigAsync();
        _manifest = await _remoteContent.LoadManifestAsync(config.ManifestUrl);

        var news = await _remoteContent.LoadNewsAsync(_manifest.NewsUrl);
        var changelog = await _remoteContent.LoadChangelogAsync(_manifest.ChangelogUrl);
        NewsList.ItemsSource = news;
        ChangelogList.ItemsSource = changelog;

        if (!UpdateService.IsValidGameFolder(_settings.GamePath))
        {
            _pendingFiles = [];
            StatusText.Text = "Elegí la carpeta de World of Warcraft 3.3.5a.";
            GamePathText.Text = "Carpeta de WoW no seleccionada";
            RefreshButtons();
            return;
        }

        _pendingFiles = await _updateService!.GetOutdatedFilesAsync(_settings.GamePath, _manifest);
        StatusText.Text = _pendingFiles.Count == 0
            ? $"Cliente actualizado · versión {_manifest.Version}"
            : $"Actualización disponible · {_pendingFiles.Count} archivo(s)";
        RefreshButtons();
    }

    private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Seleccioná la carpeta de World of Warcraft 3.3.5a",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_settings.GamePath) && Directory.Exists(_settings.GamePath))
            dialog.InitialDirectory = _settings.GamePath;

        if (dialog.ShowDialog(this) != true)
            return;

        if (!UpdateService.IsValidGameFolder(dialog.FolderName))
        {
            MessageBox.Show(
                "La carpeta seleccionada no parece ser una instalación válida: deben existir Wow.exe y la carpeta Data.",
                "Carpeta de WoW inválida",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _settings.GamePath = dialog.FolderName;
        await _settingsService.SaveAsync(_settings);
        GamePathText.Text = _settings.GamePath;
        await RefreshRemoteStateAsync();
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_manifest is null || _updateService is null || _pendingFiles.Count == 0)
            return;

        SetBusy(true);
        UpdateProgress.Visibility = Visibility.Visible;

        try
        {
            for (var index = 0; index < _pendingFiles.Count; index++)
            {
                var file = _pendingFiles[index];
                StatusText.Text = $"Actualizando {file.Path} ({index + 1}/{_pendingFiles.Count})...";

                var progress = new Progress<double>(value =>
                {
                    var overall = (index + value) / _pendingFiles.Count;
                    UpdateProgress.Value = overall * 100;
                });

                await _updateService.DownloadAndInstallAsync(_settings.GamePath, file, progress);
            }

            await RefreshRemoteStateAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = "La actualización no pudo completarse.";
            MessageBox.Show(
                ex.Message,
                "Error al actualizar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            UpdateProgress.Visibility = Visibility.Collapsed;
            UpdateProgress.Value = 0;
            SetBusy(false);
            RefreshButtons();
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateService.IsValidGameFolder(_settings.GamePath))
            return;

        if (_pendingFiles.Count > 0)
        {
            MessageBox.Show(
                "Primero actualizá el cliente.",
                "Actualización pendiente",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var wowExe = Path.Combine(_settings.GamePath, "Wow.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = wowExe,
            WorkingDirectory = _settings.GamePath,
            UseShellExecute = true
        });

        Close();
    }

    private void RefreshButtons()
    {
        var validGame = UpdateService.IsValidGameFolder(_settings.GamePath);
        UpdateButton.IsEnabled = validGame && _manifest is not null && _pendingFiles.Count > 0;
        PlayButton.IsEnabled = validGame && _manifest is not null && _pendingFiles.Count == 0;
    }

    private void SetBusy(bool busy)
    {
        SelectFolderButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy && _pendingFiles.Count > 0;
        PlayButton.IsEnabled = !busy && _pendingFiles.Count == 0;
    }
}
