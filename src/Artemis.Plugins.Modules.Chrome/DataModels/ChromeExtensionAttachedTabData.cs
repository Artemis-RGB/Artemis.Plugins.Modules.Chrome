using Artemis.Core;

namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class AttachedTabData : DataModelEventArgs
{
  public int TabId { get; set; }
  public AttachInfo AttachInfo { get; set; }
}

public class AttachInfo
{
  public int NewPosition { get; set; }
  public int NewWindowId { get; set; }
}