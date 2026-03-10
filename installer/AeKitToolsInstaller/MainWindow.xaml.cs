using AeKitToolsInstaller.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AeKitToolsInstaller;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly InstallerService installerService = new();
    private readonly bool launchInstallOnLoad;
    private EnvironmentSnapshot? snapshot;
    private bool enableDebugMode;
    private bool isInstalling;
    private string detectionSummary = "Analisando o sistema...";
    private string environmentNote = string.Empty;
    private string installPath = string.Empty;
    private string installationBadgeText = "Instalacao global";
    private string payloadSummary = "Atualizacao via GitHub";
    private string statusMessage = "Tudo pronto para instalar a extensao.";
    private string statusTitle = "Pronto";
    private string statusTone = "neutral";

    public MainWindow()
    {
        string[] args = Environment.GetCommandLineArgs();
        launchInstallOnLoad = args.Any(arg => arg.Equals("--elevated-install", StringComparison.OrdinalIgnoreCase));
        enableDebugMode = !args.Any(arg => arg.Equals("--disable-debug", StringComparison.OrdinalIgnoreCase));

        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AfterEffectsVersions { get; } = [];

    public string DetectionSummary
    {
        get => detectionSummary;
        private set => SetField(ref detectionSummary, value);
    }

    public bool EnableDebugMode
    {
        get => enableDebugMode;
        set => SetField(ref enableDebugMode, value);
    }

    public string EnvironmentNote
    {
        get => environmentNote;
        private set
        {
            if (SetField(ref environmentNote, value))
            {
                OnPropertyChanged(nameof(ShowEnvironmentNote));
            }
        }
    }

    public string InstallButtonText
    {
        get
        {
            if (IsInstalling)
            {
                return "Instalando...";
            }

            if (!IsRunAsAdministrator())
            {
                return snapshot?.ExtensionAlreadyInstalled == true
                    ? "Reinstalar como administrador"
                    : "Instalar como administrador";
            }

            return snapshot?.ExtensionAlreadyInstalled == true
                ? "Reinstalar extensao"
                : "Instalar extensao";
        }
    }

    public string InstallPath
    {
        get => installPath;
        private set => SetField(ref installPath, value);
    }

    public string InstallationBadgeText
    {
        get => installationBadgeText;
        private set => SetField(ref installationBadgeText, value);
    }

    public bool IsInstalling
    {
        get => isInstalling;
        private set
        {
            if (SetField(ref isInstalling, value))
            {
                OnPropertyChanged(nameof(InstallButtonText));
                OnPropertyChanged(nameof(IsReadyToInstall));
            }
        }
    }

    public bool IsReadyToInstall => !IsInstalling;

    public string PayloadSummary
    {
        get => payloadSummary;
        private set => SetField(ref payloadSummary, value);
    }

    public bool ShowEnvironmentNote => !string.IsNullOrWhiteSpace(EnvironmentNote);

    public string StatusMessage
    {
        get => statusMessage;
        private set => SetField(ref statusMessage, value);
    }

    public string StatusTitle
    {
        get => statusTitle;
        private set => SetField(ref statusTitle, value);
    }

    public string StatusTone
    {
        get => statusTone;
        private set => SetField(ref statusTone, value);
    }

    private void ApplySnapshot(EnvironmentSnapshot environmentSnapshot)
    {
        snapshot = environmentSnapshot;
        InstallPath = environmentSnapshot.InstallPath;
        DetectionSummary = environmentSnapshot.AfterEffectsInstallations.Count switch
        {
            0 => "Nenhuma instalacao detectada",
            1 => "1 instalacao detectada",
            _ => $"{environmentSnapshot.AfterEffectsInstallations.Count} instalacoes detectadas"
        };

        InstallationBadgeText = environmentSnapshot.ExtensionAlreadyInstalled
            ? "Atualizacao global"
            : "Instalacao global";

        PayloadSummary = installerService.PayloadSummaryText;
        EnvironmentNote = BuildEnvironmentNote(environmentSnapshot);

        AfterEffectsVersions.Clear();
        foreach (string version in environmentSnapshot.AfterEffectsInstallations)
        {
            AfterEffectsVersions.Add(version);
        }

        OnPropertyChanged(nameof(InstallButtonText));
    }

    private string BuildEnvironmentNote(EnvironmentSnapshot environmentSnapshot)
    {
        if (!IsRunAsAdministrator() && environmentSnapshot.AfterEffectsRunning)
        {
            return "Ao clicar em instalar, o Windows vai pedir permissao de administrador. Depois, reinicie o After Effects para recarregar o painel global.";
        }

        if (!IsRunAsAdministrator())
        {
            return "A instalacao grava em Program Files (CEP global). Clique em instalar e confirme o UAC para continuar.";
        }

        if (environmentSnapshot.AfterEffectsRunning)
        {
            return "After Effects esta em execucao. Reinicie o aplicativo depois da instalacao para recarregar o painel.";
        }

        return "O painel sera instalado para todos os usuarios em Common Files\\Adobe\\CEP\\extensions.";
    }

    private void AnimateEntrance()
    {
        AnimateElement(HeroPanel, delayMilliseconds: 0, offsetY: 18);
        AnimateElement(ActionPanel, delayMilliseconds: 90, offsetY: 24);
        AnimateElement(FooterPanel, delayMilliseconds: 170, offsetY: 22);
    }

    private static void AnimateElement(UIElement element, int delayMilliseconds, double offsetY)
    {
        TranslateTransform transform = new(0, offsetY);
        element.RenderTransform = transform;
        element.Opacity = 0;

        Duration duration = new(TimeSpan.FromMilliseconds(700));
        CubicEase easing = new() { EasingMode = EasingMode.EaseOut };

        DoubleAnimation opacityAnimation = new(0, 1, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds)
        };

        DoubleAnimation moveAnimation = new(offsetY, 0, duration)
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds),
            EasingFunction = easing
        };

        element.BeginAnimation(OpacityProperty, opacityAnimation);
        transform.BeginAnimation(TranslateTransform.YProperty, moveAnimation);
    }

    private static bool IsRunAsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static void OpenFolderInExplorer(string path)
    {
        string resolvedPath = Directory.Exists(path)
            ? path
            : Path.GetDirectoryName(path) ?? path;

        if (!Directory.Exists(resolvedPath))
        {
            resolvedPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{resolvedPath}\"")
        {
            UseShellExecute = true
        });
    }

    private async Task BeginInstallationAsync()
    {
        if (snapshot is null || IsInstalling)
        {
            return;
        }

        IsInstalling = true;
        SetStatus(
            title: "Instalando",
            message: "Baixando a extensao do GitHub e preparando o CEP global do Windows.",
            tone: "working");

        try
        {
            InstallResult result = await installerService.InstallAsync(EnableDebugMode);
            ApplySnapshot(installerService.InspectEnvironment());

            string resultMessage = result.Success
                ? $"{result.Message} Fonte: {result.SourceLabel}."
                : result.Message;

            SetStatus(
                title: result.Title,
                message: resultMessage,
                tone: result.Success ? "success" : "error");
        }
        catch (Exception ex)
        {
            SetStatus(
                title: "Falha na instalacao",
                message: ex.Message,
                tone: "error");
        }
        finally
        {
            IsInstalling = false;
        }
    }

    private bool TryRelaunchElevatedInstall()
    {
        try
        {
            string processPath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName
                ?? throw new InvalidOperationException("Nao foi possivel localizar o executavel atual.");

            string arguments = EnableDebugMode
                ? "--elevated-install"
                : "--elevated-install --disable-debug";

            Process.Start(new ProcessStartInfo(processPath, arguments)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            });

            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            SetStatus(
                title: "Permissao cancelada",
                message: "O Windows nao recebeu autorizacao para executar o instalador como administrador.",
                tone: "error");

            return false;
        }
    }

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (snapshot is null || IsInstalling)
        {
            return;
        }

        if (!IsRunAsAdministrator())
        {
            SetStatus(
                title: "Solicitando permissao",
                message: "Confirme o UAC do Windows para instalar a extensao no CEP global.",
                tone: "working");

            if (TryRelaunchElevatedInstall())
            {
                Close();
            }

            return;
        }

        await BeginInstallationAsync();
    }

    private void OpenExtensionsRootButton_Click(object sender, RoutedEventArgs e)
    {
        string rootPath = snapshot?.ExtensionsRoot
            ?? installerService.InspectEnvironment().ExtensionsRoot;

        OpenFolderInExplorer(rootPath);
    }

    private void OpenInstallPathButton_Click(object sender, RoutedEventArgs e)
    {
        string targetPath = snapshot?.InstallPath
            ?? installerService.InspectEnvironment().InstallPath;

        OpenFolderInExplorer(targetPath);
    }

    private void SetStatus(string title, string message, string tone)
    {
        StatusTitle = title;
        StatusMessage = message;
        StatusTone = tone;
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged(string? propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ApplySnapshot(installerService.InspectEnvironment());
        SetStatus(
            title: "Pronto",
            message: "Clique para baixar a extensao do GitHub e instalar no CEP global do Windows.",
            tone: "neutral");

        AnimateEntrance();

        if (launchInstallOnLoad)
        {
            await BeginInstallationAsync();
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
