using System.Collections.Generic;
using Artemis.Core;
using Artemis.Core.Modules;
using Newtonsoft.Json;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class ChromeDataModel : DataModel
{
  public bool IsInFullscreen { get; set; }
  public bool AnyTabAudible { get; set; }
  public bool ActiveTabAudible { get; set; }
  public Tab? ActiveTab { get; set; }

  [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
  public List<Tab> Tabs { get; set; } = new List<Tab>();

  public DataModelEvent<TabUpdated> OnTabUpdated { get; } = new DataModelEvent<TabUpdated>();
  public DataModelEvent<TabActivated> OnTabActivated { get; } = new DataModelEvent<TabActivated>();
  public DataModelEvent<Tab> OnNewTab { get; } = new DataModelEvent<Tab>();
  public DataModelEvent<TabMoved> OnTabMoved { get; } = new DataModelEvent<TabMoved>();
  public DataModelEvent<TabClosed> OnTabClosed { get; } = new DataModelEvent<TabClosed>();
}