using UnityEngine;

public class FollowCamera2D : MonoBehaviour
{
    public Transform target;
    public float smooth = 10f;

    void LateUpdate()
    {
        if (!target) return;
        Vector3 p = transform.position;
        p.x = Mathf.Lerp(p.x, target.position.x, Time.deltaTime * smooth);
        p.y = Mathf.Lerp(p.y, target.position.y, Time.deltaTime * smooth);
        transform.position = new Vector3(p.x, p.y, -10f);
    }
}
