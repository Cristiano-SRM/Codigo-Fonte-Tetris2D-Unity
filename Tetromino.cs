using System;

namespace BlockDropArena.Core
{
    /// <summary>Coordenada inteira de uma célula. X = coluna (0..9), Y = linha (0..19, cresce PARA BAIXO).</summary>
    public readonly struct Vec2
    {
        public readonly int X;
        public readonly int Y;
        public Vec2(int x, int y) { X = x; Y = y; }
    }

    public enum TetrominoType { I, O, T, S, Z, J, L }

    /// <summary>
    /// Tetrominó = conjunto de 4 coordenadas (x1,y1)...(x4,y4) + um pivô de rotação.
    /// A classe é IMUTÁVEL: Translate/Rotate devolvem uma NOVA peça. Isso permite
    /// "testar" um movimento (board.Fits(candidata)) sem desfazer nada se ele for inválido.
    /// </summary>
    public sealed class Piece
    {
        public TetrominoType Type { get; }
        public Vec2[] Blocks { get; }      // exatamente 4 elementos
        public double PivotX { get; }      // pivô pode ser fracionário (ex.: peça I gira em torno de 4.5, 0.5)
        public double PivotY { get; }

        public Piece(TetrominoType type, Vec2[] blocks, double pivotX, double pivotY)
        {
            Type = type; Blocks = blocks; PivotX = pivotX; PivotY = pivotY;
        }

        /// <summary>
        /// TRANSLAÇÃO: (x, y) -> (x + dx, y + dy) aplicada a cada uma das 4 coordenadas.
        /// O pivô acompanha a peça.
        /// </summary>
        public Piece Translate(int dx, int dy)
        {
            var moved = new Vec2[4];
            for (int i = 0; i < 4; i++)
                moved[i] = new Vec2(Blocks[i].X + dx, Blocks[i].Y + dy);
            return new Piece(Type, moved, PivotX + dx, PivotY + dy);
        }

        /// <summary>
        /// ROTAÇÃO (transformação linear) em torno do pivô (px, py):
        ///
        ///   | x' - px |   | cos θ  -sin θ |   | x - px |
        ///   |         | = |               | * |        |
        ///   | y' - py |   | sin θ   cos θ |   | y - py |
        ///
        /// Como o eixo Y do grid cresce para baixo, θ = +90° gira no sentido HORÁRIO na tela.
        /// O resultado é arredondado para o inteiro mais próximo (elimina ruído de ponto flutuante,
        /// ex.: cos(90°) = 6e-17).
        /// </summary>
        public Piece Rotate(bool clockwise)
        {
            if (Type == TetrominoType.O) return this; // O é simétrica: rotação não altera nada

            double theta = (clockwise ? 1 : -1) * Math.PI / 2.0;
            double cos = Math.Cos(theta);
            double sin = Math.Sin(theta);

            var rotated = new Vec2[4];
            for (int i = 0; i < 4; i++)
            {
                double rx = Blocks[i].X - PivotX;   // vetor relativo ao pivô
                double ry = Blocks[i].Y - PivotY;

                double nx = PivotX + (rx * cos - ry * sin);
                double ny = PivotY + (rx * sin + ry * cos);

                rotated[i] = new Vec2(
                    (int)Math.Round(nx, MidpointRounding.AwayFromZero),
                    (int)Math.Round(ny, MidpointRounding.AwayFromZero));
            }
            return new Piece(Type, rotated, PivotX, PivotY);
        }
    }

    /// <summary>Define o formato inicial (spawn) de cada peça no topo do tabuleiro.</summary>
    public static class PieceFactory
    {
        public static Piece Create(TetrominoType t)
        {
            switch (t)
            {
                // Pivô da I fica no CENTRO geométrico das 4 células (4.5, 0.5) para a rotação
                // cair sempre em coordenadas inteiras.
                case TetrominoType.I: return Make(t, 4.5, 0.5, 3,0, 4,0, 5,0, 6,0);
                case TetrominoType.O: return Make(t, 4.5, 0.5, 4,0, 5,0, 4,1, 5,1);
                case TetrominoType.T: return Make(t, 4, 1, 4,0, 3,1, 4,1, 5,1);
                case TetrominoType.S: return Make(t, 4, 1, 4,0, 5,0, 3,1, 4,1);
                case TetrominoType.Z: return Make(t, 4, 1, 3,0, 4,0, 4,1, 5,1);
                case TetrominoType.J: return Make(t, 4, 1, 3,0, 3,1, 4,1, 5,1);
                case TetrominoType.L: return Make(t, 4, 1, 5,0, 3,1, 4,1, 5,1);
                default: throw new ArgumentOutOfRangeException(nameof(t));
            }
        }

        private static Piece Make(TetrominoType t, double px, double py,
                                  int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4)
        {
            return new Piece(t,
                new[] { new Vec2(x1, y1), new Vec2(x2, y2), new Vec2(x3, y3), new Vec2(x4, y4) },
                px, py);
        }
    }
}
