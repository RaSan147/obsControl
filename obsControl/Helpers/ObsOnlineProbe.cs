using System;
using System.Net.Sockets;

namespace obsControl;

internal static class ObsOnlineProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Lightweight check: can we reach the OBS WebSocket TCP port?
    /// Does not authenticate.
    /// </summary>
    public static bool IsOnline(string hostUrl)
    {
        if (!TryParseEndpoint(hostUrl, out var host, out var port))
        {
            return false;
        }

        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            if (!connect.Wait(Timeout))
            {
                return false;
            }

            return connect.IsCompletedSuccessfully && client.Connected;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryParseEndpoint(string hostUrl, out string host, out int port)
    {
        host = "127.0.0.1";
        port = 4455;

        if (string.IsNullOrWhiteSpace(hostUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(hostUrl.Trim(), UriKind.Absolute, out var uri))
        {
            var parts = hostUrl.Split(':', 2, StringSplitOptions.TrimEntries);
            host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var parsed))
            {
                port = parsed;
            }

            return !string.IsNullOrWhiteSpace(host);
        }

        host = string.IsNullOrWhiteSpace(uri.Host) ? "127.0.0.1" : uri.Host;
        port = uri.IsDefaultPort ? 4455 : uri.Port;
        return true;
    }
}
