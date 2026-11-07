using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace obsControl;

public partial class obsControlCommandsProvider : CommandProvider
{
    private readonly SettingsManager _settings = new();
    private readonly ObsSession _session;
    private readonly ObsControlPage _page;
    private readonly ObsControlPage _dockPage;
    private readonly CommandItem _topLevel;
    private readonly CommandItem _dockBand;

    public obsControlCommandsProvider()
    {
        DisplayName = "OBS";
        Id = "com.obscontrol.dock";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Settings = _settings.Settings;

        _session = new ObsSession(_settings);
        _page = new ObsControlPage(_settings, _session, dock: false);
        _dockPage = new ObsControlPage(_settings, _session, dock: true);

        _topLevel = new CommandItem(_page)
        {
            Title = "OBS",
            Icon = _page.Icon,
        };

        // ListPage dock band → each GetItems() entry becomes its own button.
        _dockBand = new CommandItem(_dockPage)
        {
            Title = "OBS",
            Icon = _dockPage.Icon,
        };

        // ObsSession starts polling immediately on construct.
    }

    public override ICommandItem[] TopLevelCommands() => [_topLevel];

    public override ICommandItem[]? GetDockBands() => [_dockBand];
}
