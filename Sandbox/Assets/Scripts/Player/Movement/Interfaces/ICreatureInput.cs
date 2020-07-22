/* Input interface */
public interface ICreatureInput
{
    bool Updated { get; }

    bool Moved { get; }
    int DirectionX { get; }
    int DirectionZ { get; }
    bool Jump { get; }
    bool JumpContinuous { get; }
    bool Sneak { get; }
    bool SneakContinuous { get; }

    bool Rotated { get; }
    int RotationHorizontal { get; }
    int RotationVertical { get; }

    bool Use { get; }
    bool UseContinuous { get; }    
}
