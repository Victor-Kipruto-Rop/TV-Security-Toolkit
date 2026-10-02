using TVSecurityToolkit.Engine;

namespace TVSecurityToolkit.Tests.Update.Integrity;

/// <summary>Steps for this test live in test-catalog/update/integrity.json.</summary>
public sealed class PackageIntegrityTest : CatalogTest
{
    public override string Id => "update.integrity.corrupted-package";
}
