using Artemis.Core;
using Artemis.Core.ColorScience;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

// Tab implementation from the Chrome APIs
// See here: https://developer.chrome.com/docs/extensions/reference/tabs/#type-Tab
public class ChromeExtensionTab : DataModelEventArgs
{
  public bool Active { get; set; }
  public bool Audible { get; set; }
  public bool AutoDiscardable { get; set; }
  public bool Discarded { get; set; }
  public string FavIconUrl { get; set; } = "";
  public int GroupId { get; set; }
  public int Height { get; set; }
  public bool Highlighted { get; set; }
  public int Id { get; set; }
  public bool Incognito { get; set; }
  public int Index { get; set; }
  public MutedInfo MutedInfo { get; set; } = new MutedInfo();
  public int OpenerTabId { get; set; }
  public string PendingUrl { get; set; } = "";
  public bool Pinned { get; set; }
  public string SessionId { get; set; } = "";
  public string Status { get; set; } = "";
  public string Title { get; set; } = "";
  public string URL { get; set; } = "";
  public int Width { get; set; }
  public int WindowId { get; set; }

  // The following are calculated by the plugin itself and not by the Chrome APIs
  public ColorSwatch? FavIconColors { get; set; }
}