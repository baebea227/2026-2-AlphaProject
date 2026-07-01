using UnityEngine;
using Fusion;

public class PlayerInteractionHandler : NetworkBehaviour
{
    public float checkRange;

    Collider[] nearObjects;

    void Awake()
    {
        nearObjects = new Collider[10];
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasInputAuthority)
        {
            return;
        }

        CheckNearObjects();
    }

    // 주변 아이템 감지
    void CheckNearObjects()
    {
        // 추후 레이어마스크를 통한 식별 필요?
        int nearCnt = Physics.OverlapSphereNonAlloc(transform.position, checkRange, nearObjects);
        NetworkObject nearestObject = GetNearestObject(nearCnt);
        for (int i=nearCnt-1; i>=0; i--)
        {
            // 이름으로 식별 중
            if (!nearObjects[i].gameObject.name.Contains("Item"))
            {
                nearCnt--;
            }
        }

        if(GetInput(out NetworkInputData input) && input.isInteract)
        {
            if(!Runner.IsForward)
            {
                return;
            }

            Debug.Log($"주변에 {nearCnt}개의 아이템 감지");
            if (nearestObject != null)
            {
                Debug.Log($"가장 가까운 아이템: {nearestObject.name}");
            }
        }
    }

    // 가장 가까운 아이템 검출
    NetworkObject GetNearestObject(int objectCnt)
    {
        float minDist = Mathf.Infinity;
        NetworkObject nearestObject = null;
        int nearestIdx = 0;
        bool detected = false;

        for(int i = 0; i<objectCnt; i++)
        {
            if (!nearObjects[i].gameObject.name.Contains("Item"))
            {
                continue;
            }

            float newDist = Vector3.Distance(transform.position, nearObjects[i].transform.position);
            if (newDist < minDist)
            {
                minDist = newDist;
                nearestIdx = i;
                detected = true;
            }
        }

        if (detected)
        {
            nearestObject = nearObjects[nearestIdx].GetComponent<NetworkObject>();
        }
        return nearestObject;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, checkRange);
    }
}
