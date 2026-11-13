using System.Drawing;
using System.Drawing.Drawing2D;

namespace PaintApp.Core;

/// <summary>
/// Общая часть всех фигур: две точки, цвет и толщина. Класс абстрактный —
/// «просто фигуру» нарисовать нельзя, каждый наследник обязан уметь Draw и Contains.
/// </summary>
public abstract class Shape
{
    protected Shape(Point start, Color color, int width)
    {
        if (width is < 1 or > 64)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Толщина должна быть от 1 до 64.");
        }

        Start = start;
        End = start;
        Color = color;
        Width = width;
    }

    public Point Start { get; protected set; }

    public Point End { get; protected set; }

    public Color Color { get; }

    public int Width { get; }

    /// <summary>Имя типа для файла: "Line", "Rect", "Ellipse", "Pencil".</summary>
    public abstract string Kind { get; }

    /// <summary>Прямоугольник, в который фигура вписана (для выделения и попадания мышью).</summary>
    public virtual Rectangle Bounds => FromTwoPoints(Start, End);

    /// <summary>Допуск попадания мышью: половина толщины плюс небольшой запас.</summary>
    protected int Tolerance => Math.Max(4, (Width / 2) + 3);

    /// <summary>Тянуть можно в любую сторону — вторая точка просто следует за мышью.</summary>
    public virtual void MoveEnd(Point point) => End = point;

    /// <summary>Сдвинуть фигуру целиком (перетаскивание мышью).</summary>
    public virtual void Translate(int dx, int dy)
    {
        Start = new Point(Start.X + dx, Start.Y + dy);
        End = new Point(End.X + dx, End.Y + dy);
    }

    public abstract void Draw(Graphics graphics);

    /// <summary>Лежит ли точка на фигуре — каждая фигура решает сама.</summary>
    public abstract bool Contains(Point point);

    protected Pen CreatePen() => new(Color, Width)
    {
        StartCap = LineCap.Round,
        EndCap = LineCap.Round,
        LineJoin = LineJoin.Round,
    };

    protected static Rectangle FromTwoPoints(Point first, Point second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(first.X - second.X),
        Math.Abs(first.Y - second.Y));

    /// <summary>Расстояние от точки до отрезка — основа попадания по линии и карандашу.</summary>
    protected static double DistanceToSegment(Point point, Point start, Point end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared == 0)
        {
            return Distance(point, start);
        }

        var projection = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / (double)lengthSquared;
        projection = Math.Clamp(projection, 0d, 1d);
        var closest = new Point((int)Math.Round(start.X + (projection * dx)), (int)Math.Round(start.Y + (projection * dy)));
        return Distance(point, closest);
    }

    protected static double Distance(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
