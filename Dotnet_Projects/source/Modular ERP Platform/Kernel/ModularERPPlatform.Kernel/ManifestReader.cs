using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Reads and validates <c>module.manifest.json</c> files. A manifest that fails
/// validation is rejected with a stated reason and its module never loads. Satisfies FR-002.
/// </summary>
public sealed class ManifestReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Reads a manifest from disk.</summary>
    /// <param name="manifestPath">Full path to the manifest file.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parsed manifest, or the reason it was rejected.</returns>
    public static async Task<ManifestReadResult> ReadAsync(
        string manifestPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);

        if (!File.Exists(manifestPath))
        {
            return ManifestReadResult.Rejected($"Manifest not found at '{manifestPath}'.");
        }

        ManifestDocument? document;

        try
        {
            using FileStream stream = File.OpenRead(manifestPath);
            document = await JsonSerializer
                .DeserializeAsync<ManifestDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}' is not valid JSON: {ex.Message}");
        }
        catch (IOException ex)
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}' could not be read: {ex.Message}");
        }

        if (document is null)
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}' is empty.");
        }

        return Validate(document, manifestPath);
    }

    private static ManifestReadResult Validate(ManifestDocument document, string manifestPath)
    {
        if (!ModuleId.TryCreate(document.Id, out ModuleId id, out string? idError))
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}': {idError}");
        }

        if (!Version.TryParse(document.Version, out Version? version))
        {
            return ManifestReadResult.Rejected(
                $"Manifest '{manifestPath}': version '{document.Version}' is not a valid version.");
        }

        if (string.IsNullOrWhiteSpace(document.AssemblyFile))
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}': assemblyFile is required.");
        }

        if (Path.GetFileName(document.AssemblyFile) != document.AssemblyFile)
        {
            return ManifestReadResult.Rejected(
                $"Manifest '{manifestPath}': assemblyFile must be a file name, not a path.");
        }

        if (!VersionRange.TryParse(
                document.RequiredKernelVersion,
                out VersionRange kernelRange,
                out string? kernelError))
        {
            return ManifestReadResult.Rejected($"Manifest '{manifestPath}': {kernelError}");
        }

        List<ModuleDependency> dependencies = new();

        foreach (DependencyDocument dependency in document.Dependencies ?? [])
        {
            if (!ModuleId.TryCreate(dependency.Id, out ModuleId dependencyId, out string? dependencyIdError))
            {
                return ManifestReadResult.Rejected($"Manifest '{manifestPath}': {dependencyIdError}");
            }

            if (!VersionRange.TryParse(
                    dependency.VersionRange,
                    out VersionRange range,
                    out string? rangeError))
            {
                return ManifestReadResult.Rejected($"Manifest '{manifestPath}': {rangeError}");
            }

            if (dependencyId == id)
            {
                return ManifestReadResult.Rejected(
                    $"Manifest '{manifestPath}': module '{id}' declares a dependency on itself.");
            }

            if (dependencies.Any(d => d.Id == dependencyId))
            {
                return ManifestReadResult.Rejected(
                    $"Manifest '{manifestPath}': dependency '{dependencyId}' is declared more than once.");
            }

            dependencies.Add(new ModuleDependency(dependencyId, range));
        }

        List<Permission> permissions = new();

        foreach (string permission in document.RequiredPermissions ?? [])
        {
            if (string.IsNullOrWhiteSpace(permission))
            {
                return ManifestReadResult.Rejected(
                    $"Manifest '{manifestPath}': a declared permission is empty.");
            }

            permissions.Add(Permission.Create(permission));
        }

        string displayName = string.IsNullOrWhiteSpace(document.DisplayName)
            ? id.Value
            : document.DisplayName;

        ModuleManifest manifest = new(
            id,
            version,
            displayName,
            document.AssemblyFile,
            kernelRange,
            dependencies,
            permissions);

        return ManifestReadResult.Accepted(manifest);
    }

    private sealed class ManifestDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("version")]
        public string? Version { get; init; }

        [JsonPropertyName("assemblyFile")]
        public string? AssemblyFile { get; init; }

        [JsonPropertyName("requiredKernelVersion")]
        public string? RequiredKernelVersion { get; init; }

        [JsonPropertyName("dependencies")]
        public IReadOnlyList<DependencyDocument>? Dependencies { get; init; }

        [JsonPropertyName("requiredPermissions")]
        public IReadOnlyList<string>? RequiredPermissions { get; init; }
    }

    private sealed class DependencyDocument
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("versionRange")]
        public string? VersionRange { get; init; }
    }
}

/// <summary>
/// The outcome of reading a manifest: either a manifest, or the reason there isn't one.
/// </summary>
public sealed class ManifestReadResult
{
    private ManifestReadResult(ModuleManifest? manifest, string? rejectionReason)
    {
        Manifest = manifest;
        RejectionReason = rejectionReason;
    }

    /// <summary>Gets the parsed manifest when reading succeeded.</summary>
    public ModuleManifest? Manifest { get; }

    /// <summary>Gets the reason the manifest was rejected, when it was.</summary>
    public string? RejectionReason { get; }

    /// <summary>Gets a value indicating whether a usable manifest was produced.</summary>
    public bool IsAccepted => Manifest is not null;

    /// <summary>Creates an accepted result.</summary>
    /// <param name="manifest">The parsed manifest.</param>
    /// <returns>An accepted result.</returns>
    public static ManifestReadResult Accepted(ModuleManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return new ManifestReadResult(manifest, null);
    }

    /// <summary>Creates a rejected result.</summary>
    /// <param name="reason">Why the manifest was rejected.</param>
    /// <returns>A rejected result.</returns>
    public static ManifestReadResult Rejected(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new ManifestReadResult(null, reason);
    }
}
