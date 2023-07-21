using Artemis.Core.Modules;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class YouTubeData
{
  [DataModelIgnore] public int? TabId { get; set; }
  public bool Music { get; set; }
}