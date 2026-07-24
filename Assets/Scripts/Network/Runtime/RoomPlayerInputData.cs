using Fusion;
using UnityEngine;

public struct RoomPlayerInputData : INetworkInput
{
    private const byte ForwardFlag = 1 << 0;
    private const byte BackwardFlag = 1 << 1;
    private const byte LeftFlag = 1 << 2;
    private const byte RightFlag = 1 << 3;

    public byte DirectionFlags;

    public void SetForward(bool isPressed)
    {
        SetFlag(ForwardFlag, isPressed);
    }

    public void SetBackward(bool isPressed)
    {
        SetFlag(BackwardFlag, isPressed);
    }

    public void SetLeft(bool isPressed)
    {
        SetFlag(LeftFlag, isPressed);
    }

    public void SetRight(bool isPressed)
    {
        SetFlag(RightFlag, isPressed);
    }

    public Vector3 CreateMovementDirection()
    {
        float x = 0f;
        float z = 0f;

        if (HasFlag(LeftFlag))
        {
            x -= 1f;
        }

        if (HasFlag(RightFlag))
        {
            x += 1f;
        }

        if (HasFlag(BackwardFlag))
        {
            z -= 1f;
        }

        if (HasFlag(ForwardFlag))
        {
            z += 1f;
        }

        Vector3 direction = new Vector3(x, 0f, z);

        if (direction.sqrMagnitude > 1f)
        {
            direction.Normalize();
        }

        return direction;
    }

    private void SetFlag(byte flag, bool isPressed)
    {
        if (isPressed)
        {
            DirectionFlags |= flag;
            return;
        }

        DirectionFlags = (byte)(DirectionFlags & ~flag);
    }

    private bool HasFlag(byte flag)
    {
        return (DirectionFlags & flag) != 0;
    }
}
