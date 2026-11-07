using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace obsControl;

/// <summary>
/// Primary OBS controls. Used as both the Command Palette page and the dock band
/// (ListPage items → dock buttons). Rare actions live in MoreCommands / nested pages.
/// Dock hover tooltips come from <see cref="ListItem.Title"/> — keep it set.
/// </summary>
internal sealed partial class ObsControlPage : ListPage
{
    private readonly SettingsManager _settings;
    private readonly ObsSession _session;
    private readonly ScenesPage _scenesPage;

    public ObsControlPage(SettingsManager settings, ObsSession session, bool dock = false)
    {
        _settings = settings;
        _session = session;
        _scenesPage = new ScenesPage(session);

        Id = dock ? "com.obscontrol.dockband" : "com.obscontrol.page";
        Name = "OBS";
        Title = "OBS";
        Icon = new IconInfo("\uE714");

        _session.Changed += (_, _) => RaiseItemsChanged();
        _settings.Settings.SettingsChanged += (_, _) => RaiseItemsChanged();
    }

    public override IListItem[] GetItems() =>
        _session.IsConnected ? BuildConnectedItems() : BuildDisconnectedItems();

    private IListItem[] BuildDisconnectedItems()
    {
        var status = _session.IsLoading
            ? "Connecting…"
            : _session.IsObsOnline ? "OBS online" : "OBS offline";

        return
        [
            Item(
                status,
                _session.IsObsOnline ? "\uE8FB" : "\uE783",
                new NoOpCommand { Name = status },
                _session.LastError),
            Item("Launch OBS", "\uE7FC", _session.LaunchObs),
            Item(
                _settings.HasPassword ? "Password" : "Enter password",
                "\uE72E",
                new ConnectionFormPage(_settings, _session.Connect)),
            Item("Reconnect", "\uE72C", _session.Connect),
        ];
    }

    private IListItem[] BuildConnectedItems()
    {
        var items = new List<IListItem>();

        // Head menu + overflow for rare actions (incl. output folder).
        var head = Item(
            StatusTitle(),
            StatusGlyph(),
            new NoOpCommand { Name = StatusTitle() },
            StatusSubtitle());
        head.MoreCommands = BuildMoreCommands();
        items.Add(head);

        items.Add(Item(RecordTitle(), RecordGlyph(), _session.ToggleRecord));

        if (_session.IsRecording)
        {
            items.Add(Item(
                _session.IsRecordingPaused ? "Resume" : "Pause",
                _session.IsRecordingPaused ? "\uE768" : "\uE769",
                _session.ToggleRecordPause));
        }

        items.Add(Item(
            _session.IsStreaming ? "Stop live" : "Go live",
            "\uE774",
            _session.ToggleStream));

        // One dock button → nested scene list.
        items.Add(new ListItem(_scenesPage)
        {
            Title = SceneButtonTitle(),
            Subtitle = _session.CurrentScene,
            Icon = new IconInfo("\uE8B9"),
        });

        return [.. items];
    }

    private IContextItem[] BuildMoreCommands()
    {
        var more = new List<IContextItem>
        {
            new CommandContextItem(new ConnectionFormPage(_settings, _session.Connect))
            {
                Title = "Password",
                Icon = new IconInfo("\uE72E"),
            },
            new CommandContextItem(new AnonymousCommand(_session.Connect)
            {
                Name = "Reconnect",
                Icon = new IconInfo("\uE72C"),
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = "Reconnect",
                Icon = new IconInfo("\uE72C"),
            },
            new Separator(),
            new CommandContextItem(new AnonymousCommand(_session.OpenRecordFolder)
            {
                Name = "Output folder",
                Icon = new IconInfo("\uE8B7"),
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = "Output folder",
                Icon = new IconInfo("\uE8B7"),
            },
            new CommandContextItem(new AnonymousCommand(_session.ToggleVirtualCam)
            {
                Name = VirtualCamTitle(),
                Icon = new IconInfo("\uE714"),
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = VirtualCamTitle(),
                Icon = new IconInfo("\uE714"),
            },
        };

        if (_settings.ShowReplayBuffer)
        {
            more.Add(new CommandContextItem(new AnonymousCommand(_session.ToggleReplayBuffer)
            {
                Name = ReplayTitle(),
                Icon = new IconInfo("\uE8A5"),
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = ReplayTitle(),
                Icon = new IconInfo("\uE8A5"),
            });

            if (_session.IsReplayBufferActive)
            {
                more.Add(new CommandContextItem(new AnonymousCommand(_session.SaveReplayBuffer)
                {
                    Name = "Save replay",
                    Icon = new IconInfo("\uE74E"),
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = "Save replay",
                    Icon = new IconInfo("\uE74E"),
                });
            }
        }

        return [.. more];
    }

    private string StatusTitle()
    {
        if (_session.IsStreaming && _session.IsRecording)
        {
            return _session.IsRecordingPaused ? "LIVE · paused" : "LIVE · rec";
        }

        if (_session.IsStreaming)
        {
            return "LIVE";
        }

        if (_session.IsRecording)
        {
            return _session.IsRecordingPaused ? "Paused" : "Recording";
        }

        return "OBS Menu";
    }

    private string? StatusSubtitle() =>
        string.IsNullOrWhiteSpace(_session.CurrentScene) ? null : _session.CurrentScene;

    private string StatusGlyph()
    {
        if (_session.IsStreaming)
        {
            return "\uE774";
        }

        if (_session.IsRecording)
        {
            return "\uE7C8";
        }

        return "\uE8FB";
    }

    private string RecordTitle() =>
        !_session.IsRecording ? "Record" : "Stop rec";

    private string RecordGlyph() =>
        !_session.IsRecording ? "\uE7C8" : "\uE71A";

    private string SceneButtonTitle() =>
        string.IsNullOrWhiteSpace(_session.CurrentScene) ? "Scenes" : _session.CurrentScene;

    private string VirtualCamTitle() =>
        _session.IsVirtualCamActive ? "Stop cam" : "Virtual cam";

    private string ReplayTitle() =>
        _session.IsReplayBufferActive ? "Stop replay" : "Replay buf";

    private static ListItem Item(string title, string glyph, Action action, string? subtitle = null) =>
        Item(
            title,
            glyph,
            new AnonymousCommand(action)
            {
                Name = title,
                Result = CommandResult.KeepOpen(),
            },
            subtitle);

    private static ListItem Item(string title, string glyph, ICommand command, string? subtitle = null) =>
        new(command)
        {
            Title = title,
            Subtitle = subtitle ?? string.Empty,
            Icon = new IconInfo(glyph),
        };
}
