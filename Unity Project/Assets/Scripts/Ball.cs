using KitSymes.GTRP;
using UnityEngine;

public partial class Ball : NetworkBehaviour
{
    Vector3 direction;
    public NetworkObject lastTouched;

    public float startSpeed = 1.0f;
    [SyncVar]
    public float ballSpeed = 1.0f;

    void Start()
    {
        ballSpeed = startSpeed;
    }

    void FixedUpdate()
    {
        if (!networkObject.IsServer())
            return;

        transform.position += direction * Time.fixedDeltaTime * ballSpeed;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!networkObject.IsServer())
            return;
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 dif = transform.position - collision.transform.position;
            dif.y /= 5.0f;
            direction = dif.normalized;
            lastTouched = collision.gameObject.GetComponent<NetworkObject>();
            ballSpeed += 0.2f;
        }
    }

    public void SetDirection(Vector3 vector3)
    {
        direction = vector3.normalized;
    }
}
