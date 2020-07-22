using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreativeJump : FastJump, ISneakMove
{
    [SerializeField]
    private float _creativeActivationTime_ = 0.2f;
    [SerializeField]
    private float _creativeVerticalSpeed_ = 5;

    [SerializeField]
    bool _isCreative;
    bool _jumping;
    bool _sneaking;

    public override float Jump(float currentYVelocity, bool isGrounded)
    {
        if (_isCreative)
            return (_jumping ? _creativeVerticalSpeed_ : 0) - (_sneaking ? _creativeVerticalSpeed_ : 0);

        return base.Jump(currentYVelocity, isGrounded);
    }

    public override void ApplyJumpInput(bool jump, bool jumpContinuous)
    {
        float time = Time.realtimeSinceStartup;
        if (jump)
        {
            if (time - _JumpedTime < _creativeActivationTime_)
                _isCreative = !_isCreative;                
            _JumpedTime = time;
        }
        _jumping = jumpContinuous;
    }

    public void ApplySneakingInput(bool sneak, bool sneakContinuous)
    {
        _sneaking = sneakContinuous;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        GetComponent<CreatureController>().SetSneaking(this, true);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        GetComponent<CreatureController>().SetSneaking(this, false);
    }
}
