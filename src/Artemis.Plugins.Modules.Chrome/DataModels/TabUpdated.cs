namespace Artemis.Plugins.Modules.Chrome.DataModels;

using Artemis.Core;

public class TabUpdated : DataModelEventArgs
{
  public int TabId { get; set; }
  public ChangeInfo ChangeInfo { get; set; }
}