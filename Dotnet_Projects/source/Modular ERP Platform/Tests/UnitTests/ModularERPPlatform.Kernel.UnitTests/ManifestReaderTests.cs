using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;

namespace ModularERPPlatform.Kernel.UnitTests;

[TestFixture]
public sealed class ManifestReaderTests
{
    private string _directory = string.Empty;
    private ManifestReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"manifest-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _reader = new ManifestReader();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<ManifestReadResult> ReadAsync(string json)
    {
        string path = Path.Combine(_directory, "module.manifest.json");
        await File.WriteAllTextAsync(path, json);
        return await _reader.ReadAsync(path);
    }

    [Test]
    public async Task Valid_manifest_is_accepted()
    {
        ManifestReadResult result = await ReadAsync("""
        {
          "id": "finance",
          "displayName": "Finance",
          "version": "1.2.3",
          "assemblyFile": "ModularERP.Finance.dll",
          "requiredKernelVersion": ">=1.0.0 <2.0.0",
          "dependencies": [ { "id": "shared", "versionRange": ">=1.0.0" } ],
          "requiredPermissions": [ "Finance.Post" ]
        }
        """);

        result.IsAccepted.Should().BeTrue();
        result.Manifest!.Id.Value.Should().Be("finance");
        result.Manifest.Version.Should().Be(new Version(1, 2, 3));
        result.Manifest.Dependencies.Should().ContainSingle();
        result.Manifest.RequiredPermissions.Should().ContainSingle();
    }

    [Test]
    public async Task Missing_assembly_file_is_rejected()
    {
        ManifestReadResult result = await ReadAsync("""
        { "id": "finance", "version": "1.0.0" }
        """);

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("assemblyFile is required");
    }

    [Test]
    public async Task Assembly_path_instead_of_file_name_is_rejected()
    {
        ManifestReadResult result = await ReadAsync("""
        { "id": "finance", "version": "1.0.0", "assemblyFile": "..\\..\\evil.dll" }
        """);

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("must be a file name, not a path");
    }

    [Test]
    public async Task Self_dependency_is_rejected()
    {
        ManifestReadResult result = await ReadAsync("""
        {
          "id": "finance",
          "version": "1.0.0",
          "assemblyFile": "f.dll",
          "dependencies": [ { "id": "finance", "versionRange": ">=1.0.0" } ]
        }
        """);

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("dependency on itself");
    }

    [Test]
    public async Task Duplicate_dependency_is_rejected()
    {
        ManifestReadResult result = await ReadAsync("""
        {
          "id": "finance",
          "version": "1.0.0",
          "assemblyFile": "f.dll",
          "dependencies": [
            { "id": "shared", "versionRange": ">=1.0.0" },
            { "id": "shared", "versionRange": ">=2.0.0" }
          ]
        }
        """);

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("declared more than once");
    }

    [Test]
    public async Task Malformed_json_is_rejected_without_throwing()
    {
        ManifestReadResult result = await ReadAsync("{ not json");

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("not valid JSON");
    }

    [Test]
    public async Task Absent_file_is_rejected()
    {
        ManifestReadResult result = await _reader.ReadAsync(
            Path.Combine(_directory, "does-not-exist.json"));

        result.IsAccepted.Should().BeFalse();
        result.RejectionReason.Should().Contain("not found");
    }
}
