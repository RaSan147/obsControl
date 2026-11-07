// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace obsControl;

internal sealed partial class SettingsManager : JsonSettingsManager
{
    private static readonly string Namespace = "obsControl";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly TextSetting _host = new(
        Namespaced(nameof(Host)),
        "WebSocket URL",
        "OBS WebSocket URL, for example ws://127.0.0.1:4455",
        "ws://127.0.0.1:4455");

    private readonly TextSetting _password = new(
        Namespaced(nameof(Password)),
        "WebSocket password",
        "Password from OBS → Settings → WebSocket (leave blank if auth is disabled)",
        string.Empty)
    {
        Placeholder = "Enter OBS WebSocket password",
    };

    private readonly ToggleSetting _showReplayBuffer = new(
        Namespaced(nameof(ShowReplayBuffer)),
        "Show replay buffer",
        "Show replay buffer controls in OBS",
        false);

    public string Host => string.IsNullOrWhiteSpace(_host.Value) ? "ws://127.0.0.1:4455" : _host.Value.Trim();

    public string Password => _password.Value ?? string.Empty;

    public bool HasPassword => !string.IsNullOrEmpty(Password);

    public bool ShowReplayBuffer => _showReplayBuffer.Value;

    public void SetConnection(string host, string password)
    {
        _host.Value = string.IsNullOrWhiteSpace(host) ? "ws://127.0.0.1:4455" : host.Trim();
        _password.Value = password ?? string.Empty;
        SaveSettings();
    }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("obsControl");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_host);
        Settings.Add(_password);
        Settings.Add(_showReplayBuffer);

        LoadSettings();
        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
