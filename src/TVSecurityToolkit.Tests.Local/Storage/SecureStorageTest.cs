using TVSecurityToolkit.Engine;

namespace TVSecurityToolkit.Tests.Local.Storage;

/// <summary>Steps for this test live in test-catalog/local/storage.json.</summary>
public sealed class SecureStorageTest : CatalogTest
{
    public override string Id => "local.storage.sensitive-storage";
}
