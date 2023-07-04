using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;
using Newtonsoft.Json;

namespace Artemis.Plugins.Modules.Chrome;

public class ChromeExtensionModule : Module<ChromeExtensionDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new();

    private readonly IWebServerService _webServerService;

    private StringPluginEndPoint _updateEndpoint;

    public ChromeExtensionModule(IWebServerService webServerService)
    {
        _webServerService = webServerService;
    }

    public override void Enable()
    {
        _updateEndpoint = _webServerService.AddStringEndPoint(this, "update", HandleUpdate);
        _updateEndpoint.ProcessedRequest += OnProcessedRequest;
    }

    private void HandleUpdate(string data)
    {
        var serializerSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        JsonConvert.PopulateObject(data, DataModel, serializerSettings);
    }

    private void OnProcessedRequest(object sender, EndpointRequestEventArgs e)
    {
        if (DataModel.Tabs.Count > 0)
        {
            DataModel.AnyTabAudible = DataModel.Tabs.Any(v => v.Audible);
            DataModel.ActiveTabAudible = DataModel.Tabs.Find(v => v.Active).Audible;
        }
        else
        {
            DataModel.AnyTabAudible = false;
            DataModel.ActiveTabAudible = false;
        }
    }

    public override void Disable()
    {
        _webServerService.RemovePluginEndPoint(_updateEndpoint);
    }

    public override void ModuleActivated(bool isOverride)
    {

    }

    public override void ModuleDeactivated(bool isOverride)
    {

    }

    public override void Update(double deltaTime)
    {

    }
}