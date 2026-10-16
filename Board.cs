namespace BlockDropArena.Core
{
    /// <summary>
    /// Espaço de estados: matriz 20 linhas x 10 colunas, cada célula é 0 (vazia) ou 1 (cheia).
    /// Esta classe NÃO conhece gráficos, input ou tempo: só matemática de matrizes.
    /// </summary>
    public sealed class Board
    {
        public const int Rows = 20;
        public const int Cols = 10;

        private readonly int[,] _cells = new int[Rows, Cols]; // [linha, coluna]

        /// <summary>Leitura somente-leitura para o renderizador: board[linha, coluna] -> 0 ou 1.</summary>
        public int this[int row, int col] => _cells[row, col];

        public void Clear()
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    _cells[r, c] = 0;
        }

        /// <summary>
        /// Célula livre? Fora das paredes laterais ou abaixo do chão = bloqueado.
        /// Y &lt; 0 (acima do topo) é considerado livre, pois peças giram parcialmente acima do grid.
        /// </summary>
        public bool IsFree(int x, int y)
        {
            if (x < 0 || x >= Cols || y >= Rows) return false;
            if (y < 0) return true;
            return _cells[y, x] == 0;
        }

        /// <summary>A peça cabe nesta posição? (4 coordenadas todas livres)</summary>
        public bool Fits(Piece p)
        {
            for (int i = 0; i < 4; i++)
                if (!IsFree(p.Blocks[i].X, p.Blocks[i].Y)) return false;
            return true;
        }

        /// <summary>
        /// Fixa a peça no tabuleiro (células viram 1).
        /// Retorna true se alguma célula ficou ACIMA do topo (y &lt; 0) => condição de game over.
        /// </summary>
        public bool Lock(Piece p)
        {
            bool overflow = false;
            for (int i = 0; i < 4; i++)
            {
                int x = p.Blocks[i].X, y = p.Blocks[i].Y;
                if (y < 0) { overflow = true; continue; }
                _cells[y, x] = 1;
            }
            return overflow;
        }

        /// <summary>
        /// ELIMINAÇÃO DE LINHAS:
        ///  - soma dos 10 elementos da linha == 10 => linha cheia;
        ///  - a queda é feita substituindo a linha atual pela superior (row[r] = row[r-1]),
        ///    repetindo até o topo, e zerando a linha 0.
        /// Após uma eliminação, a MESMA linha é reavaliada (a linha que desceu pode estar cheia).
        /// Retorna quantas linhas foram eliminadas (0 a 4).
        /// </summary>
        public int ClearLines()
        {
            int cleared = 0;
            int row = Rows - 1;

            while (row >= 0)
            {
                int sum = 0;
                for (int c = 0; c < Cols; c++) sum += _cells[row, c];

                if (sum == Cols)
                {
                    for (int r = row; r > 0; r--)
                        for (int c = 0; c < Cols; c++)
                            _cells[r, c] = _cells[r - 1, c];   // linha atual <- linha superior

                    for (int c = 0; c < Cols; c++) _cells[0, c] = 0;
                    cleared++;
                    // NÃO decrementa 'row': reavalia a linha que acabou de descer.
                }
                else
                {
                    row--;
                }
            }
            return cleared;
        }
    }
}
