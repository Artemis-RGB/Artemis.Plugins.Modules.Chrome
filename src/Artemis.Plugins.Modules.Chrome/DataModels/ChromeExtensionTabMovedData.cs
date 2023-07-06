namespace Artemis.Plugins.Modules.Chrome.DataModels;

public class TabMovedData
{
  public int TabId { get; set; }
  public MoveInfo MoveInfo { get; set; }
}

public class MoveInfo
{
  public int FromIndex { get; set; }
  public int ToIndex { get; set; }
  public int WindowId { get; set; }
}