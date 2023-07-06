using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class ClosedTabData : DataModelEventArgs
{
  public int TabId { get; set; }
}