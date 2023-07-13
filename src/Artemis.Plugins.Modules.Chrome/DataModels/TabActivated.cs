using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class TabActivated : DataModelEventArgs
{
  public int TabId { get; set; }
}