/* Input interface */
public interface ICreatureInput
{
    bool Updated { get; }
    int DirectionX { get; }
    int DirectionZ { get; }
    bool Jump { get; }
    int RotationHorizontal { get; }
    int RotationVertical { get; }
    bool UseContinuous { get; }
    bool UseSingle { get; }
}
