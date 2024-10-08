﻿using UnityEngine;

namespace Basis.Scripts.Animator_Driver
{
[System.Serializable]
public struct BasisAnimatorVariables
{
    public float cachedAnimSpeed;
    public bool cachedIsMoving;
    public float cachedHorizontalMovement;
    public float cachedVerticalMovement;
    public bool IsJumping;
    public bool IsFalling;
    public bool cachedIsJumping;
    public bool cachedIsFalling;
    public bool IsCrouching;
    public bool cachedIsCrouching;
    public float AnimationsCurrentSpeed;
    public bool isMoving;
    public Vector3 AngularVelocity;
    public Vector3 Velocity;
}
}