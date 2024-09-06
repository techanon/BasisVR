﻿using System;
using Unity.Collections;
using UnityEngine;
using static JiggleRigConstruction;
namespace JigglePhysics
{
    [Serializable]
    public class JiggleRig : JiggleRigBase
    {
        [SerializeField]
        [Tooltip("The root bone from which an individual JiggleRig will be constructed. The JiggleRig encompasses all children of the specified root.")]
        public Transform rootTransform;
        [Tooltip("The settings that the rig should update with, create them using the Create->JigglePhysics->Settings menu option.")]
        public JiggleSettingsBase jiggleSettings;
        [SerializeField]
        [Tooltip("The list of transforms to ignore during the jiggle. Each bone listed will also ignore all the children of the specified bone.")]
        public Transform[] ignoredTransforms;
        public Collider[] colliders;
        [SerializeField]
        public JiggleSettingsData jiggleSettingsdata;
        public Transform GetRootTransform() => rootTransform;
        public bool NeedsCollisions;
        public int collidersCount;
        public Vector3 Zero = Vector3.zero;
        public void Initialize()
        {
            InitalizeLists(this);
            CreateSimulatedPoints(this, ignoredTransforms, rootTransform, null);
            InitalizeIndexes();
            simulatedPointsCount = JiggleBones.Length;
            // Precompute normalized indices in a single pass
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                JiggleBone test = JiggleBones[SimulatedIndex];
                int distanceToRoot = 0, distanceToChild = 0;

                // Calculate distance to root
                while (test.JiggleParentIndex != -1)
                {
                    test = JiggleBones[test.JiggleParentIndex];
                    distanceToRoot++;
                }

                test = JiggleBones[SimulatedIndex];
                // Calculate distance to child
                while (test.childIndex != -1)
                {
                    test = JiggleBones[test.childIndex];
                    distanceToChild++;
                }

                int max = distanceToRoot + distanceToChild;
                PreInitalData.normalizedIndex[SimulatedIndex] = (float)distanceToRoot / max;
            }

            InitializeNativeArrays();

            jiggleSettingsdata = jiggleSettings.GetData();
            NeedsCollisions = colliders.Length != 0;
        }

