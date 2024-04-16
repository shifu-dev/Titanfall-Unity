﻿using System;
using System.Diagnostics.Contracts;
using GameFramework.Extensions;
using UnityEngine;
using UnityEngine.Playables;

class CharacterMovementGroundModule : CharacterMovementModule
{
    protected struct GroundResult
    {
        public static readonly GroundResult invalid = new GroundResult();

        public GameObject gameObject => collider ? collider.gameObject : null;
        public Collider collider;
        public int layer => gameObject ? gameObject.layer : 0;

        public Vector3 direction;
        public float distance;
        public float angle;
        public float edgeDistance;

        public Vector3 basePosition;
        public Quaternion baseRotation;

        public bool isValid
        {
            get => collider != null;
        }
    }

    protected enum MovementState : byte
    {
        Idle,
        Walking,
        Running,
        Sprinting
    }

    protected enum LocomotionState : byte
    {
        Standing,
        Crouching,
        Proning,
        Jumping
    }

    public CharacterMovementGroundModule(CharacterAsset charAsset)
    {
        Contract.Assume(charAsset is not null);

        _prevGroundResult = GroundResult.invalid;
        _groundResult = GroundResult.invalid;

        _groundCheckDepth = charAsset.groundCheckDepth;
        _groundLayer = charAsset.groundLayer;
        _minMoveDistance = charAsset.groundMinMoveDistance;

        _standDeacceleration = charAsset.groundStandIdleAcceleration;
        _standWalkSpeed = charAsset.groundStandWalkSpeed;
        _standWalkAcceleration = charAsset.groundStandWalkAcceleration;
        _standRunSpeed = charAsset.groundStandRunSpeed;
        _standRunAcceleration = charAsset.groundStandRunAcceleration;
        _standSprintSpeed = charAsset.groundStandSprintSpeed;
        _standSprintAcceleration = charAsset.groundStandSprintAcceleration;
        _standSprintLeftAngleMax = charAsset.groundStandSprintLeftAngleMax;
        _standSprintRightAngleMax = charAsset.groundStandSprintRightAngleMax;
        _standJumpForce = charAsset.groundStandJumpForce;
        _standStepUpPercent = charAsset.groundStandStepUpPercent;
        _standStepUpHeight = _capsule.height * _standStepUpPercent / 100f;
        _standStepDownPercent = charAsset.groundStandStepDownPercent;
        _standStepDownHeight = _capsule.height * _standStepDownPercent / 100f;
        _standSlopeUpAngle = charAsset.groundStandSlopeUpAngle;
        _standSlopeDownAngle = charAsset.groundStandSlopeDownAngle;
        _standMaintainVelocityOnSurface = charAsset.groundStandMaintainVelocityOnSurface;
        _standMaintainVelocityAlongSurface = charAsset.groundStandMaintainVelocityAlongSurface;
        _standCapsuleCenter = charAsset.groundStandCapsuleCenter;
        _standCapsuleHeight = charAsset.groundStandCapsuleHeight;
        _standCapsuleRadius = charAsset.groundStandCapsuleRadius;
        _standToCrouchTransitionSpeed = charAsset.groundStandToCrouchTransitionSpeed;

        _crouchDeacceleration = charAsset.groundCrouchIdleAcceleration;
        _crouchWalkSpeed = charAsset.groundCrouchWalkSpeed;
        _crouchWalkAcceleration = charAsset.groundCrouchWalkAcceleration;
        _crouchRunSpeed = charAsset.groundCrouchRunSpeed;
        _crouchRunAcceleration = charAsset.groundCrouchRunAcceleration;
        _crouchJumpForce = charAsset.groundCrouchJumpForce;
        _crouchStepUpPercent = charAsset.groundCrouchStepUpPercent;
        _crouchStepUpHeight = _capsule.height * _crouchStepUpPercent / 100f;
        _crouchStepDownPercent = charAsset.groundCrouchStepDownPercent;
        _crouchStepUpHeight = _capsule.height * _crouchStepUpPercent / 100f;
        _crouchSlopeUpAngle = charAsset.groundCrouchSlopeUpAngle;
        _crouchSlopeDownAngle = charAsset.groundCrouchSlopeDownAngle;
        _crouchMaintainVelocityOnSurface = charAsset.groundCrouchMaintainVelocityOnSurface;
        _crouchMaintainVelocityAlongSurface = charAsset.groundCrouchMaintainVelocityAlongSurface;
        _crouchCapsuleCenter = charAsset.groundCrouchCapsuleCenter;
        _crouchCapsuleHeight = charAsset.groundCrouchCapsuleHeight;
        _crouchCapsuleRadius = charAsset.groundCrouchCapsuleRadius;
        _crouchToStandTransitionSpeed = charAsset.groundCrouchToStandTransitionSpeed;
    }

