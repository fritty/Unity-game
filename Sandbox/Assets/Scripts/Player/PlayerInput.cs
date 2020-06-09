using UnityEngine;

public class PlayerInput : MonoBehaviour , ICreatureInput
{   
    public bool Updated { get; private set; }    
    public int DirectionX { get; private set; }
    public int DirectionZ { get; private set; }
    public bool Jump { get; private set; }
    public int RotationHorizontal { get; private set; }
    public int RotationVertical { get; private set; } 
    public bool UseContinuous { get; private set; }
    public bool UseSingle { get; private set; }

    public KeyBindings Keybindings;

    private void Awake()
    {
        if (Keybindings == null)
            Keybindings = (KeyBindings)ScriptableObject.CreateInstance(typeof(KeyBindings));
    }

    private void Update()
    {
        Updated = Input.anyKey;                       
        DirectionX = Input.GetKey(Keybindings.DirectionXPositive) ? 1 : Input.GetKey(Keybindings.DirectionXNegative) ? -1 : 0;
        DirectionZ = Input.GetKey(Keybindings.DirectionZPositive) ? 1 : Input.GetKey(Keybindings.DirectionZNegative) ? -1 : 0;
        Jump = Input.GetKeyDown(Keybindings.Jump); 
        RotationHorizontal = Input.GetKey(Keybindings.RotationHorizontalPositive) ? 1 : Input.GetKey(Keybindings.RotationHorizontalNegative) ? -1 : 0;
        RotationVertical = Input.GetKey(Keybindings.RotationVerticalPositive) ? 1 : Input.GetKey(Keybindings.RotationVerticalNegative) ? -1 : 0;
        UseContinuous = Input.GetKey(Keybindings.Use);
        UseSingle = Input.GetKeyDown(Keybindings.Use);                 
    }
}

[CreateAssetMenu(fileName = "Key Bindings")]
public class KeyBindings : ScriptableObject
{
    public KeyCode DirectionXPositive = KeyCode.D;
    public KeyCode DirectionXNegative = KeyCode.A;
    public KeyCode DirectionZPositive = KeyCode.W;
    public KeyCode DirectionZNegative = KeyCode.S;
    public KeyCode Jump = KeyCode.Space;
    public KeyCode RotationHorizontalPositive = KeyCode.RightArrow;
    public KeyCode RotationHorizontalNegative = KeyCode.LeftArrow;
    public KeyCode RotationVerticalPositive = KeyCode.DownArrow;
    public KeyCode RotationVerticalNegative = KeyCode.UpArrow;
    public KeyCode Use = KeyCode.E;
}
