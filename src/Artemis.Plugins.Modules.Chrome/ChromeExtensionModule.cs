using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Artemis.Core.ColorScience;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;
using Newtonsoft.Json;
using Serilog;
using SkiaSharp;

namespace Artemis.Plugins.Modules.Chrome;

public partial class ChromeExtensionModule : Module<ChromeExtensionDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new();

    private readonly ILogger _logger;
    private readonly IWebServerService _webServerService;
    private readonly HttpClient _httpClient;

    private StringPluginEndPoint? _rawEndpoint;
    private JsonPluginEndPoint<UpdatedTabData>? _updatedTabEndpoint;
    private JsonPluginEndPoint<ActivatedTabData>? _activatedTabEndpoint;
    private JsonPluginEndPoint<AttachedTabData>? _attachedTabEndpoint;
    private JsonPluginEndPoint<ChromeExtensionTab>? _newTabEndpoint;
    private JsonPluginEndPoint<TabMovedData>? _tabMovedEndpoint;
    private JsonPluginEndPoint<ClosedTabData>? _closedTabEndpoint;

    private Dictionary<string, ColorSwatch?> _cache;

    public ChromeExtensionModule(IWebServerService webServerService, ILogger logger)
    {
        _webServerService = webServerService;
        _logger = logger;
        _httpClient = new HttpClient();
        _cache = new Dictionary<string, ColorSwatch?>();
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

    private async void HandleRaw(string data)
    {
        var serializerSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };

        JsonConvert.PopulateObject(data, DataModel, serializerSettings);

        foreach (var tab in DataModel.Tabs)
        {
            _logger.Debug("calling getfaviconcolors from raw tabs " + JsonConvert.SerializeObject(tab));
            tab.FavIconColors = await GetFavIconColors(tab.FavIconUrl);
        }
    }

    private async void HandleUpdatedTab(UpdatedTabData data)
    {
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(data.ChangeInfo), DataModel.Tabs.Find(v => v.Id == data.TabId));

        DataModel.OnTabUpdated.Trigger(data);

        if (data.ChangeInfo.FavIconUrl != null)
        {
            _logger.Debug("calling getfaviconcolors from updated tab " + JsonConvert.SerializeObject(DataModel.Tabs.Find(v => v.Id == data.TabId)));
            DataModel.Tabs.Find(v => v.Id == data.TabId).FavIconColors = await GetFavIconColors(data.ChangeInfo.FavIconUrl);
        }
    }

    private void HandleActivatedTab(ActivatedTabData data)
    {
        DataModel.Tabs.Find(v => v.Active).Active = false;
        DataModel.Tabs.Find(v => v.Id == data.TabId).Active = true;

        DataModel.OnTabActivated.Trigger(data);
    }

    private void HandleAttachedTab(AttachedTabData data)
    {
        DataModel.Tabs.Find(v => v.Id == data.TabId).Index = data.AttachInfo.NewPosition;
        DataModel.Tabs.Find(v => v.Id == data.TabId).WindowId = data.AttachInfo.NewWindowId;

        DataModel.OnTabAttached.Trigger(data);
    }

    private async void HandleNewTab(ChromeExtensionTab tab)
    {
        _logger.Debug("calling getfaviconcolors from new tab " + JsonConvert.SerializeObject(tab));
        tab.FavIconColors = await GetFavIconColors(tab.FavIconUrl);
        DataModel.Tabs.Add(tab);
        DataModel.OnNewTab.Trigger(tab);
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

        DataModel.OnTabMoved.Trigger(data);
    }

    private void HandleClosedTab(ClosedTabData data)
    {
        DataModel.Tabs.Remove(DataModel.Tabs.Find(v => v.Id == data.TabId));

        DataModel.OnTabClosed.Trigger(data);
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

        DataModel.ActiveTab = DataModel.Tabs.Find(v => v.Active);
    }

    private async Task<ColorSwatch?> GetFavIconColors(string url)
    {
        _logger.Debug("getting colors for " + url);
        lock (_cache)
        {
            if (_cache.TryGetValue(url, out var colors))
            {
                return colors;
            }
        }

        try
        {
            var newSwatch = await GetFavIconColorsFromUri(url);
            _logger.Debug("adding to cache " + JsonConvert.SerializeObject(_cache));
            lock (_cache)
            {
                _cache.Add(url, newSwatch);
                return newSwatch;
            }
        }
        catch (Exception e)
        {
            _logger.Error(e, "Failed to get favicon colors");
        }

        return null;
    }

    private async Task<ColorSwatch?> GetFavIconColorsFromUri(string uri)
    {
        if (!IsDataUri(uri) && uri != "")
        {
            _logger.Debug(uri + " is HTTP url");
            using Stream stream = await _httpClient.GetStreamAsync(uri);
            using SKBitmap skbm = SKBitmap.Decode(stream);
            SKColor[] skClrs = ColorQuantizer.Quantize(skbm.Pixels, 256);
            return ColorQuantizer.FindAllColorVariations(skClrs, true);
        }
        else if (uri == "")
        {
            return null;
        }
        else
        {
            _logger.Debug(uri + " is data URI");
            var matches = DataURIRegex().Match(uri);

            if (matches.Groups.Count < 3)
            {
                throw new Exception("Invalid DataUrl format");
            }

            _logger.Debug(Convert.FromBase64String(matches.Groups["data"].Value).ToString());

            Stream stream = new MemoryStream(Convert.FromBase64String(matches.Groups["data"].Value));

            using SKBitmap skbm = SKBitmap.Decode(stream);
            _logger.Debug(JsonConvert.SerializeObject(skbm, Formatting.Indented));
            SKColor[] skClrs = ColorQuantizer.Quantize(skbm.Pixels, 256);
            return ColorQuantizer.FindAllColorVariations(skClrs, true);
        }
    }

    private static bool IsDataUri(string input)
    {
        string pattern = @"^data:[a-zA-Z0-9\/\+]+;base64,([a-zA-Z0-9\/\+=])+$";

        return Regex.IsMatch(input, pattern);
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

    [GeneratedRegex("data:(?<type>.+?);base64,(?<data>.+)")]
    private static partial Regex DataURIRegex();

}