using UnityEngine;
using BlockDropArena.Core;
using BlockDropArena.Presentation;

/// <summary>
/// Script "tudo em um" para testar a engine na Unity.
/// Anexe a um GameObject vazio em uma cena com Main Camera e aperte Play.
/// Não precisa de sprites, prefabs ou materiais: tudo é criado por código.
/// Controles: setas (mover/soft drop), Cima ou X (girar horário), Z (anti-horário),
/// Espaço (hard drop), Esc (pausa), R (reiniciar).
/// </summary>
public class TetrisUnityBootstrap : MonoBehaviour, IGameRenderer
{
    [Tooltip("Mesma seed = mesma sequência de peças. 0 = aleatória.")]
    public int seed = 0;

    private GameEngine _engine;
    private InputRouter _input;
    private SpriteRenderer[,] _cells;
    private readonly int[,] _view = new int[Board.Rows, Board.Cols];
    private Sprite _square;

    // Cores por valor da matriz de renderização: 0 vazio, 1 fixo, 2 ativa, 3 fantasma
    private static readonly Color[] Palette =
    {
        new Color(0.10f, 0.10f, 0.14f),
        new Color(0.45f, 0.55f, 0.75f),
        new Color(0.20f, 0.85f, 0.95f),
        new Color(0.30f, 0.30f, 0.40f),
    };

    void Start()
    {
        SetupCamera();
        BuildGrid();
        StartNewGame();
    }

    void StartNewGame()
    {
        _engine = seed == 0 ? new GameEngine() : new GameEngine(seed);
        _input = new InputRouter(_engine);
        _engine.Changed += () => Render(_engine);
        _engine.LinesCleared += n => Debug.Log("Linhas eliminadas: " + n);   // gancho para áudio/VFX
        _engine.Start();
    }

    void Update()
    {
        // 1) INPUT -> engine (via InputRouter)
        Bind(KeyCode.LeftArrow,  GameAction.Left);
        Bind(KeyCode.RightArrow, GameAction.Right);
        Bind(KeyCode.DownArrow,  GameAction.SoftDrop);
        Bind(KeyCode.Space,      GameAction.HardDrop);
        Bind(KeyCode.UpArrow,    GameAction.RotateCW);
        Bind(KeyCode.X,          GameAction.RotateCW);
        Bind(KeyCode.Z,          GameAction.RotateCCW);
        Bind(KeyCode.Escape,     GameAction.Pause);
        if (Input.GetKeyDown(KeyCode.R)) StartNewGame();

        // 2) LOOP: todo frame, com o tempo em segundos
        _input.Update(Time.deltaTime);
        _engine.Tick(Time.deltaTime);
    }

    private void Bind(KeyCode key, GameAction action)
    {
        if (Input.GetKeyDown(key)) _input.Press(action);
        if (Input.GetKeyUp(key))   _input.Release(action);
    }

    // ---------------- Renderização (implementa IGameRenderer) ----------------
    public void Render(GameEngine e)
    {
        RenderGrid.Fill(e, _view);
        for (int r = 0; r < Board.Rows; r++)
            for (int c = 0; c < Board.Cols; c++)
                _cells[r, c].color = Palette[_view[r, c]];
    }

    private void BuildGrid()
    {
        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f); // 1 pixel = 1 unidade

        _cells = new SpriteRenderer[Board.Rows, Board.Cols];
        for (int r = 0; r < Board.Rows; r++)
        {
            for (int c = 0; c < Board.Cols; c++)
            {
                var go = new GameObject("cell_" + r + "_" + c);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(c, -r, 0);   // linha 0 no topo
                go.transform.localScale = new Vector3(0.92f, 0.92f, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _square;
                _cells[r, c] = sr;
            }
        }
    }

    private void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        cam.orthographic = true;
        cam.orthographicSize = 11f;
        cam.transform.position = new Vector3((Board.Cols - 1) / 2f, -(Board.Rows - 1) / 2f, -10f);
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.clearFlags = CameraClearFlags.SolidColor;
    }

    // HUD simples (substitua por TextMeshPro depois)
    void OnGUI()
    {
        if (_engine == null) return;
        GUI.Label(new Rect(10, 10, 300, 24), "Pontos: " + _engine.Score + "   Linhas: " + _engine.Lines + "   Nivel: " + _engine.Level);
        GUI.Label(new Rect(10, 34, 300, 24), "Proxima: " + _engine.NextType);
        if (_engine.State == GameState.GameOver) GUI.Label(new Rect(10, 58, 300, 24), "GAME OVER - aperte R");
        if (_engine.State == GameState.Paused)   GUI.Label(new Rect(10, 58, 300, 24), "PAUSADO");
    }
}
