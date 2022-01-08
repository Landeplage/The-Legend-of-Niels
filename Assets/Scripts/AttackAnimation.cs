using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MordiAudio;

public class AttackAnimation : MonoBehaviour
{
    public AudioEvent attackEvent;

    float t = 0.0f;
    bool doAnimation = false, hasDoneAnimation = false;
    Vector3 initialPos;
    float rotRandomX, rotRandomZ;

    private void Start() {
        initialPos = transform.localPosition;
    }

    void Update()
    {
        if (!hasDoneAnimation && doAnimation) {
            if (t <= 1.0f) {
                t += Time.deltaTime * 8f;
                if (t >= 1.0f) {
                    t = 1.0f;
                    hasDoneAnimation = true;

                    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
                    foreach(GameObject e in enemies) {
                        if (Vector3.Distance(transform.position, e.transform.position) < 2f) {
                            EnemyController eCon = e.GetComponent<EnemyController>();
                            if (eCon != null) {
                                eCon.TakeDamage();
                            }
                        }
                    }
                }
            }
        } else {
            if (t > 0f) {
                t -= Time.deltaTime * 8f;
                if (t < 0f) {
                    t = 0f;
                }
            } else {
                if (hasDoneAnimation) {
                    doAnimation = false;
                    hasDoneAnimation = false;
                }
            }
        }

        transform.localPosition = initialPos + Vector3.forward * EasingFunction.EaseInQuad(0f, 1.5f, t);
        transform.localRotation = Quaternion.Euler(new Vector3(-EasingFunction.EaseInQuad(0f, rotRandomX, t), 90f, EasingFunction.EaseInQuad(0f, rotRandomZ, t)));
    }

    public void StartAnimation() {
        if (!doAnimation) {
            attackEvent.Play(transform.position);
            doAnimation = true;
            rotRandomX = Random.Range(-100f, 100f);
            rotRandomZ = Random.Range(-100f, 100f);
        }
    }
}
