using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using TVSecurityToolkit.Core.Exceptions;
using TVSecurityToolkit.Core.Interfaces;
using TVSecurityToolkit.Core.Models;

namespace TVSecurityToolkit.Engine;

/// <summary>Holds catalog definitions (JSON) and the ISecurityTest classes discovered in test assemblies.</summary>
public sealed class TestRegistry
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, TestDefinition> _defs = new();
    private readonly Dictionary<string, ISecurityTest> _tests = new();

    public IReadOnlyCollection<TestDefinition> Definitions => _defs.Values;
    public IReadOnlyCollection<ISecurityTest> Tests => _tests.Values;

    public int LoadCatalog(string dir)
    {
        if (!Directory.Exists(dir)) throw new ValidationException("test catalog not found: " + dir);
        var n = 0;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var cf = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(file), Opts)
                     ?? throw new ValidationException("empty catalog file: " + file);
            foreach (var t in cf.Tests)
            {
                if (string.IsNullOrEmpty(t.Category)) t.Category = cf.Category;
                if (string.IsNullOrEmpty(t.Group)) t.Group = cf.Group;
                _defs[t.Id] = t; n++;
            }
        }
        return n;
    }

    public int RegisterAssembly(Assembly assembly)
    {
        var n = 0;
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ISecurityTest).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null) continue;
            var test = (ISecurityTest)Activator.CreateInstance(type)!;
            if (_tests.TryAdd(test.Id, test)) n++;
        }
        return n;
    }

    public bool TryGet(string id, out TestDefinition def) => _defs.TryGetValue(id, out def!);
    public TestDefinition Get(string id) =>
        _defs.TryGetValue(id, out var d) ? d : throw new ValidationException("no catalog definition for test " + id);

    /// <summary>Adds definitions directly. Used by tests that need a small, explicit catalog.</summary>
    public void LoadDefinitions(IEnumerable<TestDefinition> definitions)
    {
        foreach (var d in definitions) _defs[d.Id] = d;
    }
}
