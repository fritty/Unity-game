using UnityEngine;

public class PlayerInput : MonoBehaviour , ICreatureInput
{
    public bool Updated => Input.anyKey;
    public bool Moved => DirectionX != 0 || DirectionZ != 0;
    public int DirectionX => Input.GetKey(Keybindings.DirectionXPositive) ? 1 : Input.GetKey(Keybindings.DirectionXNegative) ? -1 : 0;
    public int DirectionZ => Input.GetKey(Keybindings.DirectionZPositive) ? 1 : Input.GetKey(Keybindings.DirectionZNegative) ? -1 : 0;
    public bool Jump => Input.GetKeyDown(Keybindings.Jump);
    public bool JumpContinuous => Input.GetKey(Keybindings.Jump);
    public bool Sneak => Input.GetKeyDown(Keybindings.Sneak);
    public bool SneakContinuous => Input.GetKey(Keybindings.Sneak);
    public bool Rotated => RotationHorizontal != 0 || RotationVertical != 0;
    public int RotationHorizontal => Input.GetKey(Keybindings.RotationHorizontalPositive) ? 1 : Input.GetKey(Keybindings.RotationHorizontalNegative) ? -1 : 0;
    public int RotationVertical => Input.GetKey(Keybindings.RotationVerticalPositive) ? 1 : Input.GetKey(Keybindings.RotationVerticalNegative) ? -1 : 0;
    public bool Use => Input.GetKeyDown(Keybindings.Use);
    public bool UseContinuous => Input.GetKey(Keybindings.Use);
    

    public KeyBindings Keybindings;

    private void Awake()
    {
        if (Keybindings == null)
            Keybindings = FindObjectOfType<KeyBindings>();
    }
}
