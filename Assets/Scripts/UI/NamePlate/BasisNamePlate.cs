using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Drivers;
using Basis.Scripts.TransformBinders.BoneControl;
using Basis.Scripts.UI.UI_Panels;
using TMPro;
using UnityEngine;

namespace Basis.Scripts.UI.NamePlate
{
public abstract class BasisNamePlate : MonoBehaviour
{
    public BasisUIComponent BasisUIComponent;
    public Transform LocalCameraDriver;
    public Vector3 directionToCamera;
    public BasisBoneControl HipTarget;
    public BasisBoneControl MouthTarget;
    public TextMeshProUGUI Text;
    public void Initalize(BasisBoneControl hipTarget, BasisRemotePlayer BasisRemotePlayer)
    {
        HipTarget = hipTarget;
        MouthTarget = BasisRemotePlayer.MouthControl;
        LocalCameraDriver = BasisLocalCameraDriver.Instance.transform;
        Text.text = BasisRemotePlayer.DisplayName;
    }
    private void Update()
    {
        // Get the direction to the camera
        directionToCamera = LocalCameraDriver.position - transform.position;
        transform.SetPositionAndRotation(
            GeneratePoint(),
            Quaternion.Euler(transform.rotation.eulerAngles.x, Mathf.Atan2(directionToCamera.x, directionToCamera.z)
            * Mathf.Rad2Deg, transform.rotation.eulerAngles.z));
    }
    public Vector3 GeneratePoint()
    {
        return HipTarget.OutgoingWorldData.position + new Vector3(0, MouthTarget.TposeLocal.position.y / 1.25f, 0);
    }
}
}