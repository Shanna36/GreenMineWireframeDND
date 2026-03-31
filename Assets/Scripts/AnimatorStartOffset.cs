using UnityEngine;

public class AnimatorStartOffset : MonoBehaviour
{
    [Range(0f, 1f)] public float normalizedStartTime = -1f; // -1 = random

    void Start()
    {
        var anim = GetComponent<Animator>();
        if (!anim) return;

        float t = normalizedStartTime < 0f ? Random.value : normalizedStartTime;

        anim.Play(0, 0, t);
        anim.Update(0f); 
    }
}