        private void InitializeNativeArrays()
        {
            // Consolidate NativeArray initialization into one method
            Runtimedata.boneRotationChangeCheck = new NativeArray<Quaternion>(PreInitalData.boneRotationChangeCheck.ToArray(), Allocator.Persistent);
            Runtimedata.lastValidPoseBoneRotation = new NativeArray<Quaternion>(PreInitalData.boneRotationChangeCheck.ToArray(), Allocator.Persistent);
            Runtimedata.currentFixedAnimatedBonePosition = new NativeArray<Vector3>(PreInitalData.currentFixedAnimatedBonePosition.ToArray(), Allocator.Persistent);
            Runtimedata.bonePositionChangeCheck = new NativeArray<Vector3>(PreInitalData.bonePositionChangeCheck.ToArray(), Allocator.Persistent);
            Runtimedata.lastValidPoseBoneLocalPosition = new NativeArray<Vector3>(PreInitalData.lastValidPoseBoneLocalPosition.ToArray(), Allocator.Persistent);
            Runtimedata.workingPosition = new NativeArray<Vector3>(PreInitalData.workingPosition.ToArray(), Allocator.Persistent);
            Runtimedata.preTeleportPosition = new NativeArray<Vector3>(PreInitalData.preTeleportPosition.ToArray(), Allocator.Persistent);
            Runtimedata.extrapolatedPosition = new NativeArray<Vector3>(PreInitalData.extrapolatedPosition.ToArray(), Allocator.Persistent);
            Runtimedata.hasTransform = new NativeArray<bool>(PreInitalData.hasTransform.ToArray(), Allocator.Persistent);
            Runtimedata.normalizedIndex = new NativeArray<float>(PreInitalData.normalizedIndex.ToArray(), Allocator.Persistent);
            Runtimedata.targetAnimatedBoneSignalCurrent = new NativeArray<Vector3>(PreInitalData.targetAnimatedBoneSignalCurrent.ToArray(), Allocator.Persistent);
            Runtimedata.targetAnimatedBoneSignalPrevious = new NativeArray<Vector3>(PreInitalData.targetAnimatedBoneSignalPrevious.ToArray(), Allocator.Persistent);
            Runtimedata.particleSignalCurrent = new NativeArray<Vector3>(PreInitalData.particleSignalCurrent.ToArray(), Allocator.Persistent);
            Runtimedata.particleSignalPrevious = new NativeArray<Vector3>(PreInitalData.particleSignalPrevious.ToArray(), Allocator.Persistent);
        }
        public Vector3 ConstrainLengthBackwards(int JiggleIndex, Vector3 newPosition, float elasticity)
        {
            if (JiggleBones[JiggleIndex].childIndex == -1)
            {
                return newPosition;
            }

            Vector3 diff = newPosition - Runtimedata.workingPosition[JiggleBones[JiggleIndex].childIndex];
            Vector3 dir = diff.normalized;
            return Vector3.Lerp(newPosition, Runtimedata.workingPosition[JiggleBones[JiggleIndex].childIndex] + dir * GetLengthToParent(JiggleIndex), elasticity);
        }
        public void Update(Vector3 wind,float fixedDeltaTime, float squaredDeltaTime, Vector3 Gravity,float Percentage)
        {
            Vector3 gravityEffect = Gravity * (jiggleSettingsdata.gravityMultiplier * squaredDeltaTime);
            float airDragDeltaTime = fixedDeltaTime * jiggleSettingsdata.airDrag;
            float inverseAirDrag = 1f - jiggleSettingsdata.airDrag;
            float inverseFriction = 1f - jiggleSettingsdata.friction;
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                // Cache values for better performance

                Vector3 AnimatedCurrentSignal = Runtimedata.targetAnimatedBoneSignalCurrent[SimulatedIndex];
                Vector3 AnimatedPreviousSignal = Runtimedata.targetAnimatedBoneSignalPrevious[SimulatedIndex];

                Vector3 currentFixedAnimatedBonePosition = SamplePosition(AnimatedCurrentSignal, AnimatedPreviousSignal, Percentage);
                Runtimedata.currentFixedAnimatedBonePosition[SimulatedIndex] = currentFixedAnimatedBonePosition;

                if (JiggleBones[SimulatedIndex].JiggleParentIndex == -1)
                {
                    Runtimedata.workingPosition[SimulatedIndex] = currentFixedAnimatedBonePosition;

                    Vector3 particleSignalCurrent = Runtimedata.particleSignalCurrent[SimulatedIndex];
                    Vector3 particleSignalPrevious = Runtimedata.particleSignalPrevious[SimulatedIndex];

                    SetPosition(ref particleSignalCurrent, ref particleSignalPrevious, currentFixedAnimatedBonePosition);

                    Runtimedata.particleSignalCurrent[SimulatedIndex] = particleSignalCurrent;
                    Runtimedata.particleSignalPrevious[SimulatedIndex] = particleSignalPrevious;
                    continue;
                }

                // Cache signals for better performance.
                Vector3 currentSignal = Runtimedata.particleSignalCurrent[SimulatedIndex];
                Vector3 previousSignal = Runtimedata.particleSignalPrevious[SimulatedIndex];

                int parentIndex = JiggleBones[SimulatedIndex].JiggleParentIndex;
                Vector3 parentCurrentSignal = Runtimedata.particleSignalCurrent[parentIndex];
                Vector3 parentPreviousSignal = Runtimedata.particleSignalPrevious[parentIndex];

                // Precompute deltas
                Vector3 deltaSignal = currentSignal - previousSignal;
                Vector3 parentDeltaSignal = parentCurrentSignal - parentPreviousSignal;

                // Calculate local space velocity
                Vector3 localSpaceVelocity = deltaSignal - parentDeltaSignal;

                // Update working position using the precomputed values
                Vector3 workingPosition = currentSignal + (deltaSignal - localSpaceVelocity) * inverseAirDrag + localSpaceVelocity * inverseFriction + gravityEffect;
                workingPosition += wind * airDragDeltaTime;
                Runtimedata.workingPosition[SimulatedIndex] = workingPosition;
            }
            if (NeedsCollisions)
            {
                for (int Index = simulatedPointsCount - 1; Index >= 0; Index--)
                {
                    Runtimedata.workingPosition[Index] = ConstrainLengthBackwards(Index, Runtimedata.workingPosition[Index], jiggleSettingsdata.lengthElasticity * jiggleSettingsdata.lengthElasticity * 0.5f);
                }
            }
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                if (JiggleBones[SimulatedIndex].JiggleParentIndex == -1)
                {
                    continue;
                }

