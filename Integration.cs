using BlockDropArena.Core;

// =============================================================================
//  CAMADA DE INTEGRAÇÃO (exemplos). Este arquivo é o ÚNICO que fala com a
//  tecnologia do frontend. Core/ nunca referencia Unity nem Blazor.
//  Os blocos de Unity e Blazor estão comentados: copie o que for usar.
// =============================================================================

namespace BlockDropArena.Presentation
{
    /// <summary>
    /// Contrato do renderizador. Implemente com Unity (sprites/Tilemap), Blazor (Canvas/SVG/CSS grid), etc.
    /// A engine chama Render(engine) sempre que algo muda; leia o estado e desenhe.
    /// </summary>
    public interface IGameRenderer
    {
        void Render(GameEngine engine);
    }

    /// <summary>
    /// Helper opcional: monta uma matriz 20x10 pronta para desenhar, com
    /// 0 = vazio, 1 = bloco fixo, 2 = peça ativa, 3 = fantasma.
    /// (A matriz do Board continua estritamente 0/1; esta é só uma "foto" para a view.)
    /// </summary>
    public static class RenderGrid
    {
        public static void Fill(GameEngine e, int[,] target)
        {
            for (int r = 0; r < Board.Rows; r++)
                for (int c = 0; c < Board.Cols; c++)
                    target[r, c] = e.Board[r, c];

            if (e.Current == null) return;

            foreach (var b in e.GetGhost().Blocks)
                if (b.Y >= 0 && target[b.Y, b.X] == 0) target[b.Y, b.X] = 3;

            foreach (var b in e.Current.Blocks)
                if (b.Y >= 0) target[b.Y, b.X] = 2;
        }
    }
}

/* =============================================================================
   EXEMPLO 1: UNITY (WebGL)
   =============================================================================
using UnityEngine;
using BlockDropArena.Core;
using BlockDropArena.Presentation;

public class TetrisBehaviour : MonoBehaviour
{
    [SerializeField] private MonoBehaviour rendererBehaviour;   // um componente que implemente IGameRenderer
    private IGameRenderer _renderer;
    private GameEngine _engine;
    private InputRouter _input;

    void Start()
    {
        _renderer = (IGameRenderer)rendererBehaviour;
        _engine = new GameEngine(seed: 12345);       // mesma seed = mesma sequência (duelos/replays)
        _input = new InputRouter(_engine);
        _engine.Changed += () => _renderer.Render(_engine);
        _engine.LinesCleared += n => Debug.Log($"Linhas: {n}");   // ligue áudio/VFX aqui
        _engine.Start();
    }

    void Update()
    {
        // 1) INPUT -> Press/Release (use o seu sistema: Input Manager, Input System, botões de UI/touch...)
        if (Input.GetKeyDown(KeyCode.LeftArrow))  _input.Press(GameAction.Left);
        if (Input.GetKeyUp(KeyCode.LeftArrow))    _input.Release(GameAction.Left);
        if (Input.GetKeyDown(KeyCode.RightArrow)) _input.Press(GameAction.Right);
        if (Input.GetKeyUp(KeyCode.RightArrow))   _input.Release(GameAction.Right);
        if (Input.GetKeyDown(KeyCode.DownArrow))  _input.Press(GameAction.SoftDrop);
        if (Input.GetKeyUp(KeyCode.DownArrow))    _input.Release(GameAction.SoftDrop);
        if (Input.GetKeyDown(KeyCode.Space))      _input.Press(GameAction.HardDrop);
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.X)) _input.Press(GameAction.RotateCW);
        if (Input.GetKeyDown(KeyCode.Z))          _input.Press(GameAction.RotateCCW);
        if (Input.GetKeyDown(KeyCode.Escape))     _input.Press(GameAction.Pause);

        // 2) LOOP: sempre os dois, todo frame
        _input.Update(Time.deltaTime);
        _engine.Tick(Time.deltaTime);
    }
}

   =============================================================================
   EXEMPLO 2: BLAZOR (Razor, ex.: Pages/Game.razor)
   =============================================================================
@page "/game"
@using BlockDropArena.Core
@implements IDisposable

<div tabindex="0" @ref="_root" @onkeydown="OnKeyDown" @onkeyup="OnKeyUp" style="outline:none">
    <div class="grid">
        @for (int r = 0; r < Board.Rows; r++)
        {
            @for (int c = 0; c < Board.Cols; c++)
            {
                <div class="cell v@(_view[r, c])"></div>
            }
        }
    </div>
    <p>Pontos: @_engine.Score | Linhas: @_engine.Lines | Nível: @_engine.Level</p>
</div>

@code {
    private ElementReference _root;
    private readonly GameEngine _engine = new GameEngine();
    private InputRouter _input;
    private readonly int[,] _view = new int[Board.Rows, Board.Cols];
    private PeriodicTimer _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
    private DateTime _last = DateTime.UtcNow;

    protected override void OnInitialized()
    {
        _input = new InputRouter(_engine);
        _engine.Changed += () => BlockDropArena.Presentation.RenderGrid.Fill(_engine, _view);
        _engine.Start();
        _ = LoopAsync();
    }

    protected override async Task OnAfterRenderAsync(bool first)
    {
        if (first) await _root.FocusAsync();   // para receber o teclado
    }

    // LOOP: equivalente ao Update() da Unity
    private async Task LoopAsync()
    {
        while (await _timer.WaitForNextTickAsync())
        {
            var now = DateTime.UtcNow;
            double dt = (now - _last).TotalSeconds;
            _last = now;
            _input.Update(dt);
            _engine.Tick(dt);
            StateHasChanged();
        }
    }

    // INPUT: ignore e.Repeat (o DAS/ARR é responsabilidade do InputRouter)
    private void OnKeyDown(KeyboardEventArgs e) { if (!e.Repeat && Map(e.Code, out var a)) _input.Press(a); }
    private void OnKeyUp(KeyboardEventArgs e)   { if (Map(e.Code, out var a)) _input.Release(a); }

    private static bool Map(string code, out GameAction a)
    {
        switch (code)
        {
            case "ArrowLeft":  a = GameAction.Left; return true;
            case "ArrowRight": a = GameAction.Right; return true;
            case "ArrowDown":  a = GameAction.SoftDrop; return true;
            case "Space":      a = GameAction.HardDrop; return true;
            case "ArrowUp":
            case "KeyX":       a = GameAction.RotateCW; return true;
            case "KeyZ":       a = GameAction.RotateCCW; return true;
            case "Escape":     a = GameAction.Pause; return true;
            default:           a = default; return false;
        }
    }

    public void Dispose() => _timer.Dispose();
}
   ============================================================================= */
