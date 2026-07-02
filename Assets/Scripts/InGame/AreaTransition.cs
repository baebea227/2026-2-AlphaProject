using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class AreaTransition : MonoBehaviour
{
    private static readonly Dictionary<int, float> NextTeleportTimesByPlayer = new Dictionary<int, float>();
    private static readonly Color DestinationGizmoColor = new Color(0.2f, 0.85f, 1f, 1f);
    private static readonly Color ArrivalGizmoColor = new Color(0.2f, 1f, 0.45f, 1f);

    [Tooltip("플레이어가 이동할 목적지 Transform입니다.")]
    [SerializeField] private Transform destination;
    [Tooltip("도착 발판의 앞쪽 방향으로 플레이어를 내려놓을 거리입니다.")]
    [Min(0f)]
    [SerializeField] private float arrivalDistance = 1.5f;
    [Tooltip("도착한 플레이어 루트 Transform의 높이입니다.")]
    [SerializeField] private float arrivalHeight;
    [Tooltip("순간 이동 직후 다시 전환되지 않도록 막는 시간입니다.")]
    [Min(0f)]
    [SerializeField] private float teleportCooldown = 0.5f;
    [Tooltip("목적지 Transform의 회전을 플레이어에게 적용할지 여부입니다.")]
    [SerializeField] private bool matchDestinationRotation;

    private void OnValidate()
    {
        arrivalDistance = Mathf.Max(0f, arrivalDistance);
        teleportCooldown = Mathf.Max(0f, teleportCooldown);
    }

    private void OnDrawGizmosSelected()
    {
        if (destination == null)
        {
            return;
        }

        Vector3 localArrivalPosition = GetArrivalPosition(transform);

        Gizmos.color = DestinationGizmoColor;
        Gizmos.DrawLine(transform.position, destination.position);
        Gizmos.DrawWireSphere(destination.position, 0.2f);

        Gizmos.color = ArrivalGizmoColor;
        Gizmos.DrawLine(transform.position, localArrivalPosition);
        Gizmos.DrawWireSphere(localArrivalPosition, 0.35f);
        Gizmos.DrawRay(localArrivalPosition, transform.forward * 0.75f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot))
        {
            return;
        }

        NetworkObject networkObject = playerRoot.GetComponent<NetworkObject>();
        NetworkCharacterController networkController = playerRoot.GetComponent<NetworkCharacterController>();

        if (networkObject == null || networkController == null)
        {
            return;
        }

        if (!networkObject.HasStateAuthority)
        {
            return;
        }

        if (destination == null)
        {
            Debug.LogWarning($"{nameof(AreaTransition)}: 목적지가 연결되지 않았습니다.", this);
            return;
        }

        int playerKey = networkObject.GetInstanceID();
        float currentTime = Time.time;

        if (NextTeleportTimesByPlayer.TryGetValue(playerKey, out float nextTeleportTime) &&
            currentTime < nextTeleportTime)
        {
            return;
        }

        Vector3 targetPosition = GetArrivalPosition(destination);
        Quaternion targetRotation = matchDestinationRotation ? destination.rotation : playerRoot.rotation;

        NextTeleportTimesByPlayer[playerKey] = currentTime + teleportCooldown;
        networkController.Teleport(targetPosition, targetRotation);
    }

    private Vector3 GetArrivalPosition(Transform arrivalBase)
    {
        if (arrivalBase == null)
        {
            return transform.position;
        }

        Vector3 targetPosition = arrivalBase.position + arrivalBase.forward * arrivalDistance;
        targetPosition.y = arrivalHeight;
        return targetPosition;
    }

    private static bool TryGetPlayerRoot(Collider hitCollider, out Transform playerRoot)
    {
        playerRoot = null;

        if (hitCollider == null)
        {
            return false;
        }

        Transform hitTransform = hitCollider.transform;
        if (hitTransform.CompareTag("Player"))
        {
            playerRoot = hitTransform;
            return true;
        }

        PlayerMovement playerMovement = hitTransform.GetComponentInParent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerRoot = playerMovement.transform;
            return true;
        }

        Transform rootTransform = hitTransform.root;
        if (rootTransform != null && rootTransform.CompareTag("Player"))
        {
            playerRoot = rootTransform;
            return true;
        }

        return false;
    }
}
