namespace Artemis.Plugins.Modules.Chrome.DataModels;

using Artemis.Core;
using Newtonsoft.Json;

public class UpdatedTabData : DataModelEventArgs
{
  public int TabId { get; set; }
  public ChangeInfo ChangeInfo { get; set; }
}

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class ChangeInfo
{
  public bool? Audible { get; set; }
  public bool? AutoDiscardable { get; set; }
  public bool? Discarded { get; set; }
  public string? FavIconUrl { get; set; }
  public int? GroupId { get; set; }
  public MutedInfo? MutedInfo { get; set; }
  public bool? Pinned { get; set; }
  public string? Status { get; set; }
  public string? Title { get; set; }
  public string? Url { get; set; }
}