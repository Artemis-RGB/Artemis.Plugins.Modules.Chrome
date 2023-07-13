using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class TabMoved : DataModelEventArgs
{
  public int TabId { get; set; }
  public MoveInfo MoveInfo { get; set; }
}