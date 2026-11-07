
using UnityEngine;

public class MovingBar : MonoBehaviour
{
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float speed = 3f;
    [SerializeField, Range(0.01f, 1f)] private float stairSpeedMultiplier = 0.35f;

    private int direction = 1;
    private bool isMoving = true;
    private bool isStairMode;
    private float difficultySpeedMultiplier = 1f;
    private Vector3 initialPosition;

    public float NormalizedPosition =>
        Mathf.InverseLerp(minY, maxY, transform.position.y);

    private void Awake()
    {
        initialPosition = transform.position;
        // Cover scenes and dynamically created bars without an Inspector setup.
        if (GetComponent<SpriteRenderer>() != null && GetComponent<MovingBarOutline>() == null)
            gameObject.AddComponent<MovingBarOutline>();
    }

    private void Update()
    {
        if (!isMoving) return;

        Vector3 position = transform.position;
        position.y += direction * speed * difficultySpeedMultiplier *
            (isStairMode ? stairSpeedMultiplier : 1f) * Time.deltaTime;

        if (position.y >= maxY)
        {
            position.y = maxY;
            direction = -1;
        }
        else if (position.y <= minY)
        {
            position.y = minY;
            direction = 1;
        }

        transform.position = position;
    }

    public void ResetForCountdown()
    {
        // Keep the bar on its current horizontal lane, including after a script reload.
        var position = transform.position;
        position.y = Mathf.Clamp(initialPosition.y, minY, maxY);
        transform.position = position;
        direction = 1;
        isMoving = false;
        isStairMode = false;
    }

    public void SetDifficultySpeedMultiplier(float multiplier) =>
        difficultySpeedMultiplier = Mathf.Max(0.01f, multiplier);

    public void StopMovement()
    {
        isMoving = false;
    }

    public void ResumeMovement()
    {
        isMoving = true;
    }

    public void StartStairMovement()
    {
        isStairMode = true;
        isMoving = true;
    }

    public void StopStairMovement()
    {
        isStairMode = false;
    }
}