    //// -------------------------------------------------------------------------------------------
    //// CharacterBehaviour events
    //// -------------------------------------------------------------------------------------------

    public override void OnLoaded(CharacterMovement charMovement)
    {
        base.OnLoaded(charMovement);

        if (_character is not null)
        {
            _charView = _character.charView;
        }
    }

    public override void OnUnloaded(CharacterMovement charMovement)
    {
        base.OnUnloaded(charMovement);

        _charView = null;
    }

    public override bool ShouldRun()
    {        
        _baseDeltaPosition = Vector3.zero;
        _baseDeltaRotation = Vector3.zero;

        if (_groundResult.isValid)
        {
            _baseDeltaPosition = _groundResult.collider.transform.position - _groundResult.basePosition;
            _baseDeltaRotation = _groundResult.collider.transform.rotation.eulerAngles - _groundResult.baseRotation.eulerAngles;
        }

        if (_baseDeltaPosition != Vector3.zero || _baseDeltaRotation != Vector3.zero)
        {
            return true;
        }

        return _groundResult.isValid;
    }

    public override void RunPhysics()
    {
        base.RunPhysics();

        PullPhysicsData();
        _RecoverFromBaseMove();

        _UpdateValues();

        Vector3 moveInput = new Vector3(_inputMove.x, 0, _inputMove.y);
        moveInput = Quaternion.Euler(0, _charView.turnAngle, 0) * moveInput.normalized;
        moveInput = _character.rotation * moveInput;

        _velocity = Vector3.ProjectOnPlane(_velocity, _charUp);

        Vector3 move = moveInput * _moveSpeed * _deltaTime;
        move = Vector3.MoveTowards(_velocity * _deltaTime, move, _moveAccel * _deltaTime);

        move += _charUp * _jumpPower * _deltaTime;

        _GroundMove(move);

        _lastMovementState = _movementState;
        _lastLocomotionState = _locomotionState;

        PushPhysicsData();
    }

    //// -------------------------------------------------------------------------------------------
    //// Commands to control ground movement of character.
    //// -------------------------------------------------------------------------------------------

    public void Walk()
    {
        _movementState = MovementState.Walking;
    }

    public void Run()
    {
        _movementState = MovementState.Running;
    }

    public void Sprint()
    {
        _movementState = MovementState.Sprinting;
    }

    public void Stand()
    {
        _locomotionState = LocomotionState.Standing;
    }

    public void Crouch()
    {
        _locomotionState = LocomotionState.Crouching;
    }

    public void Prone()
    {
        _locomotionState = LocomotionState.Proning;
    }

    public void Jump()
    {
        _locomotionState = LocomotionState.Jumping;
    }

    public bool CanStandOnGround(RaycastHit hit, Vector3 slopeNormal, out float slopeAngle)
    {
        return _CanStandOn(hit, slopeNormal, out slopeAngle);
    }

    //// -------------------------------------------------------------------------------------------
    //// Movement implementation
    //// -------------------------------------------------------------------------------------------

