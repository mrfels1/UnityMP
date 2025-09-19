using UnityEngine;

public class PlayerController2D : MonoBehaviour
{
    public float speed = 6f;
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public Rigidbody2D rb;

    Vector2 input;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        input = new Vector2(
            Input.GetAxisRaw(horizontalAxis),
            Input.GetAxisRaw(verticalAxis)
        ).normalized;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + input * speed * Time.fixedDeltaTime);
    }
}
