using System.Collections.Generic;
using Artemis.Core.Modules;
using Newtonsoft.Json;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class ChromeExtensionDataModel : DataModel
{
  public bool IsInFullscreen { get; set; }
  public bool AnyTabAudible { get; set; }
  public bool ActiveTabAudible { get; set; }

  [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
  public List<ChromeExtensionTab> Tabs { get; set; } = new List<ChromeExtensionTab>();
}