    protected void _UpdateValues()
    {
        switch (_locomotionState)
        {
            case LocomotionState.Standing:
                _stepDownDepth = _standStepDownHeight;
                _stepUpHeight = _standStepUpHeight;
                _maxSlopeUpAngle = _standSlopeUpAngle;
                _slopeDownAngle = _standSlopeDownAngle;
                _maintainVelocityOnSurface = _standMaintainVelocityOnSurface;
                _maintainVelocityAlongSurface = _standMaintainVelocityAlongSurface;
                _jumpPower = 0f;

                if (_locomotionState == LocomotionState.Jumping)
                    _jumpPower = _standJumpForce;

                switch (_movementState)
                {
                    case MovementState.Idle:
                        _moveAccel = _standDeacceleration;
                        _moveSpeed = 0;
                        break;

                    case MovementState.Walking:
                        _moveAccel = _standWalkAcceleration;
                        _moveSpeed = _standWalkSpeed;
                        break;

                    case MovementState.Running:
                        _moveAccel = _standRunAcceleration;
                        _moveSpeed = _standRunSpeed;
                        break;

                    case MovementState.Sprinting:
                        _moveAccel = _standSprintAcceleration;
                        _moveSpeed = _standSprintSpeed;
                        break;

                    default:
                        _moveAccel = 0;
                        _moveSpeed = 0;
                        break;
                }

                break;

            case LocomotionState.Crouching:
                _stepDownDepth = _crouchStepDownDepth;
                _stepUpHeight = _crouchStepUpHeight;
                _maxSlopeUpAngle = _crouchSlopeUpAngle;
                _slopeDownAngle = _crouchSlopeDownAngle;
                _maintainVelocityOnSurface = _crouchMaintainVelocityOnSurface;
                _maintainVelocityAlongSurface = _crouchMaintainVelocityAlongSurface;
                _jumpPower = 0f;

                if (_locomotionState == LocomotionState.Jumping)
                    _jumpPower = _crouchJumpForce;

                switch (_movementState)
                {
                    case MovementState.Idle:
                        _moveAccel = _crouchDeacceleration;
                        _moveSpeed = 0;
                        break;

                    case MovementState.Walking:
                        _moveAccel = _crouchWalkAcceleration;
                        _moveSpeed = _crouchWalkSpeed;
                        break;

                    case MovementState.Running:
                        _moveAccel = _crouchRunAcceleration;
                        _moveSpeed = _crouchRunSpeed;
                        break;

                    default:
                        _moveAccel = 0;
                        _moveSpeed = 0;
                        break;
                }

                break;
        }

        _maxSlopeUpAngle = Math.Clamp(_maxSlopeUpAngle,
            _MIN_SLOPE_ANGLE, _MAX_SLOPE_ANGLE);
    }

