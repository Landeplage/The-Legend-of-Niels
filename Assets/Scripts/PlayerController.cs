using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MordiAudio;

public class PlayerController : MonoBehaviour
{
    float maxSpeed = 10f, acceleration = 1.2f, deceleration = 10f;
    Vector3 mousePrev = Vector3.zero, movement = Vector3.zero;

    public AttackAnimation rightHand;
    public Transform rightLeg, leftLeg;
    Vector3 rightLegInitialPos, leftLegInitialPos;

    float walkAnim = 0.0f;
    float walkBlend = 0.0f;
    Quaternion targetRot;
    Vector3 prevPos = Vector3.zero;
    bool leftAudio = true, rightAudio = true;

    [Header("Audio events")]
    public AudioEvent footstepEvent;

    private void Awake() {
        rightLegInitialPos = rightLeg.localPosition;
        leftLegInitialPos = leftLeg.localPosition;
        targetRot = transform.rotation;
    }

    void Update()
    {
        // Turning
        Transform nearestEnemy = null;
        float distanceToNearest = Mathf.Infinity;
        foreach(GameObject enemy in GameObject.FindGameObjectsWithTag("Enemy")) {
            EnemyController e = enemy.GetComponent<EnemyController>();
            if (e.GetIsAttacking()) {
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < distanceToNearest) {
                    distanceToNearest = d;
                    nearestEnemy = e.transform;
                }
            }
        }

        if (nearestEnemy != null) {
            Vector3 dir = nearestEnemy.position - transform.position;
            targetRot = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        } else {
            Vector3 dir = transform.rotation.eulerAngles + new Vector3(0f, (Input.mousePosition.x - mousePrev.x), 0f);
            targetRot = Quaternion.Euler(dir);
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 0.2f);

        mousePrev = Input.mousePosition;

        // Movement
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) {
            move += Vector3.forward;
        }
        if (Input.GetKey(KeyCode.S)) {
            move += Vector3.back;
        }
        if (Input.GetKey(KeyCode.A)) {
            move += Vector3.left;
        }
        if (Input.GetKey(KeyCode.D)) {
            move += Vector3.right;
        }

        movement += move * acceleration;
        if (movement.magnitude > maxSpeed) {
            movement = new Vector3(Mathf.Clamp(movement.x, -maxSpeed, maxSpeed), 0f, Mathf.Clamp(movement.z, -maxSpeed, maxSpeed));
        }
        transform.Translate(movement * Time.deltaTime);

        movement = movement / (1.0f + (deceleration * Time.deltaTime));

        // Attack
        if (Input.GetMouseButtonDown(0)) {
            rightHand.StartAnimation();
        }

        // Walk animation
        walkBlend = (prevPos - transform.position).magnitude * 2f;
        walkAnim += Time.deltaTime * 3f;
        if (walkAnim > 1f) {
            walkAnim = 0f;
            leftAudio = true;
            rightAudio = true;
        }
        float rad = Mathf.Sin(walkAnim * Mathf.PI * 2f);
        rightLeg.localPosition = rightLegInitialPos + Vector3.forward * rad * walkBlend * 3f;
        leftLeg.localPosition = leftLegInitialPos + Vector3.forward * -rad * walkBlend * 3f;
        if (move != Vector3.zero) {
            if (leftAudio && rad > 0.5f) {
                footstepEvent.Play(transform.position);
                leftAudio = false;
            }
            if (rightAudio && rad < -0.5f) {
                footstepEvent.Play(transform.position);
                rightAudio = false;
            }
        }

        prevPos = transform.position;
    }
}
