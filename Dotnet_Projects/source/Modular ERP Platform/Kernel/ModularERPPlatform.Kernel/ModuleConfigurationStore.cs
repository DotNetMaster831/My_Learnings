using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ModularERPPlatform.Kernel.Contracts;

namespace ModularERPPlatform.Kernel;

/// <summary>
/// Owns every module's configuration and hands each module an instance bound to itself.
/// A module has no API by which to name another module's store. Satisfies FR-010 and PC-004.
/// </summary>
public sealed class ModuleConfigurationStore
{
    private readonly KernelOptions _options;
    private readonly ConcurrentDictionary<ModuleId, ModuleConfiguration> _configurations = new();

    /// <summary>Initialises the configuration store.</summary>
    /// <param name="options">Kernel options.</param>
    public ModuleConfigurationStore(IOptions<KernelOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;

        Directory.CreateDirectory(_options.ConfigurationDirectory);
    }

    /// <summary>Gets the configuration scoped to one module, creating it on first use.</summary>
    /// <param name="moduleId">The owning module.</param>
    /// <returns>Configuration scoped to that module alone.</returns>
    public IModuleConfiguration For(ModuleId moduleId)
    {
        if (moduleId.IsEmpty)
        {
            throw new ArgumentException("Module id must be initialised.", nameof(moduleId));
        }

        return _configurations.GetOrAdd(
            moduleId,
            static (id, options) => new ModuleConfiguration(
                id,
                Path.Combine(options.ConfigurationDirectory, $"{id.Value}.json")),
            _options);
    }
}

/// <summary>
/// Configuration for a single module, persisted as one JSON file per module.
/// </summary>
internal sealed class ModuleConfiguration : IModuleConfiguration, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    internal ModuleConfiguration(ModuleId owner, string filePath)
    {
        Owner = owner;
        _filePath = filePath;
    }

    public ModuleId Owner { get; }

    public void Dispose()
    {
        _gate.Dispose();
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Dictionary<string, JsonElement> values = await ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!values.TryGetValue(key, out JsonElement element))
            {
                return default;
            }

            return element.Deserialize<T>(SerializerOptions);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Dictionary<string, JsonElement> values = await ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            values[key] = JsonSerializer.SerializeToElement(value, SerializerOptions);

            await WriteAsync(values, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Dictionary<string, JsonElement> values = await ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!values.Remove(key))
            {
                return false;
            }

            await WriteAsync(values, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<string>> GetKeysAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Dictionary<string, JsonElement> values = await ReadAsync(cancellationToken)
                .ConfigureAwait(false);

            return values.Keys.ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, JsonElement>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        using FileStream stream = File.OpenRead(_filePath);

        Dictionary<string, JsonElement>? values = await JsonSerializer
            .DeserializeAsync<Dictionary<string, JsonElement>>(
                stream,
                SerializerOptions,
                cancellationToken)
            .ConfigureAwait(false);

        return values ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
    }

    private async Task WriteAsync(
        Dictionary<string, JsonElement> values,
        CancellationToken cancellationToken)
    {
        string temporaryPath = _filePath + ".tmp";

        using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer
                .SerializeAsync(stream, values, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
