
using UnityEngine;

public class MovingBar : MonoBehaviour
{
    [SerializeField] private float minY = -3f;
    [SerializeField] private float maxY = 3f;
    [SerializeField] private float speed = 3f;

    private int direction = 1;

    private void Update()
    {
        Vector3 position = transform.position;

        position.y += direction * speed * Time.deltaTime;

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
}
