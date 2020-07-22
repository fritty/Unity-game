using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CreatureController))]
public class DefaultJump : MonoBehaviour, IJumpMove
{
    [SerializeField]
    protected float _JumpHeight_ = 1;
    [SerializeField]
    [Tooltip("X - how long jump state is stored \nY - how long grounded state is stored")]
    protected Vector2 _JumpBuffer_ = Vector2.one * 0.2f;
    [SerializeField]
    protected float _JumpSpacing_ = 0.1f;

    protected float _JumpSpeed;

    protected float _JumpedTime;
    protected float _PreviousJumpTime;
    protected float _GroundedTime;
    protected float _Time;

    public virtual float Jump(float currentYVelocity, bool isGrounded)
    {
        float velocity = currentYVelocity;
        _Time = Time.realtimeSinceStartup;

        if (isGrounded)
            _GroundedTime = _Time;

        if ((_Time - _GroundedTime < _JumpBuffer_.y) &&
            (_Time - _JumpedTime < _JumpBuffer_.x) &&
            (_Time - _PreviousJumpTime > Mathf.Max(_JumpSpacing_, _JumpBuffer_.y)))
        {
            float jumpVelocity = JumpFunction();
            velocity = jumpVelocity;
            _PreviousJumpTime = _Time;
        }
        else
        {
            velocity += GravityFunction() * Time.fixedDeltaTime;
            if (velocity < 0 && isGrounded)
                velocity = 0;
        }

        return velocity;
    }

    public virtual void ApplyJumpInput(bool jump, bool jumpContinuous)
    {
        if (jump)
            _JumpedTime = Time.realtimeSinceStartup;
    }

    protected virtual float JumpFunction() => _JumpSpeed;

    protected virtual float GravityFunction() => Physics.gravity.y;

    protected virtual void Recalculate() => _JumpSpeed = Mathf.Sqrt(2 * _JumpHeight_ * -Physics.gravity.y);

    protected virtual void OnEnable() => GetComponent<CreatureController>().SetJumping(this, true);

    protected virtual void OnDisable() => GetComponent<CreatureController>().SetJumping(this, false);

    private void Awake() => Recalculate();

    private void OnValidate() => Recalculate();
}
