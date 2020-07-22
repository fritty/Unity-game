using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FastJump : DefaultJump
{
    [SerializeField]
    protected float _JumpMultiplier_ = 2;
    [SerializeField]
    protected float _FallMultiplier_ = 3;

    float _upGravity;
    float _downGravity;
    float _upTime;
    float _downTime;

    protected override void Recalculate()
    {
        _upGravity = Physics.gravity.y * _JumpMultiplier_;
        _downGravity = Physics.gravity.y * _FallMultiplier_;

        _JumpSpeed = Mathf.Sqrt(2 * _JumpHeight_ * -_upGravity);
        
        _upTime = _JumpSpeed / -_upGravity;
        _downTime = Mathf.Sqrt(2 * _JumpHeight_ * -_downGravity) / -_downGravity;
    }

    protected override float GravityFunction()
    {
        if (_PreviousJumpTime >= _GroundedTime)
        {
            if (_Time - _PreviousJumpTime < _upTime)
                return _upGravity;
            if (_Time - _PreviousJumpTime < _upTime + _downTime)
                return _downGravity;
        }
        return base.GravityFunction();
    }
}
