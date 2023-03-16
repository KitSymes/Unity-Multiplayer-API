using KitSymes.GTRP;
using UnityEngine;

public partial class PlayerScript : NetworkBehaviour
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

    // Start is called before the first frame update
    void Start()
    {
        //DebugSourceGenerator.GetTestText();
    }

    // Update is called once per frame
    void Update()
    {
        if (!networkObject.IsOwner() || !networkObject.HasAuthority())
            return;

        if (Input.GetKey(KeyCode.W))
            transform.position += Vector3.up * Time.deltaTime * paddleSpeed;
        if (Input.GetKey(KeyCode.S))
            transform.position -= Vector3.up * Time.deltaTime * paddleSpeed;
    }
}