    protected void _GroundMove(Vector3 originalMove)
    {
        _UpdateCapsuleSize();

        Vector3 moveH = Vector3.ProjectOnPlane(originalMove, _charUp);
        Vector3 moveV = originalMove - moveH;
        Vector3 remainingMove = moveH;
        float moveVMag = moveV.magnitude;

        // perform the vertical move (usually jump)
        if (moveVMag > 0f)
        {
            CapsuleMove(moveV);
        }

        if (remainingMove.magnitude > _minMoveDistance)
        {
            var stepUpHeight = 0f;
            var canStepUp = moveVMag == 0f;
            var didStepUp = false;
            var didStepUpRecover = false;
            var positionBeforeStepUp = Vector3.zero;
            var moveBeforeStepUp = Vector3.zero;

            CapsuleResolvePenetration();

            for (uint it = 0; it < _MAX_MOVE_ITERATIONS; it++)
            {
                remainingMove -= CapsuleMove(remainingMove, out RaycastHit moveHit, out Vector3 moveHitNormal);

                // perform step up recover
                if (didStepUp && !didStepUpRecover)
                {
                    didStepUp = false;
                    didStepUpRecover = true;

                    CapsuleMove(_character.down * stepUpHeight, out RaycastHit stepUpRecoverHit, out Vector3 stepUpRecoverHitNormal);

                    if (stepUpRecoverHit.collider)
                    {
                        // if we cannot step on this _ground, revert the step up
                        // and continue the loop without stepping up this time
                        if (_CanStandOn(stepUpRecoverHit, stepUpRecoverHitNormal, out float baseAngle) == false)
                        {
                            if (baseAngle < 90f)
                            {
                                _capsule.position = positionBeforeStepUp;
                                remainingMove = moveBeforeStepUp;
                                canStepUp = false;

                                continue;
                            }
                        }
                    }
                }

                // if there is no collision (no obstacle or remainingMove == 0)
                // break the loop
                if (moveHit.collider is null)
                {
                    break;
                }

                // try sliding on the obstacle
                if (_SlideOnSurface(originalMove, ref remainingMove, moveHit, moveHitNormal))
                {
                    continue;
                }

                // step up the first time, we hit an obstacle
                if (canStepUp && didStepUp == false)
                {
                    canStepUp = false;
                    didStepUp = true;
                    didStepUpRecover = false;
                    positionBeforeStepUp = _capsule.position;
                    moveBeforeStepUp = remainingMove;

                    stepUpHeight = CapsuleMove(_charUp * _stepUpHeight).magnitude;

                    continue;
                }

                // try sliding along the obstacle
                if (_SlideAlongSurface(originalMove, ref remainingMove, moveHit, moveHitNormal))
                {
                    continue;
                }

                // there's nothing we can do now, so stop the move
                remainingMove = Vector3.zero;
            }
        }

        _StepDown(originalMove);
        CapsuleResolvePenetration();
    }

    protected void _RecoverFromBaseMove()
    {
        if (_baseDeltaPosition != Vector3.zero)
        {
            _capsule.position += _baseDeltaPosition;
            _baseDeltaPosition = Vector3.zero;
        }

        // TODO: update position for base rotation also
    }

    protected void _UpdateCapsuleSize()
    {
        float weight = 0;
        float speed = 0;
        float targetHeight = 0;
        float targetRadius = 0;
        Vector3 targetCenter = Vector3.zero;

        if (_locomotionState == LocomotionState.Crouching)
        {
            targetCenter = _crouchCapsuleCenter;
            targetHeight = _crouchCapsuleHeight;
            targetRadius = _crouchCapsuleRadius;
            speed = _standToCrouchTransitionSpeed * _deltaTime;
        }
        else
        {
            targetCenter = _standCapsuleCenter;
            targetHeight = _standCapsuleHeight;
            targetRadius = _standCapsuleRadius;
            speed = _crouchToStandTransitionSpeed * _deltaTime;
        }

        // charCapsule.localPosition += charCapsule.up * Mathf.MoveTowards(charCapsule.localHeight, targetHeight, speed);
        // _capsule.center = Vector3.Lerp(mCapsule.center, targetCenter, speed);
        _capsule.height = Mathf.Lerp(_capsule.height, targetHeight, speed);
        _capsule.radius = Mathf.Lerp(_capsule.radius, targetRadius, speed);

        weight = Mathf.Lerp(weight, 1f, speed);
        // _movementStateWeight = weight;
    }

    protected bool _SlideOnSurface(Vector3 originalMove, ref Vector3 remainingMove, RaycastHit hit, Vector3 hitNormal)
    {
        if (remainingMove == Vector3.zero)
            return false;

        if (_CanStandOn(hit, hitNormal, out float slopeAngle))
        {
            if (slopeAngle == 0f)
            {
                return false;
            }

            Plane plane = new Plane(hitNormal, hit.point);
            Ray ray = new Ray(hit.point + remainingMove, _charUp);
            plane.Raycast(ray, out float enter);

            Vector3 slopeMove = remainingMove + (_charUp * enter);

            if (_maintainVelocityOnSurface == false)
            {
                slopeMove = slopeMove.normalized * remainingMove.magnitude;
            }

            remainingMove = slopeMove;
            return true;
        }

        return false;
    }

