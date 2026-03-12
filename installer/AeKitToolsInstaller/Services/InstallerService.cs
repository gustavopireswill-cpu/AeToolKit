using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;

namespace AeKitToolsInstaller.Services;

public sealed class InstallerService
{
    private const string ExtensionFolderName = "AeKitTools";
    private const string RepositoryUrl = "https://github.com/gustavopireswill-cpu/AeToolKit";
    private const string RepositoryZipUrl = RepositoryUrl + "/archive/refs/heads/main.zip";

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    private static readonly string[] PreferredCsxsVersions =
    [
        "11",
        "12",
        "13",
        "14",
        "15",
        "16"
    ];

    private static readonly string[] PayloadRelativePaths =
    [
        ".debug",
        "index.html",
        "CSXS/manifest.xml",
        "css/styles.css",
        "js/cep.js",
        "js/main.js",
        "jsx/hostscript.jsx"
    ];

    private static readonly HashSet<string> PayloadRelativePathSet = new(PayloadRelativePaths, StringComparer.OrdinalIgnoreCase);

    private readonly Assembly assembly = typeof(InstallerService).Assembly;

    public string PayloadSummaryText => "GitHub sync + fallback local";

    public EnvironmentSnapshot InspectEnvironment()
    {
        string extensionsRoot = GetExtensionsRoot();
        string installPath = Path.Combine(extensionsRoot, ExtensionFolderName);

        return new EnvironmentSnapshot(
            InstallPath: installPath,
            ExtensionsRoot: extensionsRoot,
            AfterEffectsInstallations: DetectAfterEffectsInstallations(),
            ExtensionAlreadyInstalled: Directory.Exists(installPath),
            AfterEffectsRunning: Process.GetProcessesByName("AfterFX").Length > 0,
            PayloadFileCount: PayloadRelativePaths.Length);
    }

