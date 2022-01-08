using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MordiAudio;

public class EnemyController : MonoBehaviour
{
    public Transform healthCube;
    public Transform player;
    public GameObject deathPrefab;

    [Header("Audio events")]
    public AudioEvent takeDamageEvent;
    public AudioEvent gruntEvent;

    float healthCubeInitialScaleX = 0f;
    int health = 100;
    bool isAttacking = false;
    float voiceTimer = 0.5f;

    private void Awake() {
        healthCubeInitialScaleX = healthCube.transform.localScale.x;
    }

    void Update()
    {
        if (isAttacking) {
            if (Vector3.Distance(transform.position, player.position) > 1f) {
                Vector3 direction = new Vector3(player.position.x, 0f, player.position.z) - new Vector3(transform.position.x, 0f, transform.position.z);
                transform.position += direction.normalized * Time.deltaTime * 2f;
                transform.rotation = Quaternion.LookRotation(direction);

                if (voiceTimer > 0f) {
                    voiceTimer -= Time.deltaTime;
                } else {
                    ResetVoiceTimer();
                    gruntEvent.Play(transform.position);
                }
            }
        } else {
            if (Vector3.Distance(transform.position, player.position) < 15f) {
                isAttacking = true;
            }
        }
        
    }

    public void TakeDamage() {
        health -= 10;
        if (health <= 0) {
            Instantiate(deathPrefab, transform.position + new Vector3(0f, 1.5f, 0f), Quaternion.Euler(-90f, 0f, 0f));
            Destroy(gameObject);
        } else {
            takeDamageEvent.Play(transform.position);
            healthCube.localScale = new Vector3((health / 100f) * healthCubeInitialScaleX, healthCube.localScale.y, healthCube.localScale.z);
        }
    }

    public bool GetIsAttacking() {
        return isAttacking;
    }

    void ResetVoiceTimer() {
        voiceTimer = Random.Range(5f, 15f);
    }
}
