using Mirror;
using UnityEngine;

public partial class MirrorPlayerScript : NetworkBehaviour
{
    /*[SyncVar]
    public int dummy = 0;

    [SyncVarAttribute]
    public int test = 0;

    [SyncVar]
    public byte testByte = 0;
    [SyncVar]
    public bool testBool = false;
    [SyncVar]
    public char testChar = 'b';
    [SyncVar]
    public int testInt = 3;
    [SyncVar]
    public float testFloat = 4.2f;
    [SyncVar]
    public double testDouble = 6.1d;
    [SyncVar]
    public string testString = "no";*/
    [SyncVar]
    public float paddleSpeed = 1.0f;

    Renderer _renderer;

    [Command]
    void SetColor(float r, float g, float b)
    {
        _renderer.material.SetColor("_Color", new Color(r, g, b));
    }

    [ClientRpc]
    void SetColorab(float r, float g, float b)
    {
        _renderer.material.SetColor("_Color", new Color(r, g, b));
    }

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
    }

    public override void OnStartClient()
    {
        if (isOwned)
            _renderer.material.SetColor("_Color", new Color(0f, 1f, 0f));
    }

    [SyncVar]
    float r = 0.5f;
    [SyncVar]
    float g = 0.5f;
    [SyncVar]
    float b = 0.5f;

    // Update is called once per frame
    void Update()
    {
        if (!isOwned || !authority)
            return;

        if (Input.GetKey(KeyCode.W))
            transform.position += Vector3.up * Time.deltaTime * paddleSpeed;
        if (Input.GetKey(KeyCode.S))
            transform.position -= Vector3.up * Time.deltaTime * paddleSpeed;

        if (Input.GetKey(KeyCode.Alpha1))
        {
            r = Mathf.Max(r - 0.1f, 0.0f);
            SetColor(r, g, b);
        }
        if (Input.GetKey(KeyCode.Alpha2))
        {
            r = Mathf.Min(r + 0.1f, 1.0f);
            SetColor(r, g, b);
        }
        if (Input.GetKey(KeyCode.Alpha3))
        {
            g = Mathf.Max(g - 0.1f, 0.0f);
            SetColor(r, g, b);
        }
        if (Input.GetKey(KeyCode.Alpha4))
        {
            g = Mathf.Min(g + 0.1f, 1.0f);
            SetColor(r, g, b);
        }
        if (Input.GetKey(KeyCode.Alpha5))
        {
            b = Mathf.Max(b - 0.1f, 0.0f);
            SetColor(r, g, b);
        }
        if (Input.GetKey(KeyCode.Alpha6))
        {
            b = Mathf.Min(b + 0.1f, 1.0f);
            SetColor(r, g, b);
        }
    }
}