    protected bool _SlideAlongSurface(Vector3 originalMove, ref Vector3 remainingMove, RaycastHit hit, Vector3 hitNormal)
    {
        float remainingMoveSize = remainingMove.magnitude;

        if (hit.collider is null || remainingMoveSize == 0f)
            return false;

        RecalculateNormalIfZero(hit, ref hitNormal);

        Vector3 hitProject = Vector3.ProjectOnPlane(hitNormal, _charUp);
        Vector3 slideMove = Vector3.ProjectOnPlane(originalMove.normalized * remainingMoveSize, hitProject);
        if (_maintainVelocityAlongSurface)
        {
            // to avoid sliding along perpendicular surface for very small values,
            // may be a result of small miscalculations
            if (slideMove.magnitude > _MIN_MOVE_ALONG_SURFACE_TO_MAINTAIN_VELOCITY)
            {
                slideMove = slideMove.normalized * remainingMoveSize;
            }
        }

        remainingMove = slideMove;
        return true;
    }

    protected bool _StepDown(Vector3 originalMove)
    {
        var verticalMove = Vector3.Project(originalMove, _charUp).magnitude;
        if (verticalMove != 0f || _stepDownDepth <= 0)
            return false;

        CapsuleResolvePenetration();

        var moved = CapsuleMove(_character.down * _stepDownDepth, out RaycastHit hit, out Vector3 hitNormal);

        if (_CanStandOn(hit, hitNormal) == false)
        {
            _capsule.position -= moved;
            return false;
        }

        return true;
    }

    protected bool _CheckIsGround(Collider collider)
    {
        if (collider is null)
        {
            return false;
        }

        return _groundLayer.Contains(collider.gameObject.layer);
    }

    protected bool _CanStandOn(RaycastHit hit)
    {
        return _CanStandOn(hit, Vector3.zero);
    }

    protected bool _CanStandOn(RaycastHit hit, Vector3 slopeNormal)
    {
        return _CanStandOn(hit, slopeNormal, out float slopeAngle);
    }

    protected bool _CanStandOn(RaycastHit hit, Vector3 slopeNormal, out float slopeAngle)
    {
        slopeAngle = 0f;

        if (hit.collider is not null)
        {
            if (_CheckIsGround(hit.collider) is false)
            {
                return false;
            }

            RecalculateNormalIfZero(hit, ref slopeNormal);

            slopeAngle = Vector3.Angle(_charUp, slopeNormal);
            if (slopeAngle >= _MIN_SLOPE_ANGLE && slopeAngle <= _maxSlopeUpAngle)
            {
                return true;
            }
        }

        return false;
    }

    protected bool _CastForGround(float depth, out GroundResult result)
    {
        BaseSphereCast(_character.down * depth, out RaycastHit hit, out Vector3 hitNormal);
        if (hitNormal == Vector3.zero)
        {
            hitNormal = hit.normal;
        }

        result = new GroundResult();

        if (_CanStandOn(hit, hitNormal, out float slopeAngle) == false)
        {
            if (slopeAngle < 90f)
            {
                return false;
            }
        }

        result.collider = hit.collider;
        result.direction = _character.down;
        result.distance = hit.distance;

        result.angle = slopeAngle;

        result.basePosition = result.collider.transform.position;
        result.baseRotation = result.collider.transform.rotation;

        result.edgeDistance = default;

        return true;
    }

    protected void _UpdateGroundResult()
    {
        _prevGroundResult = _groundResult;
        _CastForGround(_groundCheckDepth, out _groundResult);
    }

