using UnityEngine;

public struct Byte3
{
    public byte x, y, z;

    public Byte3(byte x, byte y, byte z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }

    public byte this[int i]
    {
        get
        {
            switch (i)
            {
                case 0: return x;
                case 1: return y;
                default: return z;
            }
        }
        set
        {
            switch (i)
            {
                case 0: x = value; return;
                case 1: y = value; return;
                default: z = value; return;
            }
        }
    }
}