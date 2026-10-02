using System.Net;
using System.Net.Http;
using System.Xml;
using System.Xml.Linq;

namespace TVSecurityToolkit.Device.PlatformAdapters;

public sealed class RokuEcpAdapter
{
    private const int MaxResponseCharacters = 64 * 1024;
    private readonly HttpClient? _client;

    public RokuEcpAdapter(HttpClient? client = null) => _client = client;

    public async Task<PlatformAdapterStatus> ProbeAsync(string host, CancellationToken ct)
    {
        if (!TryCreateDeviceInfoUri(host, out var uri))
            return new PlatformAdapterStatus("Roku TV", "ECP", "Enter a valid TV IP address or hostname; discovery does not scan the network.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        using var ownedClient = _client is null
            ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            : null;
        var client = _client ?? ownedClient!;

        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new PlatformAdapterStatus("Roku TV", "ECP", "The endpoint requires authorization; no identity or compatibility was established.");
            if (!response.IsSuccessStatusCode)
                return new PlatformAdapterStatus("Roku TV", "ECP", $"Endpoint returned HTTP {(int)response.StatusCode}; no identity or compatibility was established.");
            if (response.Content.Headers.ContentLength > MaxResponseCharacters)
                return new PlatformAdapterStatus("Roku TV", "ECP", "Device information response exceeded the size limit.");

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxResponseCharacters,
                Async = true
            });
            var document = await XDocument.LoadAsync(reader, LoadOptions.None, timeout.Token);
            var root = document.Root;
            if (root?.Name.LocalName != "device-info")
                return new PlatformAdapterStatus("Roku TV", "ECP", "Endpoint responded, but the device-information format was not recognized.");

            var model = SafeText(root.Elements().FirstOrDefault(e => e.Name.LocalName == "model-name")?.Value);
            var modelSummary = model.Length == 0 ? "Model not reported" : $"Reported model: {model}";
            return new PlatformAdapterStatus("Roku TV", "ECP",
                $"{modelSummary}. Device identity is not authenticated; diagnostic-test execution remains blocked.");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new PlatformAdapterStatus("Roku TV", "ECP", "Read-only device-info request timed out.");
        }
        catch (HttpRequestException)
        {
            return new PlatformAdapterStatus("Roku TV", "ECP", "Could not reach the read-only ECP device-info endpoint.");
        }
        catch (XmlException)
        {
            return new PlatformAdapterStatus("Roku TV", "ECP", "Endpoint returned invalid or unsupported XML.");
        }
    }

    public static bool TryCreateDeviceInfoUri(string host, out Uri uri)
    {
        uri = null!;
        var candidate = host.Trim();
        if (candidate.Length == 0 ||
            candidate.Contains('/') ||
            candidate.Contains('@') ||
            candidate.Contains('?') ||
            candidate.Contains('#'))
            return false;

        if (candidate.StartsWith("[", StringComparison.Ordinal) && candidate.EndsWith("]", StringComparison.Ordinal))
            candidate = candidate[1..^1];
        if (candidate.Contains(':') && !IPAddress.TryParse(candidate, out _))
            return false;
        if (!IPAddress.TryParse(candidate, out _) &&
            Uri.CheckHostName(candidate) is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            return false;

        try
        {
            uri = new UriBuilder(Uri.UriSchemeHttp, candidate, 8060, "/query/device-info").Uri;
            return true;
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string SafeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var safe = new string(value.Where(c => !char.IsControl(c)).Take(80).ToArray()).Trim();
        return safe;
    }
}
