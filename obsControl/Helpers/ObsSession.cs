using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OBSWebsocketDotNet;
using OBSWebsocketDotNet.Communication;
using OBSWebsocketDotNet.Types;
using OBSWebsocketDotNet.Types.Events;

namespace obsControl;

internal sealed partial class ObsSession : IDisposable
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    private readonly SettingsManager _settings;
    private readonly OBSWebsocket _obs = new();
    private readonly object _sync = new();
    private readonly Timer _pollTimer;
    private bool _disposed;
    private int _connectGate; // 0 = idle, 1 = connect in flight

    public ObsSession(SettingsManager settings)
    {
        _settings = settings;

        _obs.Connected += OnConnected;
        _obs.Disconnected += OnDisconnected;
        _obs.RecordStateChanged += OnRecordStateChanged;
        _obs.StreamStateChanged += OnStreamStateChanged;
        _obs.VirtualcamStateChanged += OnVirtualCamStateChanged;
        _obs.ReplayBufferStateChanged += OnReplayBufferStateChanged;
        _obs.CurrentProgramSceneChanged += OnProgramSceneChanged;
        _obs.SceneListChanged += OnSceneListChanged;

        // Probe OBS online every few seconds; only authenticate when the port is up.
        _pollTimer = new Timer(
            _ => PollOnline(),
            null,
            TimeSpan.Zero,
            PollInterval);
    }

    public bool IsObsOnline { get; private set; }

    public event EventHandler? Changed;

    public bool IsConnected { get; private set; }

    public bool IsLoading { get; private set; }

    public string? LastError { get; private set; }

    public bool IsRecording { get; private set; }

    public bool IsRecordingPaused { get; private set; }

    public bool IsStreaming { get; private set; }

    public bool IsVirtualCamActive { get; private set; }

    public bool IsReplayBufferActive { get; private set; }

    public string CurrentScene { get; private set; } = string.Empty;

    public IReadOnlyList<string> Scenes { get; private set; } = [];

    /// <summary>Manual reconnect — drops any existing socket first.</summary>
    public void Connect() => BeginConnect(force: true);

    public void LaunchObs()
    {
        if (!ObsLauncher.TryLaunch(out var error))
        {
            LastError = error;
            RaiseChanged();
            return;
        }

        BeginConnect(force: true);
    }

    public void ToggleRecord()
    {
        if (!IsConnected)
        {
            return;
        }

        if (IsRecording)
        {
            _obs.StopRecord();
        }
        else
        {
            _obs.StartRecord();
        }
    }

    public void ToggleRecordPause()
    {
        if (!IsConnected || !IsRecording)
        {
            return;
        }

        if (IsRecordingPaused)
        {
            _obs.ResumeRecord();
        }
        else
        {
            _obs.PauseRecord();
        }
    }

    public void ToggleStream()
    {
        if (!IsConnected)
        {
            return;
        }

        if (IsStreaming)
        {
            _obs.StopStream();
        }
        else
        {
            _obs.StartStream();
        }
    }

    public void ToggleVirtualCam()
    {
        if (!IsConnected)
        {
            return;
        }

        if (IsVirtualCamActive)
        {
            _obs.StopVirtualCam();
        }
        else
        {
            _obs.StartVirtualCam();
        }
    }

    public void ToggleReplayBuffer()
    {
        if (!IsConnected || !_settings.ShowReplayBuffer)
        {
            return;
        }

        if (IsReplayBufferActive)
        {
            _obs.StopReplayBuffer();
        }
        else
        {
            _obs.StartReplayBuffer();
        }
    }

    public void SaveReplayBuffer()
    {
        if (IsConnected && IsReplayBufferActive)
        {
            _obs.SaveReplayBuffer();
        }
    }

    public void SetScene(string sceneName)
    {
        if (IsConnected && !string.IsNullOrWhiteSpace(sceneName))
        {
            _obs.SetCurrentProgramScene(sceneName);
        }
    }

    public void OpenRecordFolder()
    {
        try
        {
            string? path = null;
            if (IsConnected)
            {
                path = _obs.GetRecordDirectory();
            }

            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                LastError = "Record folder unavailable";
                RaiseChanged();
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            RaiseChanged();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer.Dispose();

        _obs.Connected -= OnConnected;
        _obs.Disconnected -= OnDisconnected;
        _obs.RecordStateChanged -= OnRecordStateChanged;
        _obs.StreamStateChanged -= OnStreamStateChanged;
        _obs.VirtualcamStateChanged -= OnVirtualCamStateChanged;
        _obs.ReplayBufferStateChanged -= OnReplayBufferStateChanged;
        _obs.CurrentProgramSceneChanged -= OnProgramSceneChanged;
        _obs.SceneListChanged -= OnSceneListChanged;

        try
        {
            if (_obs.IsConnected)
            {
                _obs.Disconnect();
            }
        }
        catch
        {
            // Ignore shutdown failures.
        }
    }

    private void PollOnline()
    {
        if (_disposed)
        {
            return;
        }

        // Already authenticated — nothing to do.
        if (IsConnected || _obs.IsConnected)
        {
            if (!IsObsOnline)
            {
                IsObsOnline = true;
                RaiseChanged();
            }

            return;
        }

        var online = ObsOnlineProbe.IsOnline(_settings.Host);
        if (online != IsObsOnline)
        {
            IsObsOnline = online;
            if (!online)
            {
                IsLoading = false;
            }

            RaiseChanged();
        }

        // Only start a real WebSocket login once OBS is reachable.
        if (online)
        {
            BeginConnect(force: false);
        }
    }

    private void BeginConnect(bool force)
    {
        if (_disposed)
        {
            return;
        }

        if (!force && (IsConnected || _obs.IsConnected))
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _connectGate, 1, 0) != 0)
        {
            // Another connect is already in flight.
            return;
        }

        try
        {
            if (force && _obs.IsConnected)
            {
                _obs.Disconnect();
            }

            var becameLoading = !IsLoading;
            IsLoading = true;
            if (force)
            {
                LastError = null;
            }

            if (becameLoading || force)
            {
                RaiseChanged();
            }

            _obs.ConnectAsync(_settings.Host, _settings.Password);
        }
        catch (Exception ex)
        {
            IsConnected = false;
            IsLoading = false;
            LastError = ex.Message;
            Interlocked.Exchange(ref _connectGate, 0);
            RaiseChanged();
        }
    }

    private void OnConnected(object? sender, EventArgs e)
    {
        IsConnected = true;
        IsLoading = false;
        LastError = null;
        Interlocked.Exchange(ref _connectGate, 0);
        SyncStateFromObs();
        RaiseChanged();
    }

    private void OnDisconnected(object? sender, ObsDisconnectionInfo e)
    {
        IsConnected = false;
        IsLoading = false;
        LastError = e.DisconnectReason;
        Interlocked.Exchange(ref _connectGate, 0);
        RaiseChanged();
    }

    private void SyncStateFromObs()
    {
        try
        {
            var record = _obs.GetRecordStatus();
            IsRecording = record.IsRecording;
            IsRecordingPaused = record.IsRecordingPaused;
            IsStreaming = _obs.GetStreamStatus().IsActive;
            IsVirtualCamActive = _obs.GetVirtualCamStatus().IsActive;
            IsReplayBufferActive = _settings.ShowReplayBuffer && _obs.GetReplayBufferStatus();
            CurrentScene = _obs.GetCurrentProgramScene() ?? string.Empty;
            Scenes = _obs.ListScenes().Select(s => s.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch
        {
            // Keep last-known UI state if a status call fails mid-connect.
        }
    }

    private void OnRecordStateChanged(object? sender, RecordStateChangedEventArgs e)
    {
        switch (e.OutputState.State)
        {
            case OutputState.OBS_WEBSOCKET_OUTPUT_STARTED:
            case OutputState.OBS_WEBSOCKET_OUTPUT_RESUMED:
                IsRecording = true;
                IsRecordingPaused = false;
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_PAUSED:
                IsRecording = true;
                IsRecordingPaused = true;
                break;
            case OutputState.OBS_WEBSOCKET_OUTPUT_STOPPED:
                IsRecording = false;
                IsRecordingPaused = false;
                break;
        }

        RaiseChanged();
    }

    private void OnStreamStateChanged(object? sender, StreamStateChangedEventArgs e)
    {
        IsStreaming = e.OutputState.State is OutputState.OBS_WEBSOCKET_OUTPUT_STARTED
            or OutputState.OBS_WEBSOCKET_OUTPUT_STARTING;
        RaiseChanged();
    }

    private void OnVirtualCamStateChanged(object? sender, VirtualcamStateChangedEventArgs e)
    {
        IsVirtualCamActive = e.OutputState.IsActive;
        RaiseChanged();
    }

    private void OnReplayBufferStateChanged(object? sender, ReplayBufferStateChangedEventArgs e)
    {
        if (!_settings.ShowReplayBuffer)
        {
            return;
        }

        IsReplayBufferActive = e.OutputState.IsActive;
        RaiseChanged();
    }

    private void OnProgramSceneChanged(object? sender, ProgramSceneChangedEventArgs e)
    {
        CurrentScene = e.SceneName;
        RaiseChanged();
    }

    private void OnSceneListChanged(object? sender, SceneListChangedEventArgs e)
    {
        try
        {
            Scenes = _obs.ListScenes().Select(s => s.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        }
        catch
        {
            // Ignore transient failures while OBS updates scenes.
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        lock (_sync)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
