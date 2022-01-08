using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public Transform camTarget;
    public Transform player;
    Vector3 targetPos;

    void Update()
    {
        Vector3 targetRot = player.position + new Vector3(player.forward.x, 1f, player.forward.z);

        int count = 1;
        foreach(GameObject e in GameObject.FindGameObjectsWithTag("Enemy")) {
            if (e.GetComponent<EnemyController>().GetIsAttacking()) {
                count += 1;
                targetRot += e.transform.position;
            }
        }
        targetRot = targetRot / count;

        targetPos = camTarget.position;

        if (count > 1) {
            targetPos += new Vector3(0f, 0.2f, 0f) + player.right * 2f;
        }

        transform.position += (targetPos - transform.position) * Time.deltaTime * 5f;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetRot - transform.position), Time.deltaTime * 8f);
    }
}
