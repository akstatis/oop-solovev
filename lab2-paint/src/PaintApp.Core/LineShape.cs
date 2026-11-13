using System.Drawing;

namespace PaintApp.Core;

/// <summary>Отрезок от точки нажатия до точки отпускания.</summary>
public sealed class LineShape : Shape
{
    public LineShape(Point start, Color color, int width)
        : base(start, color, width)
    {
    }

    public override string Kind => "Line";

    public override void Draw(Graphics graphics)
    {
        using var pen = CreatePen();
        graphics.DrawLine(pen, Start, End);
    }

    /// <summary>Точка считается своей, если лежит рядом с отрезком.</summary>
    public override bool Contains(Point point) => DistanceToSegment(point, Start, End) <= Tolerance;
}
