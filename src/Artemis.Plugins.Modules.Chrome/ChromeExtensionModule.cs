using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Artemis.Core.ColorScience;
using Artemis.Core.Modules;
using Artemis.Core.Services;
using Artemis.Plugins.Modules.Chrome.DataModels;
using Avalonia.Markup.Xaml;
using Newtonsoft.Json;
using Serilog;
using SkiaSharp;
using Svg.Skia;

namespace Artemis.Plugins.Modules.Chrome;

public partial class ChromeExtensionModule : Module<ChromeDataModel>
{
    public override List<IModuleActivationRequirement> ActivationRequirements { get; } = new()
    {
        //todo: verify that these are the correct process names
        new ProcessActivationRequirement("chrome"),
        new ProcessActivationRequirement("msedge"),
        new ProcessActivationRequirement("opera"),
        new ProcessActivationRequirement("brave")
    };

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
        _httpClient = new();
        _cache = new();
        _firstRun = true;
    }

    public override void Enable()
    {
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
        _webServerService.AddResponsiveJsonEndPoint<bool>(this, "setFullscreen", data =>
        {
            OnSetFullScreen(data);
            UpdateTabData();

            return Respond();
        });
    }

    public override void Disable()
    {
    }

    public override void ModuleActivated(bool isOverride)
    {
        _firstRun = true;
    }

    public override void ModuleDeactivated(bool isOverride)
    {
    }

    public override void Update(double deltaTime)
    {
    }

    /// <summary>
    ///     Responds to Chrome's POST request with a JSON object containing the firstRequest property.
    ///     This will be true if Chrome was already running when the plugin was enabled, false otherwise.
    ///     This is used to determine whether or not to send the entire list of tabs to the plugin.
    /// </summary>
    private object Respond()
    {
        var response = new
        {
            firstRequest = _firstRun
        };

        _firstRun = false;

        return response;
    }

    private void OnSetAllTabs(Tab[] data)
    {
        DataModel.Tabs.Clear();
        DataModel.Tabs.AddRange(data);
    }

    private void OnSetFullScreen(bool data)
    {
        DataModel.IsInFullscreen = data;
    }

    private void OnTabUpdated(TabUpdated data)
    {
        var updatedTab = DataModel.Tabs.Find(v => v.Id == data.TabId);
        if (updatedTab == null)
            return;

        var previousFavicon = updatedTab.FavIconUrl;

        JsonConvert.PopulateObject(JsonConvert.SerializeObject(data.ChangeInfo), updatedTab);

        if (updatedTab.FavIconUrl != previousFavicon)
        {
            updatedTab.ColorCalculated = false;
        }

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
            //tab is loading or otherwise unavailable
            if (tab.Status != "complete")
                continue;

            //tab has already been calculated, skip
            if (tab.ColorCalculated)
                continue;

            //tab has no favicon
            if (string.IsNullOrEmpty(tab.FavIconUrl))
            {
                tab.FavIconColors = default;
                tab.ColorCalculated = true;
                continue;
            }

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

    #region FavIconColors

    [GeneratedRegex("data:(?<type>.+?);base64,(?<data>.+)")]
    private static partial Regex DataUriRegex();

    private async Task<ColorSwatch> GetFavIconColors(string url)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("Value cannot be null or empty.", nameof(url));

        await _semaphore.WaitAsync();

        try
        {
            if (_cache.TryGetValue(url, out var colors))
                return colors;

            var newSwatch = await GetFavIconColorsFromUri(url);
            _cache[url] = newSwatch;
            return newSwatch;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<ColorSwatch> GetFavIconColorsFromUri(string url)
    {
        //TODO: this seems to work for *.ico but some PNGs broke. Still, no more concurrency issues
        var matches = DataUriRegex().Match(url);
        if (matches.Success)
        {
            if (matches.Groups.Count < 3)
                throw new Exception("Invalid DataUrl format");
            if (matches.Groups["type"].Value != "image/svg+xml")
                throw new Exception("Invalid DataUrl format");
            await using var svgStream = new MemoryStream(Convert.FromBase64String(matches.Groups["base64svg"].Value));

            return GetColorsFromSvg(svgStream);
        }

        if (url.Contains(".svg"))
        {
            await using var svgStream = await GetStreamFromUrl(url);
            return GetColorsFromSvg(svgStream);
        }

        await using var stream = await GetStreamFromUrl(url);
        var codec = SKCodec.Create(stream);

        if (codec == null)
        {
            //we failed to get the image with the bot user agent, try again without user agent
            //hack: using the stream directly doesn't work for some reason
            await using var stream3 = await _httpClient.GetStreamAsync(url);
            var ms = new MemoryStream();
            await stream3.CopyToAsync(ms);
            var arr = ms.ToArray();
            codec = SKCodec.Create(new MemoryStream(arr));
            if (codec == null)
                throw new Exception("Invalid image format");
        }

        using var skBitmap = SKBitmap.Decode(codec);
        var colors = ColorQuantizer.Quantize(skBitmap.Pixels, 256);
        codec.Dispose();
        return ColorQuantizer.FindAllColorVariations(colors, true);
    }

    private async Task<Stream> GetStreamFromUrl(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)");
        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadAsStreamAsync();
    }

    private static ColorSwatch GetColorsFromSvg(Stream stream)
    {
        var svg = new SKSvg();
        svg.Load(stream);

        using var bitmap = new SKBitmap(128, 128);
        using var canvas = new SKCanvas(bitmap);

        canvas.DrawPicture(svg.Picture);
        canvas.Flush();
        canvas.Save();

        var skClrs = ColorQuantizer.Quantize(bitmap.Pixels, 256);
        return ColorQuantizer.FindAllColorVariations(skClrs, true);
    }

    #endregion

}