    public async Task<InstallResult> InstallAsync(bool enableDebugMode, CancellationToken cancellationToken = default)
    {
        EnvironmentSnapshot snapshot = InspectEnvironment();
        string stagingPath = Path.Combine(Path.GetTempPath(), $"AeKitTools-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingPath);
            InstallSource installSource = await PopulatePayloadAsync(stagingPath, cancellationToken);

            Directory.CreateDirectory(snapshot.ExtensionsRoot);
            if (Directory.Exists(snapshot.InstallPath))
            {
                Directory.Delete(snapshot.InstallPath, recursive: true);
            }

            Directory.Move(stagingPath, snapshot.InstallPath);
            stagingPath = string.Empty;

            if (enableDebugMode)
            {
                EnablePlayerDebugMode();
            }

            string message = installSource switch
            {
                InstallSource.GitHub => "A extensao foi baixada do GitHub e instalada no CEP global do Windows. Reinicie o After Effects se ele ja estiver aberto.",
                InstallSource.EmbeddedFallback => "O download do GitHub falhou, entao o instalador usou a copia local embarcada e concluiu a instalacao.",
                _ => "A extensao foi instalada."
            };

            return new InstallResult(
                Success: true,
                Title: "Instalacao concluida",
                Message: message,
                InstallPath: snapshot.InstallPath,
                FilesWritten: PayloadRelativePaths.Length,
                DebugModeEnabled: enableDebugMode,
                SourceLabel: installSource == InstallSource.GitHub ? "GitHub" : "Pacote local");
        }
        catch (Exception ex)
        {
            return new InstallResult(
                Success: false,
                Title: "Falha na instalacao",
                Message: ex.Message,
                InstallPath: snapshot.InstallPath,
                FilesWritten: 0,
                DebugModeEnabled: false,
                SourceLabel: "Erro");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(stagingPath) && Directory.Exists(stagingPath))
            {
                try
                {
                    Directory.Delete(stagingPath, recursive: true);
                }
                catch
                {
                    // Ignora limpeza temporaria.
                }
            }
        }
    }

    private static string GetExtensionsRoot()
    {
        string commonFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86);
        if (string.IsNullOrWhiteSpace(commonFilesX86))
        {
            commonFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles);
        }

        return Path.Combine(commonFilesX86, "Adobe", "CEP", "extensions");
    }

    private async Task<InstallSource> PopulatePayloadAsync(string targetRoot, CancellationToken cancellationToken)
    {
        try
        {
            await DownloadAndExtractGitHubPayloadAsync(targetRoot, cancellationToken);
            return InstallSource.GitHub;
        }
        catch
        {
            IReadOnlyList<PayloadResource> fallbackResources = GetPayloadResources();
            if (fallbackResources.Count == 0)
            {
                throw;
            }

            await ExtractEmbeddedPayloadAsync(targetRoot, fallbackResources, cancellationToken);
            return InstallSource.EmbeddedFallback;
        }
    }

    private async Task DownloadAndExtractGitHubPayloadAsync(string targetRoot, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await HttpClient.GetAsync(
            RepositoryZipUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using Stream zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Read, leaveOpen: false);

        int extractedCount = 0;

        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string? relativePath = TryMapZipEntryToPayload(entry.FullName);
            if (relativePath is null || string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            string targetPath = Path.Combine(targetRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            await using Stream sourceStream = entry.Open();
            await using FileStream destinationStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            extractedCount++;
        }

        if (extractedCount < PayloadRelativePaths.Length)
        {
            throw new InvalidOperationException("O pacote do GitHub nao trouxe todos os arquivos esperados da extensao.");
        }
    }

    private async Task ExtractEmbeddedPayloadAsync(
        string targetRoot,
        IReadOnlyList<PayloadResource> payloadResources,
        CancellationToken cancellationToken)
    {
        foreach (PayloadResource payloadResource in payloadResources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string targetPath = Path.Combine(
                targetRoot,
                payloadResource.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            string? targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            await using Stream sourceStream = assembly.GetManifestResourceStream(payloadResource.ResourceName)
                ?? throw new InvalidOperationException($"Nao foi possivel localizar o recurso {payloadResource.ResourceName}.");

            await using FileStream destinationStream = File.Create(targetPath);
            await sourceStream.CopyToAsync(destinationStream, cancellationToken);
        }
    }

    private IReadOnlyList<string> DetectAfterEffectsInstallations()
    {
        HashSet<string> installations = new(StringComparer.OrdinalIgnoreCase);

        foreach (string? programFilesPath in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
        })
        {
            if (string.IsNullOrWhiteSpace(programFilesPath))
            {
                continue;
            }

            string adobeRoot = Path.Combine(programFilesPath, "Adobe");
            if (!Directory.Exists(adobeRoot))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateDirectories(adobeRoot, "Adobe After Effects*"))
            {
                installations.Add(Path.GetFileName(path));
            }
        }

        return installations
            .OrderByDescending(ExtractSortKey)
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void EnablePlayerDebugMode()
    {
        HashSet<string> registryPaths = new(StringComparer.OrdinalIgnoreCase);

        foreach (string version in PreferredCsxsVersions)
        {
            registryPaths.Add($@"Software\Adobe\CSXS.{version}");
        }

        using RegistryKey? adobeKey = Registry.CurrentUser.CreateSubKey(@"Software\Adobe");
        if (adobeKey is not null)
        {
            foreach (string subKeyName in adobeKey.GetSubKeyNames().Where(name => Regex.IsMatch(name, @"^CSXS\.\d+$")))
            {
                registryPaths.Add($@"Software\Adobe\{subKeyName}");
            }
        }

        foreach (string registryPath in registryPaths)
        {
            using RegistryKey? key = Registry.CurrentUser.CreateSubKey(registryPath);
            key?.SetValue("PlayerDebugMode", "1", RegistryValueKind.String);
        }
    }

    private IReadOnlyList<PayloadResource> GetPayloadResources()
    {
        return assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith("Payload/", StringComparison.Ordinal))
            .Select(name => new PayloadResource(name, name["Payload/".Length..]))
            .Where(resource => PayloadRelativePathSet.Contains(resource.RelativePath))
            .OrderBy(resource => resource.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? TryMapZipEntryToPayload(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        string normalizedPath = fullName.Replace('\\', '/');
        string[] segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return null;
        }

        string relativePath = string.Join('/', segments.Skip(1));
        return PayloadRelativePathSet.Contains(relativePath)
            ? relativePath
            : null;
    }

    private static int ExtractSortKey(string name)
    {
        Match match = Regex.Match(name, @"(\d{4})");
        return match.Success && int.TryParse(match.Value, out int numericKey)
            ? numericKey
            : 0;
    }

    private enum InstallSource
    {
        GitHub,
        EmbeddedFallback
    }

    private sealed record PayloadResource(string ResourceName, string RelativePath);
}

public sealed record EnvironmentSnapshot(
    string InstallPath,
    string ExtensionsRoot,
    IReadOnlyList<string> AfterEffectsInstallations,
    bool ExtensionAlreadyInstalled,
    bool AfterEffectsRunning,
    int PayloadFileCount);

public sealed record InstallResult(
    bool Success,
    string Title,
    string Message,
    string InstallPath,
    int FilesWritten,
    bool DebugModeEnabled,
    string SourceLabel);
