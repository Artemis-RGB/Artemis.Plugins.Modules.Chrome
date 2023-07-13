using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class TabClosed : DataModelEventArgs
{
  public int TabId { get; set; }
}