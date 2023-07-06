using System.Collections.Generic;
using Artemis.Core;
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

  public DataModelEvent<UpdatedTabData> OnTabUpdated { get; } = new DataModelEvent<UpdatedTabData>();
  public DataModelEvent<ActivatedTabData> OnTabActivated { get; } = new DataModelEvent<ActivatedTabData>();
  public DataModelEvent<AttachedTabData> OnTabAttached { get; } = new DataModelEvent<AttachedTabData>();
  public DataModelEvent<ChromeExtensionTab> OnNewTab { get; } = new DataModelEvent<ChromeExtensionTab>();
  public DataModelEvent<TabMovedData> OnTabMoved { get; } = new DataModelEvent<TabMovedData>();
  public DataModelEvent<ClosedTabData> OnTabClosed { get; } = new DataModelEvent<ClosedTabData>();
}