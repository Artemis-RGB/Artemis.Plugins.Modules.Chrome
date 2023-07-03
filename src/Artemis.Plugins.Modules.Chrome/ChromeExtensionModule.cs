using System.Collections.Generic;
using Artemis.Core;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;

namespace Artemis.Plugins.Modules.Chrome;

public class ChromeExtensionModule : Module<ChromeExtensionDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new();

    private readonly IWebServerService _webServerService;

    private DataModelJsonPluginEndPoint<ChromeExtensionDataModel> _updateEndpoint;

    public ChromeExtensionModule(IWebServerService webServerService)
    {
        _webServerService = webServerService;
    }

    public override void ModuleActivated(bool isOverride)
    {

    }

    public override void ModuleDeactivated(bool isOverride)
    {

    }

    public override void Enable()
    {
        _updateEndpoint = _webServerService.AddDataModelJsonEndPoint(this, "update");
    }

    public override void Disable()
    {
        _webServerService.RemovePluginEndPoint(_updateEndpoint);
    }

    public override void Update(double deltaTime)
    {

    }
}