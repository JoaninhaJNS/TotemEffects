using Xabbo;
using Xabbo.Core;

namespace TotemEffects.Core;

public class TotemSession
{
    public Id? HeadId { get; set; }
    public Id? BottomId { get; set; }
    public Id? CenterId { get; set; }
    public Point? TileForTotem { get; set; }
    public IFloorItem? WiredRepeatItem { get; set; }
    public IFloorItem? WiredVariableItem { get; set; }

    public void Reset()
    {
        HeadId = null;
        BottomId = null;
        CenterId = null;
        TileForTotem = null;
        WiredRepeatItem = null;
        WiredVariableItem = null;
    }
}