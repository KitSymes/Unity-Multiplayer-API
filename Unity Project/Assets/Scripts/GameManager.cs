using KitSymes.GTRP;
using UnityEngine;
using UnityEngine.UI;

public partial class GameManager : NetworkBehaviour
{
    [SerializeField]
    NetworkObject _ballPrefab;
    Ball _ball;
    [SyncVar("ScoreLChanged")]
    public int scoreL = 0;
    [SyncVar(nameof(ScoreRChanged))]
    public int scoreR = 0;

    [SerializeField]
    private Text _scoreLText;
    [SerializeField]
    private Text _scoreRText;

    public NetworkObject playerL;
    public NetworkObject playerR;

    public bool _gameReady = false;

    public override void OnServerStart()
    {
        NetworkManager.GetInstance().ServerOnPlayerConnect += OnPlayerConnect;
        NetworkManager.GetInstance().ServerOnPlayerDisconnect += OnPlayerDisonnect;
        _ball = Instantiate(_ballPrefab).GetComponent<Ball>();
        NetworkManager.Spawn(_ball.gameObject);
        ResetBall();
    }

    void OnPlayerConnect(uint id)
    {
        NetworkObject player = NetworkManager.GetInstance().GetPlayerObject(id);
        if (playerL == null)
        {
            playerL = player;
            player.transform.position = new Vector3(-7.0f, 0.0f);
        }
        else if (playerR == null)
        {
            playerR = player;
            player.transform.position = new Vector3(7.0f, 0.0f);
        }

        if (playerL != null && playerR != null)
            _gameReady = true;
    }
    void OnPlayerDisonnect(uint id)
    {
        NetworkObject player = NetworkManager.GetInstance().GetPlayerObject(id);
        if (playerL == player)
            playerL = null;
        if (playerR == player)
            playerR = null;

        ResetBall();
    }

    public void ResetBall()
    {
        _ball.transform.position = new Vector3();
        _ball.SetDirection(new Vector3(0.0f, 0.0f));
        _ball.lastTouched = null;
        _ball.ballSpeed = _ball.startSpeed;
    }

    public void OutOfBounds()
    {
        if (_ball.lastTouched == playerL)
            RScore();
        else if (_ball.lastTouched == playerR)
            LScore();
        else
        {
            ResetBall();
            _ball.SetDirection(new Vector3(1.0f, 0.0f));
        }
    }

    public void LScore()
    {
        scoreL++;
        ResetBall();
        _ball.SetDirection(new Vector3(1.0f, 0.0f));
    }

    public void RScore()
    {
        scoreR++;
        ResetBall();
        _ball.SetDirection(new Vector3(-1.0f, 0.0f));
    }

    void Update()
    {
        if (_gameReady)
        {
            _gameReady = false;
            _ball.SetDirection(new Vector3(-1.0f, 0.0f));

            playerL.ChangeAuthority(true);
            playerR.ChangeAuthority(true);
        }
    }

    void ScoreLChanged(int prev, int newV) { _scoreLText.text = "" + newV; }

    void ScoreRChanged(int prev, int newV) { _scoreRText.text = "" + newV; }
}
