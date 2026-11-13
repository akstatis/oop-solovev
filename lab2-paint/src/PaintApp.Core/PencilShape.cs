using System.Drawing;

namespace PaintApp.Core;

/// <summary>Кривая карандашом: хранит все точки, через которые прошла мышь.</summary>
public sealed class PencilShape : Shape
{
    private readonly List<Point> _points = new();

    public PencilShape(Point start, Color color, int width)
        : base(start, color, width)
    {
        _points.Add(start);
    }

    /// <summary>Конструктор для загрузки из файла: сразу все точки.</summary>
    public PencilShape(IEnumerable<Point> points, Color color, int width)
        : base(points.FirstOrDefault(), color, width)
    {
        _points.Clear();
        _points.AddRange(points);
        if (_points.Count == 0)
        {
            throw new ArgumentException("У кривой должна быть хотя бы одна точка.", nameof(points));
        }

        Start = _points[0];
        End = _points[^1];
    }

    public override string Kind => "Pencil";

    public IReadOnlyList<Point> Points => _points;

    /// <summary>Каждое движение мыши добавляет точку и тянет «конец» за собой.</summary>
    public override void MoveEnd(Point point)
    {
        if (_points.Count > 0 && _points[^1] == point)
        {
            return;
        }

        _points.Add(point);
        End = point;
    }

    public override Rectangle Bounds
    {
        get
        {
            if (_points.Count == 0)
            {
                return Rectangle.Empty;
            }

            var minX = _points.Min(p => p.X);
            var minY = _points.Min(p => p.Y);
            var maxX = _points.Max(p => p.X);
            var maxY = _points.Max(p => p.Y);
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }
    }

    public override void Draw(Graphics graphics)
    {
        using var pen = CreatePen();
        if (_points.Count == 1)
        {
            var half = Math.Max(1, Width / 2);
            graphics.FillEllipse(new SolidBrush(Color), _points[0].X - half, _points[0].Y - half, half * 2, half * 2);
            return;
        }

        graphics.DrawLines(pen, _points.ToArray());
    }

    public override bool Contains(Point point)
    {
        if (_points.Count == 1)
        {
            return Distance(point, _points[0]) <= Tolerance;
        }

        for (var i = 1; i < _points.Count; i++)
        {
            if (DistanceToSegment(point, _points[i - 1], _points[i]) <= Tolerance)
            {
                return true;
            }
        }

        return false;
    }

    public override void Translate(int dx, int dy)
    {
        for (var i = 0; i < _points.Count; i++)
        {
            _points[i] = new Point(_points[i].X + dx, _points[i].Y + dy);
        }

        Start = _points[0];
        End = _points[^1];
    }
}
