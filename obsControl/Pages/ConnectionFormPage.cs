using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace obsControl;

internal sealed partial class ConnectionFormPage : ContentPage
{
    private readonly ConnectionForm _form;

    public ConnectionFormPage(SettingsManager settings, System.Action? onSaved = null)
    {
        Id = "com.obscontrol.connection";
        Name = "Password";
        Title = "Password";
        Icon = new IconInfo("\uE72E");

        _form = new ConnectionForm(settings, onSaved);
    }

    public override IContent[] GetContent() => [_form];
}

internal sealed partial class ConnectionForm : FormContent
{
    private readonly SettingsManager _settings;
    private readonly System.Action? _onSaved;

    public ConnectionForm(SettingsManager settings, System.Action? onSaved)
    {
        _settings = settings;
        _onSaved = onSaved;

        TemplateJson = """
            {
              "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
              "type": "AdaptiveCard",
              "version": "1.6",
              "body": [
                {
                  "type": "Input.Text",
                  "id": "Host",
                  "label": "URL",
                  "placeholder": "ws://127.0.0.1:4455",
                  "value": "${host}",
                  "isRequired": true,
                  "errorMessage": "URL required"
                },
                {
                  "type": "Input.Text",
                  "id": "Password",
                  "label": "Password",
                  "placeholder": "WebSocket password",
                  "style": "Password",
                  "value": "${password}"
                }
              ],
              "actions": [
                {
                  "type": "Action.Submit",
                  "title": "Connect",
                  "style": "positive",
                  "data": { "mode": "password" }
                },
                {
                  "type": "Action.Submit",
                  "title": "No password",
                  "data": { "mode": "noPassword" }
                }
              ]
            }
            """;

        RefreshData();
    }

    public void RefreshData()
    {
        DataJson = new JsonObject
        {
            ["host"] = _settings.Host,
            ["password"] = _settings.Password,
        }.ToJsonString();
    }

    public override CommandResult SubmitForm(string payload)
    {
        var input = JsonNode.Parse(payload)?.AsObject();
        if (input is null)
        {
            return CommandResult.KeepOpen();
        }

        var host = input["Host"]?.ToString()?.Trim();
        var mode = input["mode"]?.ToString();
        var password = string.Equals(mode, "noPassword", System.StringComparison.Ordinal)
            ? string.Empty
            : input["Password"]?.ToString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(host))
        {
            return CommandResult.ShowToast("URL required");
        }

        _settings.SetConnection(host, password);
        _onSaved?.Invoke();
        RefreshData();

        return CommandResult.GoBack();
    }
}