                if (Runtimedata.hasTransform[SimulatedIndex])
                {
                    int ParentIndex = JiggleBones[SimulatedIndex].JiggleParentIndex;
                    int ParentsParentIndex = JiggleBones[ParentIndex].JiggleParentIndex;

                    Vector3 parentParentPosition;
                    Vector3 poseParentParent;
                    if (ParentsParentIndex == -1)
                    {
                        poseParentParent = Runtimedata.currentFixedAnimatedBonePosition[ParentIndex] + (Runtimedata.currentFixedAnimatedBonePosition[ParentIndex] - Runtimedata.currentFixedAnimatedBonePosition[SimulatedIndex]);
                        parentParentPosition = poseParentParent;
                    }
                    else
                    {
                        parentParentPosition = Runtimedata.workingPosition[ParentsParentIndex];
                        poseParentParent = Runtimedata.currentFixedAnimatedBonePosition[ParentsParentIndex];
                    }

                    Vector3 parentAimTargetPose = Runtimedata.currentFixedAnimatedBonePosition[ParentIndex] - poseParentParent;
                    Vector3 parentAim = Runtimedata.workingPosition[ParentIndex] - parentParentPosition;
                    Quaternion TargetPoseToPose = Quaternion.FromToRotation(parentAimTargetPose, parentAim);
                    Vector3 currentPose = Runtimedata.currentFixedAnimatedBonePosition[SimulatedIndex] - poseParentParent;
                    Vector3 constraintTarget = TargetPoseToPose * currentPose;

                    float error = Vector3.Distance(Runtimedata.workingPosition[SimulatedIndex], parentParentPosition + constraintTarget);
                    error /= GetLengthToParent(SimulatedIndex);
                    error = Mathf.Clamp01(error);
                    error = Mathf.Pow(error, jiggleSettingsdata.elasticitySoften * 2f);

                    Runtimedata.workingPosition[SimulatedIndex] = Vector3.Lerp(Runtimedata.workingPosition[SimulatedIndex],
                        parentParentPosition + constraintTarget,
                        jiggleSettingsdata.angleElasticity * jiggleSettingsdata.angleElasticity * error);

                    // Constrain Length
                    int Index = JiggleBones[SimulatedIndex].JiggleParentIndex;
                    Vector3 diffBetweenPoints = Runtimedata.workingPosition[SimulatedIndex] - Runtimedata.workingPosition[Index];
                    diffBetweenPoints = diffBetweenPoints.normalized;
                    Runtimedata.workingPosition[SimulatedIndex] = Vector3.Lerp(Runtimedata.workingPosition[SimulatedIndex],
                        Runtimedata.workingPosition[Index] + diffBetweenPoints * GetLengthToParent(SimulatedIndex),
                        jiggleSettingsdata.lengthElasticity * jiggleSettingsdata.lengthElasticity);
                }
            }
            if (NeedsCollisions)
            {
                for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
                {
                    if (!CachedSphereCollider.TryGet(out SphereCollider sphereCollider))
                    {
                        continue;
                    }
                    for (int ColliderIndex = 0; ColliderIndex < collidersCount; ColliderIndex++)
                    {
                        sphereCollider.radius = jiggleSettings.GetRadius(Runtimedata.normalizedIndex[SimulatedIndex]);
                        if (sphereCollider.radius <= 0)
                        {
                            continue;
                        }
                        Collider collider = colliders[ColliderIndex];
                        collider.transform.GetPositionAndRotation(out Vector3 position, out Quaternion rotation);
                        if (Physics.ComputePenetration(sphereCollider, Runtimedata.workingPosition[SimulatedIndex], Quaternion.identity, collider, position, rotation, out Vector3 dir, out float dist))
                        {
                            Runtimedata.workingPosition[SimulatedIndex] += dir * dist;
                        }
                    }
                }
            }
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                Vector3 CurrentSignal = Runtimedata.particleSignalCurrent[SimulatedIndex];
                Vector3 PreviousSignal = Runtimedata.particleSignalPrevious[SimulatedIndex];

                SetPosition(ref CurrentSignal, ref PreviousSignal, Runtimedata.workingPosition[SimulatedIndex]);

                Runtimedata.particleSignalCurrent[SimulatedIndex] = CurrentSignal;
                Runtimedata.particleSignalPrevious[SimulatedIndex] = PreviousSignal;
            }
        }
        public void PrepareBone(Vector3 position, JiggleRigLOD jiggleRigLOD)
        {
            for (int PointIndex = 0; PointIndex < simulatedPointsCount; PointIndex++)
            {
                Vector3 CurrentSignal = Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex];
                Vector3 PreviousSignal = Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex];
                // If bone is not animated, return to last unadulterated pose
                if (Runtimedata.hasTransform[PointIndex])
                {
                    ComputedTransforms[PointIndex].GetLocalPositionAndRotation(out Vector3 localPosition, out Quaternion localrotation);
                    if (Runtimedata.boneRotationChangeCheck[PointIndex] == localrotation)
                    {
                        ComputedTransforms[PointIndex].localRotation = Runtimedata.lastValidPoseBoneRotation[PointIndex];
                    }
                    if (Runtimedata.bonePositionChangeCheck[PointIndex] == localPosition)
                    {
                        ComputedTransforms[PointIndex].localPosition = Runtimedata.lastValidPoseBoneLocalPosition[PointIndex];
                    }
                }
                else
                {


                    int ParentIndex = JiggleBones[PointIndex].JiggleParentIndex;
                    SetPosition(ref CurrentSignal, ref PreviousSignal, GetProjectedPosition(PointIndex, ParentIndex));

                    Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex] = CurrentSignal;
                    Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex] = PreviousSignal;
                    continue;
                }

                SetPosition(ref CurrentSignal, ref PreviousSignal, ComputedTransforms[PointIndex].position);
                Runtimedata.targetAnimatedBoneSignalCurrent[PointIndex] = CurrentSignal;
                Runtimedata.targetAnimatedBoneSignalPrevious[PointIndex] = PreviousSignal;

                ComputedTransforms[PointIndex].GetLocalPositionAndRotation(out Vector3 pos, out Quaternion Rot);
                Runtimedata.lastValidPoseBoneRotation[PointIndex] = Rot;
                Runtimedata.lastValidPoseBoneLocalPosition[PointIndex] = pos;
            }
            jiggleSettingsdata = jiggleRigLOD.AdjustJiggleSettingsData(position, jiggleSettingsdata);
        }
        public void Pose(float Percentage)
        {
            Vector3 CurrentSignal = Runtimedata.particleSignalCurrent[0];
            Vector3 PreviousSignal = Runtimedata.particleSignalPrevious[0];

            Runtimedata.extrapolatedPosition[0] = SamplePosition(CurrentSignal, PreviousSignal, Percentage);

            Vector3 virtualPosition = Runtimedata.extrapolatedPosition[0];

            Vector3 offset = ComputedTransforms[0].transform.position - virtualPosition;
            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                CurrentSignal = Runtimedata.particleSignalCurrent[SimulatedIndex];
                PreviousSignal = Runtimedata.particleSignalPrevious[SimulatedIndex];
                Runtimedata.extrapolatedPosition[SimulatedIndex] = offset + SamplePosition(CurrentSignal, PreviousSignal, Percentage);
            }

            for (int SimulatedIndex = 0; SimulatedIndex < simulatedPointsCount; SimulatedIndex++)
            {
                if (JiggleBones[SimulatedIndex].childIndex == -1)
                {
                    continue; // Early exit if there's no child
                }
                int ChildIndex = JiggleBones[SimulatedIndex].childIndex;
                // Cache frequently accessed values
                Vector3 CurrentAnimated = Runtimedata.targetAnimatedBoneSignalCurrent[SimulatedIndex];
                Vector3 PreviousAnimated = Runtimedata.targetAnimatedBoneSignalPrevious[SimulatedIndex];

                Vector3 ChildCurrentAnimated = Runtimedata.targetAnimatedBoneSignalCurrent[ChildIndex];
                Vector3 ChildPreviousAnimated = Runtimedata.targetAnimatedBoneSignalPrevious[ChildIndex];

                Vector3 targetPosition = SamplePosition(CurrentAnimated, PreviousAnimated, Percentage);
                Vector3 childTargetPosition = SamplePosition(ChildCurrentAnimated, ChildPreviousAnimated, Percentage);
                // Blend positions
                Vector3 positionBlend = Vector3.Lerp(targetPosition, Runtimedata.extrapolatedPosition[SimulatedIndex], jiggleSettingsdata.blend);

                Vector3 childPositionBlend = Vector3.Lerp(childTargetPosition, Runtimedata.extrapolatedPosition[ChildIndex], jiggleSettingsdata.blend);

                if (JiggleBones[SimulatedIndex].JiggleParentIndex != -1)
                {
                    ComputedTransforms[SimulatedIndex].position = positionBlend;
                }

                // Calculate child position and vector differences
                int childIndex = JiggleBones[SimulatedIndex].childIndex;
                Vector3 childPosition = GetTransformPosition(childIndex);
                Vector3 cachedAnimatedVector = childPosition - positionBlend;
                Vector3 simulatedVector = childPositionBlend - positionBlend;

                // Rotate the transform based on the vector differences
                if (cachedAnimatedVector != Zero && simulatedVector != Zero)
                {
                    Quaternion animPoseToPhysicsPose = Quaternion.FromToRotation(cachedAnimatedVector, simulatedVector);
                    ComputedTransforms[SimulatedIndex].rotation = animPoseToPhysicsPose * ComputedTransforms[SimulatedIndex].rotation;
                }

                // Cache transform changes if the bone has a transform
                if (Runtimedata.hasTransform[SimulatedIndex])
                {
                    ComputedTransforms[SimulatedIndex].GetLocalPositionAndRotation(out Vector3 pos, out Quaternion Rot);
                    Runtimedata.boneRotationChangeCheck[SimulatedIndex] = Rot;
                    Runtimedata.bonePositionChangeCheck[SimulatedIndex] = pos;
                }
            }
        }
        public void SetPosition(ref Vector3 Current, ref Vector3 Previous, Vector3 position)
        {
            Previous = Current;
            Current = position;
        }
        public Vector3 SamplePosition(Vector3 Current, Vector3 Previous, float Percentage)
        {
            if (Percentage == 0)
            {
                return Previous;
            }
            return Vector3.Lerp(Previous, Current, Percentage);
        }
    }
}