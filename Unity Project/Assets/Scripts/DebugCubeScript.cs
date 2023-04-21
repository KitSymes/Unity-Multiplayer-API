using KitSymes.GTRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCubeScript : NetworkBehaviour
{
    public Vector3 dir;
    public float offset;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
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
    }
}