    //// -------------------------------------------------------------------------------------------
    //// Properties and Fields
    //// -------------------------------------------------------------------------------------------

    protected const uint _MAX_MOVE_ITERATIONS = 10;
    protected const float _MIN_MOVE_ALONG_SURFACE_TO_MAINTAIN_VELOCITY = .0001f;
    protected const float _MIN_SLOPE_ANGLE = 0f;
    protected const float _MAX_SLOPE_ANGLE = 89.9f;

    protected CharacterMovementGroundModuleSource _source;
    protected CharacterView _charView;
    protected GroundResult _groundResult;
    protected GroundResult _prevGroundResult;

    protected Vector3 _baseDeltaPosition;
    protected Vector3 _baseDeltaRotation;

    protected LocomotionState _locomotionState;         // current state to process
    protected MovementState _movementState;             // current state to process
    protected LocomotionState _lastLocomotionState;     // last processed state, could be same as current state
    protected MovementState _lastMovementState;         // last processed state, could be same as current state
    protected LocomotionState _prevLocomotionState;     // previous state, different from current state
    protected MovementState _prevMovementState;         // previous state, different from current state

    protected float _moveSpeed = 0;
    protected float _moveAccel = 0;
    protected float _jumpPower = 0;
    protected float _stepUpHeight = 0;
    protected float _stepDownDepth = 0;
    protected float _maxSlopeUpAngle = 0;
    protected float _slopeDownAngle = 0;
    protected bool _maintainVelocityOnSurface = true;
    protected bool _maintainVelocityAlongSurface = true;

    //// -------------------------------------------------------------------------------------------
    //// Cached values from charAsset asset
    //// -------------------------------------------------------------------------------------------

    protected readonly LayerMask _groundLayer;
    protected readonly float _minMoveDistance;
    protected readonly float _groundCheckDepth;

    protected readonly float _standDeacceleration;
    protected readonly float _standWalkSpeed;
    protected readonly float _standWalkAcceleration;
    protected readonly float _standRunSpeed;
    protected readonly float _standRunAcceleration;
    protected readonly float _standSprintSpeed;
    protected readonly float _standSprintAcceleration;
    protected readonly float _standSprintLeftAngleMax;
    protected readonly float _standSprintRightAngleMax;
    protected readonly float _standJumpForce;
    protected readonly float _standStepUpPercent;
    protected readonly float _standStepDownPercent;
    protected readonly float _standStepUpHeight;
    protected readonly float _standStepDownHeight;
    protected readonly float _standSlopeUpAngle;
    protected readonly float _standSlopeDownAngle;
    protected readonly float _standCapsuleHeight;
    protected readonly float _standCapsuleRadius;
    protected readonly float _standToCrouchTransitionSpeed;
    protected readonly bool _standMaintainVelocityOnSurface;
    protected readonly bool _standMaintainVelocityAlongSurface;
    protected readonly Vector3 _standCapsuleCenter;

    protected readonly float _crouchDeacceleration;
    protected readonly float _crouchWalkSpeed;
    protected readonly float _crouchWalkAcceleration;
    protected readonly float _crouchRunSpeed;
    protected readonly float _crouchRunAcceleration;
    protected readonly float _crouchJumpForce;
    protected readonly float _crouchStepUpPercent;
    protected readonly float _crouchStepDownPercent;
    protected readonly float _crouchStepUpHeight;
    protected readonly float _crouchStepDownDepth;
    protected readonly float _crouchSlopeUpAngle;
    protected readonly float _crouchSlopeDownAngle;
    protected readonly float _crouchCapsuleHeight;
    protected readonly float _crouchCapsuleRadius;
    protected readonly float _crouchToStandTransitionSpeed;
    protected readonly bool _crouchMaintainVelocityOnSurface;
    protected readonly bool _crouchMaintainVelocityAlongSurface;
    protected readonly Vector3 _crouchCapsuleCenter;
}