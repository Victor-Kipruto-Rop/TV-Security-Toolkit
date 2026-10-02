using System.ComponentModel;
using System.Diagnostics;

namespace TVSecurityToolkit.Device.PlatformAdapters;

public sealed class PlatformAdapterDiscovery
{
    private static readonly (string Platform, string Tool, string[] Arguments)[] Tools =
    {
        ("Android TV / Google TV", "adb", new[] { "devices", "-l" }),
        ("Samsung Tizen", "sdb", new[] { "devices" }),
        ("LG webOS", "ares-device", new[] { "--list" })
    };

    public async Task<IReadOnlyList<PlatformAdapterStatus>> DiscoverAsync(CancellationToken ct)
    {
        return await Task.WhenAll(Tools.Select(tool => ProbeAsync(tool.Platform, tool.Tool, tool.Arguments, ct)));
    }

    public static string SummarizeDeviceList(string platform, string output)
    {
        if (platform == "LG webOS")
            return string.IsNullOrWhiteSpace(output)
                ? "Tool returned no devices. TV identity is not authenticated."
                : "Vendor tool returned output; device-list format is not validated and TV identity is not authenticated.";

        var connected = 0;
        var unauthorized = 0;
        var other = 0;
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var columns = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length < 2 || columns[0].Equals("List", StringComparison.OrdinalIgnoreCase)) continue;
            if (columns[1].Equals("device", StringComparison.OrdinalIgnoreCase)) connected++;
            else if (columns[1].Equals("unauthorized", StringComparison.OrdinalIgnoreCase)) unauthorized++;
            else if (columns[1].Equals("offline", StringComparison.OrdinalIgnoreCase)) other++;
        }

        if (connected > 0)
            return $"{connected} connected candidate(s); TV identity is not authenticated.";
        if (unauthorized > 0 || other > 0)
            return $"{unauthorized + other} candidate(s) require vendor-tool authorization or are offline; identity is not authenticated.";
        return "No devices reported. TV identity is not authenticated.";
    }

    private static async Task<PlatformAdapterStatus> ProbeAsync(
        string platform, string tool, string[] arguments, CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = tool,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);

        try
        {
            if (!process.Start())
                return new PlatformAdapterStatus(platform, tool, "Could not start vendor tool.");
        }
        catch (Win32Exception)
        {
            return new PlatformAdapterStatus(platform, tool,
                $"Not installed or not available on PATH ({tool}). Install the official vendor tool and ensure it is on PATH.");
        }
        catch (InvalidOperationException)
        {
            return new PlatformAdapterStatus(platform, tool, "Could not start vendor tool.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var stdout = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stderr = process.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            _ = await stderr;
            if (process.ExitCode != 0)
                return new PlatformAdapterStatus(platform, tool, $"Vendor tool exited with code {process.ExitCode}; no device compatibility was established.");
            return new PlatformAdapterStatus(platform, tool, SummarizeDeviceList(platform, output));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            return new PlatformAdapterStatus(platform, tool, "Vendor tool timed out; no device compatibility was established.");
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            throw;
        }
    }
}
