﻿using Basis.Scripts.TransformBinders.BoneControl;
using System.Collections.Generic;
using UnityEngine;

namespace Basis.Scripts.Device_Management
{
[System.Serializable]
public class BasisDeviceMatchSettings
{
    public string DeviceID = string.Empty;
    [Header("Match with Ids")]
    public List<string> MatchableDeviceIds = new List<string>();
    [Header("Raycast Support")]
    public bool HasRayCastSupport = false;
    [Header("Phsyical Device")]
    public bool CanDisplayPhysicalTracker = false;
    [Header("Raycast Visuals")]
    public bool HasRayCastVisual = false;
    public bool HasRayCastRedical = false;

    [Header("Raycast Offsets")]
    public Vector3 RayCastOffset;
    public Vector3 RotationRaycastOffset;
    [Header("Avatar Offsets")]
    public Vector3 AvatarPositionOffset;
    public Vector3 AvatarRotationOffset;

    [Header("Tracked Role Override")]
    public bool HasTrackedRole = false;
    public BasisBoneTrackedRole TrackedRole;
}
}