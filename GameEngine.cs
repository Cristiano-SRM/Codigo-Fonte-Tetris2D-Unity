using System;
using System.Collections.Generic;

namespace BlockDropArena.Core
{
    public enum GameState { Idle, Playing, Paused, GameOver }

    /// <summary>
    /// Orquestra o jogo: gravidade, bloqueio, pontuação, sequência de peças.
    /// Também NÃO conhece gráficos nem teclado. Seu frontend interage por:
    ///   1) chamar Tick(deltaTime) a cada frame           (loop do jogo)
    ///   2) chamar os métodos de ação (MoveLeft, ...)     (input)
    ///   3) ouvir o evento Changed / ler as propriedades  (renderização)
    /// </summary>
    public sealed class GameEngine
    {
        // ---------- Configuração ----------
        public double LockDelay = 0.5;        // segundos que a peça pode ficar no chão antes de travar
        public int MaxLockResets = 15;        // limite de "resets" do lock delay (evita stall infinito)

        // ---------- Estado público (somente leitura para o renderizador) ----------
        public Board Board { get; } = new Board();
        public Piece Current { get; private set; }
        public TetrominoType NextType { get; private set; }
        public GameState State { get; private set; } = GameState.Idle;
        public int Score { get; private set; }
        public int Lines { get; private set; }
        public int Level => 1 + Lines / 10;

        // ---------- Eventos (hooks para UI, áudio, efeitos, telemetria) ----------
        public event Action Changed;               // algo visual mudou: redesenhe
        public event Action<int> LinesCleared;     // quantidade de linhas (1..4): toque som/efeito
        public event Action PieceLocked;
        public event Action GameOver;

        // ---------- Internos ----------
        private readonly Random _rng;
        private readonly List<TetrominoType> _bag = new List<TetrominoType>(7);
        private double _gravityAcc;
        private double _lockTimer;
        private int _lockResets;

