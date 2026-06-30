using Fusion;
using UnityEngine;

public class PlayerCamera : NetworkBehaviour
{
    public Vector3 transformOffset;
    public float smoothTime = 0.3f;

    Camera cam;
    Transform targetPos;
    Transform originalParent;
    Vector3 followVelocity;

    void Awake()
    {
        cam = GetComponent<Camera>();
        targetPos = GetComponentInParent<NetworkObject>()?.transform;
        originalParent = transform.parent;
    }

    public override void Spawned()
    {
        transform.SetParent(null, true);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        transform.SetParent(originalParent, true);
    }

    public override void Render()
    {
        if(cam == null || targetPos == null || !cam.enabled)
        {
            return;
        }

        // 카메라 이동
        Vector3 followPos = targetPos.position +
            Vector3.up * transformOffset.y +
            Vector3.right * transformOffset.x +
            Vector3.forward * transformOffset.z;
        transform.position = Vector3.SmoothDamp(transform.position, followPos, ref followVelocity, smoothTime);

        //카메라 회전
        Vector3 lookRotation = targetPos.position - followPos;
        transform.rotation = Quaternion.LookRotation(lookRotation);
    }
}
