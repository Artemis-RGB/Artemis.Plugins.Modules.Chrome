using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core.ColorScience;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;
using EmbedIO;
using Newtonsoft.Json;
using Serilog;
using SkiaSharp;

namespace Artemis.Plugins.Modules.Chrome;

public partial class ChromeExtensionModule : Module<ChromeDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new();
    
    private readonly Dictionary<string, ColorSwatch> _cache;
    private readonly ILogger _logger;
    private readonly IWebServerService _webServerService;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private bool _firstRun;

    public ChromeExtensionModule(IWebServerService webServerService, ILogger logger)
    {
        _webServerService = webServerService;
        _logger = logger;
        _httpClient = new HttpClient();
        _cache = new Dictionary<string, ColorSwatch>();
        _firstRun = true;
    }

    public override void Enable()
    {
        _firstRun = true;
        _webServerService.AddResponsiveJsonEndPoint<TabUpdated>(this, "tabUpdated", data =>
        {
            OnTabUpdated(data);
            UpdateTabData();

            return Respond();
        });
        _webServerService.AddResponsiveJsonEndPoint<TabActivated>(this, "tabActivated", data =>
        {
            OnTabActivated(data);
            UpdateTabData();

            return Respond();
        });
        _webServerService.AddResponsiveJsonEndPoint<Tab>(this, "tabCreated", data =>
        {
            OnTabCreated(data);
            UpdateTabData();

            return Respond();
        });
        _webServerService.AddResponsiveJsonEndPoint<TabMoved>(this, "tabMoved", data =>
        {
            OnTabMoved(data);
            UpdateTabData();

            return Respond();
        });
        _webServerService.AddResponsiveJsonEndPoint<TabClosed>(this, "tabClosed", data =>
        {
            OnTabClosed(data);
            UpdateTabData();

            return Respond();
        });
        _webServerService.AddResponsiveJsonEndPoint<Tab[]>(this, "setAllTabs", data =>
        {
            OnSetAllTabs(data);
            UpdateTabData();
            
            return Respond();
        });
    }

    /// <summary>
    ///     Responds to Chrome's POST request with a JSON object containing the firstRequest property.
    ///     This will be true if Chrome was already running when the plugin was enabled, false otherwise.
    ///     This is used to determine whether or not to send the entire list of tabs to the plugin.
    /// </summary>
    private object? Respond()
    {
        if (!_firstRun)
        {
            return new
            {
                firstRequest = false
            };
        }

        _firstRun = false;
        return new
        {
            firstRequest = true
        };
    }
    
    private void OnSetAllTabs(Tab[] data)
    {
        DataModel.Tabs.Clear();
        DataModel.Tabs.AddRange(data);
    }

    private void OnTabUpdated(TabUpdated data)
    {
        var updatedTab = DataModel.Tabs.Find(v => v.Id == data.TabId);
        if (updatedTab == null)
            return;
        
        JsonConvert.PopulateObject(JsonConvert.SerializeObject(data.ChangeInfo), updatedTab);

        DataModel.OnTabUpdated.Trigger(data);
    }

    private void OnTabActivated(TabActivated data)
    {
        var activeTab = DataModel.Tabs.Find(v => v.Active);
        var thisTab = DataModel.Tabs.Find(v => v.Id == data.TabId);

        if (activeTab != null)
            activeTab.Active = false;
        if (thisTab != null)
            thisTab.Active = true;

        DataModel.OnTabActivated.Trigger(data);
    }

    private void OnTabCreated(Tab tab)
    {
        DataModel.Tabs.Add(tab);
        DataModel.OnNewTab.Trigger(tab);
    }

    private void OnTabMoved(TabMoved data)
    {
        var tab = DataModel.Tabs.Find(v => v.Id == data.TabId);
        if (tab != null)
        {
            tab.Index = data.MoveInfo.ToIndex;
            tab.WindowId = data.MoveInfo.WindowId;
        }

        var item = DataModel.Tabs.Find(v => v.Id == data.TabId);
        var oldIndex = DataModel.Tabs.FindIndex(v => v.Id == data.TabId);
        var newIndex = data.MoveInfo.ToIndex;

        DataModel.Tabs.RemoveAt(oldIndex);

        if (newIndex > oldIndex) newIndex--;

        DataModel.Tabs.Insert(newIndex, item);

        DataModel.OnTabMoved.Trigger(data);
    }

    private void OnTabClosed(TabClosed data)
    {
        var toRemove = DataModel.Tabs.Find(v => v.Id == data.TabId);
        if (toRemove != null)
            DataModel.Tabs.Remove(toRemove);

        DataModel.OnTabClosed.Trigger(data);
    }

    private void UpdateTabData()
    {
        if (DataModel.Tabs.Any())
        {
            DataModel.AnyTabAudible = DataModel.Tabs.Any(v => v.Audible);
            DataModel.ActiveTabAudible = DataModel.Tabs.Find(v => v.Active)?.Audible ?? false;
            DataModel.ActiveTab = DataModel.Tabs.Find(v => v.Active);
        }
        else
        {
            DataModel.AnyTabAudible = false;
            DataModel.ActiveTabAudible = false;
            DataModel.ActiveTab = null;
        }

        foreach (var tab in DataModel.Tabs)
        {
            if (!tab.ColorCalculated)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        tab.FavIconColors = await GetFavIconColors(tab.FavIconUrl);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to get favicon colors for {Title} with uri {Uri}", tab.Title, tab.FavIconUrl);
                        tab.FavIconColors = default;
                    }
                    tab.ColorCalculated = true;
                });
            }            
        }
    }

    public override void Disable()
    {
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

    #region FavIconColors

    [GeneratedRegex("data:(?<type>.+?);base64,(?<data>.+)")]
    private static partial Regex DataUriRegex();
    
    private async Task<ColorSwatch> GetFavIconColors(string url)
    {
        if (string.IsNullOrEmpty(url))
            return default;
        
        await _semaphore.WaitAsync();

        if (_cache.TryGetValue(url, out var colors))
            return colors;

        try
        {
            var newSwatch = await GetFavIconColorsFromUri(url);
            _cache.Add(url, newSwatch);
            return newSwatch;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<ColorSwatch> GetFavIconColorsFromUri(string uri)
    {
        //TODO: this seems to work for *.ico but some PNGs broke. Still, no more concurrency issues
        var matches = DataUriRegex().Match(uri);
        if (!matches.Success)
        {
            await using var stream = await _httpClient.GetStreamAsync(uri);
            using var codec = SKCodec.Create(stream);
            using var skBitmap = SKBitmap.Decode(codec);
            var colors = ColorQuantizer.Quantize(skBitmap.Pixels, 256);
            return ColorQuantizer.FindAllColorVariations(colors, true);
        }
        else
        {
            if (matches.Groups.Count < 3)
            {
                throw new Exception("Invalid DataUrl format");
            }

            using var stream = new MemoryStream(Convert.FromBase64String(matches.Groups["data"].Value));
            using var codec = SKCodec.Create(stream);
            using var skBitmap = SKBitmap.Decode(codec);
            var skClrs = ColorQuantizer.Quantize(skBitmap.Pixels, 256);
            return ColorQuantizer.FindAllColorVariations(skClrs, true);
        }
    }
    
    #endregion

}