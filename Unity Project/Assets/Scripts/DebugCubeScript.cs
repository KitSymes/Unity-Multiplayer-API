using KitSymes.GTRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCubeScript : NetworkBehaviour
{
    public Vector3 dir;
    public float offset;
    private float pause1 = 6.0f;
    private float pause2 = 12.0f;

    void Update()
    {
        if (!networkObject.IsServer())
            return;

        if (offset > 0.0f)
        {
            offset -= Time.deltaTime;
            return;
        }

        transform.position += dir * Time.deltaTime;

        if (transform.position.y < -5 || transform.position.y > 5)
            dir *= -1;

        if (pause1 > 0.0f)
        {
            pause1 -= Time.deltaTime;
        } else
        {
            transform.Rotate(new Vector3(90.0f, 90.0f, 0.0f) * Time.deltaTime);
        }

        /*if (pause2 > 0.0f)
        {
            pause2 -= Time.deltaTime;
        } else
        {
            transform.localScale += dir * Time.deltaTime;
        }*/
    }
}
