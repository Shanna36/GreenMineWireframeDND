using System.Collections;
using UnityEngine;

public class TruckMover : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float forwardDistance = 12f;
    [SerializeField] private float moveDuration = 6f;

    [Header("Timing")]
    [SerializeField] private float waitBeforeReturn = 30f;

    private Vector3 startPosition;
    private Vector3 forwardPosition;

    private void Awake()
    {
        startPosition = transform.position;
        forwardPosition = startPosition + new Vector3(0f, 0f, forwardDistance);

        StartCoroutine(MoveRoutine());
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            yield return MoveToPosition(forwardPosition, moveDuration);
            yield return new WaitForSeconds(waitBeforeReturn);
            yield return MoveToPosition(startPosition, moveDuration);
            yield return new WaitForSeconds(waitBeforeReturn);
        }
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition, float duration)
    {
        Vector3 initialPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            transform.position = Vector3.Lerp(initialPosition, targetPosition, t);

            yield return null;
        }

        transform.position = targetPosition;
    }
}