using UnityEngine;

public interface IJumpMove
{
    float Jump(float currentYVelocity, bool isGrounded);
    void ApplyJumpInput(bool jump, bool jumpContinuous);
}
