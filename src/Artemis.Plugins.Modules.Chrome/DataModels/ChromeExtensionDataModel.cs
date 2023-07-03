using Artemis.Core.Modules;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class ChromeExtensionDataModel : DataModel
{
  public bool IsInFullscreen { get; set; }
  public bool AnyTabAudible { get; set; }
  public bool ActiveTabAudible { get; set; }
}