using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MordiAudio;

public class PlayAudioEvent : MonoBehaviour
{
    public enum EventType
    {
        None,
        Awake,
        Enable,
        Disable,
        Destroy
    }
    public EventType playOn = EventType.Awake;
    public EventType stopOn = EventType.None;
    public AudioEvent audioEvent;

    private void Awake() {
        if (playOn == EventType.Awake)
            Play();
        if (stopOn == EventType.Awake)
            Stop();
    }

    private void OnEnable() {
        if (playOn == EventType.Enable)
            Play();
        if (stopOn == EventType.Enable)
            Stop();
    }

    private void OnDisable() {
        if (playOn == EventType.Disable)
            Play();
        if (stopOn == EventType.Disable)
            Stop();
    }

    private void OnDestroy() {
        if (playOn == EventType.Destroy)
            Play();
        if (stopOn == EventType.Destroy)
            Stop();
    }

    void Play() {
        audioEvent.Play(transform.position);
    }

    void Stop() {
        audioEvent.Stop();
    }
}
