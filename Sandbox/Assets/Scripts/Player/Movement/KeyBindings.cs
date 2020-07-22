using UnityEngine;

[CreateAssetMenu]
public class KeyBindings : ScriptableObject
{
    public KeyCode DirectionXPositive = KeyCode.D;
    public KeyCode DirectionXNegative = KeyCode.A;
    public KeyCode DirectionZPositive = KeyCode.W;
    public KeyCode DirectionZNegative = KeyCode.S;
    public KeyCode Jump = KeyCode.Space;
    public KeyCode Sneak = KeyCode.LeftShift;
    public KeyCode RotationHorizontalPositive = KeyCode.RightArrow;
    public KeyCode RotationHorizontalNegative = KeyCode.LeftArrow;
    public KeyCode RotationVerticalPositive = KeyCode.DownArrow;
    public KeyCode RotationVerticalNegative = KeyCode.UpArrow;
    public KeyCode Use = KeyCode.E;
}
