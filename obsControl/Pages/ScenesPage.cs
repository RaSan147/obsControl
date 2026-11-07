using System;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace obsControl;

/// <summary>
/// Nested scene picker — one dock button, scenes stacked inside.
/// </summary>
internal sealed partial class ScenesPage : ListPage
{
    private readonly ObsSession _session;

    public ScenesPage(ObsSession session)
    {
        _session = session;

        Id = "com.obscontrol.scenes";
        Name = "Scenes";
        Title = "Scenes";
        Icon = new IconInfo("\uE8B9");

        _session.Changed += (_, _) => RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        if (!_session.IsConnected)
        {
            return
            [
                new ListItem(new NoOpCommand())
                {
                    Title = "Disconnected",
                    Icon = new IconInfo("\uE783"),
                },
            ];
        }

        return [.. _session.Scenes.Select(scene =>
        {
            var sceneName = scene;
            var isCurrent = string.Equals(sceneName, _session.CurrentScene, StringComparison.Ordinal);
            return new ListItem(new AnonymousCommand(() => _session.SetScene(sceneName))
            {
                Result = CommandResult.KeepOpen(),
            })
            {
                Title = sceneName,
                Subtitle = isCurrent ? "Current" : string.Empty,
                Icon = new IconInfo(isCurrent ? "\uE8FB" : "\uE8B9"),
            };
        })];
    }
}
