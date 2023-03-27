using KitSymes.GTRP;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Ball : NetworkBehaviour
{
    Vector3 direction;
    public NetworkObject lastTouched;

    void Start()
    {

    }

    void FixedUpdate()
    {
        if (!networkObject.IsServer())
            return;
        transform.position += direction * Time.fixedDeltaTime;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!networkObject.IsServer())
            return;
        if (collision.gameObject.CompareTag("Player"))
        {
            Vector3 dif = transform.position - collision.transform.position;
            direction = dif.normalized;
            lastTouched = collision.gameObject.GetComponent<NetworkObject>();
        }
    }

    public void SetDirection(Vector3 vector3)
    {
        direction = vector3.normalized;
    }
}
