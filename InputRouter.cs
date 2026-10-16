namespace BlockDropArena.Core
{
    public enum GameAction { Left, Right, SoftDrop, HardDrop, RotateCW, RotateCCW, Pause }

    /// <summary>
    /// PONTE ENTRE SEU INPUT E A ENGINE.
    ///
    /// Seu frontend só precisa fazer duas coisas, independentemente da tecnologia:
    ///   - quando o jogador PRESSIONA algo   -> router.Press(GameAction.X)
    ///   - quando o jogador SOLTA algo       -> router.Release(GameAction.X)
    ///   - a cada frame                      -> router.Update(deltaSeconds)
    ///
    /// O router cuida de DAS/ARR (repetição horizontal estilo competitivo), então
    /// NÃO use o "key repeat" do sistema operacional/navegador (ele é lento e inconsistente).
    /// </summary>
    public sealed class InputRouter
    {
        // Ajustes que jogadores competitivos esperam poder configurar (segundos).
        public double Das = 0.167;          // atraso antes de começar a repetir
        public double Arr = 0.033;          // intervalo entre repetições (0 = teleporta até a parede)
        public double SoftDropInterval = 0.02;

        private readonly GameEngine _engine;
        private int _dir;                   // -1 esquerda, +1 direita, 0 nenhum (a ÚLTIMA tecla pressionada vence)
        private double _dasTimer, _arrTimer;
        private bool _softHeld;
        private double _softTimer;

        public InputRouter(GameEngine engine) { _engine = engine; }

        public void Press(GameAction a)
        {
            switch (a)
            {
                case GameAction.Left:     _dir = -1; _dasTimer = _arrTimer = 0; _engine.MoveLeft();  break;
                case GameAction.Right:    _dir = 1;  _dasTimer = _arrTimer = 0; _engine.MoveRight(); break;
                case GameAction.SoftDrop: _softHeld = true; _softTimer = 0; _engine.SoftDrop(); break;
                case GameAction.HardDrop: _engine.HardDrop(); break;
                case GameAction.RotateCW: _engine.RotateCW(); break;
                case GameAction.RotateCCW:_engine.RotateCCW(); break;
                case GameAction.Pause:    _engine.TogglePause(); break;
            }
        }

        public void Release(GameAction a)
        {
            if (a == GameAction.Left && _dir == -1) _dir = 0;
            else if (a == GameAction.Right && _dir == 1) _dir = 0;
            else if (a == GameAction.SoftDrop) _softHeld = false;
        }

        /// <summary>Chame a cada frame, ANTES ou DEPOIS de engine.Tick (tanto faz, mas sempre os dois).</summary>
        public void Update(double dt)
        {
            if (_dir != 0)
            {
                _dasTimer += dt;
                if (_dasTimer >= Das)
                {
                    if (Arr <= 0)
                    {
                        for (int i = 0; i < Board.Cols; i++) Step();   // ARR instantâneo
                    }
                    else
                    {
                        _arrTimer += dt;
                        while (_arrTimer >= Arr) { _arrTimer -= Arr; Step(); }
                    }
                }
            }

            if (_softHeld)
            {
                _softTimer += dt;
                while (_softTimer >= SoftDropInterval) { _softTimer -= SoftDropInterval; _engine.SoftDrop(); }
            }
        }

        private void Step()
        {
            if (_dir < 0) _engine.MoveLeft(); else _engine.MoveRight();
        }
    }
}