        /// <param name="seed">Mesma seed = mesma sequência de peças (útil para duelos e replays).</param>
        public GameEngine(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // =====================================================================
        //  CICLO DE VIDA
        // =====================================================================
        public void Start()
        {
            Board.Clear();
            Score = 0; Lines = 0;
            _bag.Clear();
            NextType = DrawFromBag();
            State = GameState.Playing;
            SpawnPiece();
            Changed?.Invoke();
        }

        public void TogglePause()
        {
            if (State == GameState.Playing) State = GameState.Paused;
            else if (State == GameState.Paused) State = GameState.Playing;
            Changed?.Invoke();
        }

        // =====================================================================
        //  LOOP: chame UMA vez por frame com o tempo decorrido em SEGUNDOS
        //  (Unity: Time.deltaTime | Blazor: diferença entre ticks do timer)
        // =====================================================================
        public void Tick(double deltaSeconds)
        {
            if (State != GameState.Playing) return;

            // Gravidade: acumula tempo e desce 1 linha a cada "intervalo".
            _gravityAcc += deltaSeconds;
            double interval = GravityInterval;
            while (_gravityAcc >= interval)
            {
                _gravityAcc -= interval;
                var down = Current.Translate(0, 1);
                if (Board.Fits(down)) Current = down;
                else { _gravityAcc = 0; break; }
            }

            // Lock delay: peça apoiada espera um instante antes de travar (permite ajustes finos).
            if (IsGrounded())
            {
                _lockTimer += deltaSeconds;
                if (_lockTimer >= LockDelay) LockCurrent();
            }
            else _lockTimer = 0;

            Changed?.Invoke();
        }

        /// <summary>Segundos por linha (curva do guideline: (0.8 - (nível-1)*0.007)^(nível-1)).</summary>
        public double GravityInterval
        {
            get
            {
                double v = Math.Pow(0.8 - (Level - 1) * 0.007, Level - 1);
                return Math.Max(v, 0.001);
            }
        }

        // =====================================================================
        //  AÇÕES DE INPUT: ligue cada tecla/botão/toque a UM destes métodos.
        //  Todos são seguros de chamar a qualquer momento (ignoram se não estiver Playing).
        // =====================================================================
        public void MoveLeft()  => TryTransform(Current?.Translate(-1, 0));
        public void MoveRight() => TryTransform(Current?.Translate(1, 0));
        public void RotateCW()  => TryRotate(true);
        public void RotateCCW() => TryRotate(false);

        /// <summary>Desce 1 linha (+1 ponto). Chame repetidamente enquanto a tecla estiver pressionada.</summary>
        public void SoftDrop()
        {
            if (State != GameState.Playing) return;
            var down = Current.Translate(0, 1);
            if (Board.Fits(down))
            {
                Current = down; Score += 1; _gravityAcc = 0;
                Changed?.Invoke();
            }
        }

        /// <summary>Joga a peça até o chão e trava na hora (+2 pontos por linha percorrida).</summary>
        public void HardDrop()
        {
            if (State != GameState.Playing) return;
            Current = GetGhost(out int rows);
            Score += 2 * rows;
            LockCurrent();
            Changed?.Invoke();
        }

        /// <summary>Peça-fantasma: onde a atual cairia. Útil para o renderizador.</summary>
        public Piece GetGhost()
        {
            return GetGhost(out _);
        }

        // =====================================================================
        //  INTERNOS
        // =====================================================================
        private Piece GetGhost(out int rows)
        {
            rows = 0;
            if (Current == null) return null;
            var p = Current;
            while (true)
            {
                var d = p.Translate(0, 1);
                if (!Board.Fits(d)) break;
                p = d; rows++;
            }
            return p;
        }

        private bool IsGrounded() => !Board.Fits(Current.Translate(0, 1));

        private void TryTransform(Piece candidate)
        {
            if (State != GameState.Playing || candidate == null) return;
            if (!Board.Fits(candidate)) return;
            Current = candidate;
            ResetLockIfGrounded();
            Changed?.Invoke();
        }

        /// <summary>Rotação com "wall kicks" simples: tenta a posição original e pequenos deslocamentos.</summary>
        private static readonly int[][] Kicks =
        {
            new[] { 0, 0 }, new[] { -1, 0 }, new[] { 1, 0 }, new[] { -2, 0 }, new[] { 2, 0 }, new[] { 0, -1 }
        };

        private void TryRotate(bool cw)
        {
            if (State != GameState.Playing || Current == null) return;
            var rotated = Current.Rotate(cw);
            foreach (var k in Kicks)
            {
                var cand = rotated.Translate(k[0], k[1]);
                if (Board.Fits(cand))
                {
                    Current = cand;
                    ResetLockIfGrounded();
                    Changed?.Invoke();
                    return;
                }
            }
        }

        private void ResetLockIfGrounded()
        {
            if (IsGrounded() && _lockResets < MaxLockResets)
            {
                _lockTimer = 0;
                _lockResets++;
            }
        }

        private void LockCurrent()
        {
            bool overflow = Board.Lock(Current);
            PieceLocked?.Invoke();

            if (overflow) { EndGame(); return; }

            int cleared = Board.ClearLines();
            if (cleared > 0)
            {
                // 100 / 300 / 500 / 800 x nível (nível calculado ANTES de somar as linhas)
                int[] table = { 0, 100, 300, 500, 800 };
                Score += table[cleared] * Level;
                Lines += cleared;
                LinesCleared?.Invoke(cleared);
            }
            SpawnPiece();
        }

        private void SpawnPiece()
        {
            Current = PieceFactory.Create(NextType);
            NextType = DrawFromBag();
            _gravityAcc = 0; _lockTimer = 0; _lockResets = 0;
            if (!Board.Fits(Current)) EndGame();
        }

        private void EndGame()
        {
            State = GameState.GameOver;
            GameOver?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>7-bag: embaralha as 7 peças e as entrega uma a uma (distribuição justa, padrão competitivo).</summary>
        private TetrominoType DrawFromBag()
        {
            if (_bag.Count == 0)
            {
                foreach (TetrominoType t in Enum.GetValues(typeof(TetrominoType))) _bag.Add(t);
                for (int i = _bag.Count - 1; i > 0; i--)             // Fisher-Yates
                {
                    int j = _rng.Next(i + 1);
                    var tmp = _bag[i]; _bag[i] = _bag[j]; _bag[j] = tmp;
                }
            }
            var next = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);
            return next;
        }
    }
}
