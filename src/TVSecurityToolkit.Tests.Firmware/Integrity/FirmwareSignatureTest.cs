using TVSecurityToolkit.Engine;

namespace TVSecurityToolkit.Tests.Firmware.Integrity;

/// <summary>Steps for this test live in test-catalog/firmware/integrity.json.</summary>
public sealed class FirmwareSignatureTest : CatalogTest
{
    public override string Id => "firmware.integrity.signature-validation";
}
