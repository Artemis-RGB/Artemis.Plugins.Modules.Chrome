using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class ActivatedTabData : DataModelEventArgs
{
  public int TabId { get; set; }
}