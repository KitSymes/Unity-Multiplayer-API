using KitSymes.GTRP;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugCubeScript : NetworkBehaviour
{
    bool _directionMove = true;
    bool _directionScale = true;
    float _timeBetweenPhases = 6.0f;

    int _phase = 0;
    float _timer;

    public float delay;

    void Start()
    {
        _timer = delay;
    }

    void Update()
    {
        if (!networkObject.IsServer())
            return;

        if (_timer <= 0.0f)
        {
            switch (_phase)
            {
                case 0:
                case 1:
                case 2:
                    _timer = _timeBetweenPhases;
                    _phase++;
                    break;
                default:
                    break;
            }
        }
        else
            _timer -= Time.deltaTime;

        if (_phase > 0)
        {
            transform.position += new Vector3(0.0f, (_directionMove ? 1.0f : -1.0f), 0.0f) * Time.deltaTime;

            if (transform.position.y < -5 || transform.position.y > 5)
                _directionMove = !_directionMove;
        }

        if (_phase > 1)
        {
            transform.Rotate(new Vector3(90.0f, 90.0f, 0.0f) * Time.deltaTime);
        }

        if (_phase > 2)
        {
            int dir = (_directionScale ? 1 : -1);
            transform.localScale += new Vector3(dir, dir, dir) * Time.deltaTime;

            if (transform.localScale.y <= 0.0f  || transform.localScale.y > 5.0f)
                _directionScale = !_directionScale;
        }
    }
}
