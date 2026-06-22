using Xabbo;

namespace TotemEffects.Core;

record TotemPiece(Id Id, bool IsFromRoom)
{
    public string PlaceString(int x, int y) => $"{-Id} {x} {y} 0";
}