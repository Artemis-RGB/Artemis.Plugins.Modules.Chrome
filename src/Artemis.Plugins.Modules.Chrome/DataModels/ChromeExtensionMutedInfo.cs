using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

// MutedInfo implementation from the Chrome APIs
// See here: https://developer.chrome.com/docs/extensions/reference/tabs/#type-MutedInfo

public class MutedInfo : DataModelEventArgs
{
  public string ExtensionId { get; set; } = "";
  public bool Muted { get; set; }
  public string Reason { get; set; } = "";
}