namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class AttachedTabData
{
  public int TabId { get; set; }
  public AttachInfo AttachInfo { get; set; }
}

public class AttachInfo
{
  public int newPosition { get; set; }
  public int newWindowId { get; set; }
}