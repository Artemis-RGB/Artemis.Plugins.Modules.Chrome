using System;
using System.Collections.Generic;
using System.Linq;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;
using Avalonia.Remote.Protocol;
using Newtonsoft.Json;
using Serilog;


namespace Artemis.Plugins.Modules.Chrome;

public class ChromeExtensionModule : Module<ChromeExtensionDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new();

    private readonly ILogger _logger;
    private readonly IWebServerService _webServerService;

    private StringPluginEndPoint? _rawEndpoint;
    private JsonPluginEndPoint<UpdatedTabData>? _updatedTabEndpoint;
    private JsonPluginEndPoint<ActivatedTabData>? _activatedTabEndpoint;
    private JsonPluginEndPoint<AttachedTabData>? _attachedTabEndpoint;
    private JsonPluginEndPoint<ChromeExtensionTab>? _newTabEndpoint;
    private JsonPluginEndPoint<TabMovedData>? _tabMovedEndpoint;
    private JsonPluginEndPoint<ClosedTabData>? _closedTabEndpoint;

    public ChromeExtensionModule(IWebServerService webServerService, ILogger logger)
    {
        _webServerService = webServerService;
        _logger = logger;
    }

    public override void Enable()
    {
        _rawEndpoint = _webServerService.AddStringEndPoint(this, "raw", HandleRaw);
        _updatedTabEndpoint = _webServerService.AddJsonEndPoint<UpdatedTabData>(this, "updatedTab", HandleUpdatedTab);
        _activatedTabEndpoint = _webServerService.AddJsonEndPoint<ActivatedTabData>(this, "activatedTab", HandleActivatedTab);
        _attachedTabEndpoint = _webServerService.AddJsonEndPoint<AttachedTabData>(this, "attachedTab", HandleAttachedTab);
        _newTabEndpoint = _webServerService.AddJsonEndPoint<ChromeExtensionTab>(this, "newTab", HandleNewTab);
        _tabMovedEndpoint = _webServerService.AddJsonEndPoint<TabMovedData>(this, "tabMoved", HandleTabMoved);
        _closedTabEndpoint = _webServerService.AddJsonEndPoint<ClosedTabData>(this, "closedTab", HandleClosedTab);

        _rawEndpoint.ProcessedRequest += OnProcessedRequest;
        _updatedTabEndpoint.ProcessedRequest += OnProcessedRequest;
        _activatedTabEndpoint.ProcessedRequest += OnProcessedRequest;
        _closedTabEndpoint.ProcessedRequest += OnProcessedRequest;
    }

    private void HandleRaw(string data)
    {
        var serializerSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        JsonConvert.PopulateObject(data, DataModel, serializerSettings);
    }

    private void HandleUpdatedTab(UpdatedTabData data)
    {
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(data.ChangeInfo), DataModel.Tabs.Find(v => v.Id == data.TabId));
    }

    private void HandleActivatedTab(ActivatedTabData data)
    {
        DataModel.Tabs.Find(v => v.Active).Active = false;
        DataModel.Tabs.Find(v => v.Id == data.TabId).Active = true;
    }

    private void HandleAttachedTab(AttachedTabData data)
    {
        DataModel.Tabs.Find(v => v.Id == data.TabId).Index = data.AttachInfo.newPosition;
        DataModel.Tabs.Find(v => v.Id == data.TabId).WindowId = data.AttachInfo.newWindowId;
    }

    private void HandleNewTab(ChromeExtensionTab tab)
    {
        DataModel.Tabs.Add(tab);
    }

    private void HandleTabMoved(TabMovedData data)
    {
        DataModel.Tabs.Find(v => v.Id == data.TabId).Index = data.MoveInfo.ToIndex;
        DataModel.Tabs.Find(v => v.Id == data.TabId).WindowId = data.MoveInfo.WindowId;

        var item = DataModel.Tabs.Find(v => v.Id == data.TabId);
        var oldIndex = DataModel.Tabs.FindIndex(v => v.Id == data.TabId);
        var newIndex = data.MoveInfo.ToIndex;

        DataModel.Tabs.RemoveAt(oldIndex);

        if (newIndex > oldIndex) newIndex--;

        DataModel.Tabs.Insert(newIndex, item);
    }

    private void HandleClosedTab(ClosedTabData data)
    {
        DataModel.Tabs.Remove(DataModel.Tabs.Find(v => v.Id == data.TabId));
    }

    private void OnProcessedRequest(object? sender, EndpointRequestEventArgs e)
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
        _webServerService.RemovePluginEndPoint(_rawEndpoint);
        _webServerService.RemovePluginEndPoint(_updatedTabEndpoint);
        _webServerService.RemovePluginEndPoint(_activatedTabEndpoint);
        _webServerService.RemovePluginEndPoint(_attachedTabEndpoint);
        _webServerService.RemovePluginEndPoint(_newTabEndpoint);
        _webServerService.RemovePluginEndPoint(_tabMovedEndpoint);
        _webServerService.RemovePluginEndPoint(_closedTabEndpoint);
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