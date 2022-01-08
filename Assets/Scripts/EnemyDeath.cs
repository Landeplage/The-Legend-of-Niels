using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MordiAudio;

public class EnemyDeath : MonoBehaviour
{
    public AudioEvent deathEvent;
    float timer = 2.0f;

    // Start is called before the first frame update
    void Start()
    {
        deathEvent.Play(transform.position);
    }

    private void Update() {
        timer -= Time.deltaTime;
        if (timer < 0f) {
            Destroy(gameObject);
        }
    }
}
