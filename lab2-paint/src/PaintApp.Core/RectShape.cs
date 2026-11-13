using System.Drawing;

namespace PaintApp.Core;

/// <summary>Прямоугольник по двум точкам: тянуть можно в любую сторону.</summary>
public sealed class RectShape : Shape
{
    public RectShape(Point start, Color color, int width)
        : base(start, color, width)
    {
    }

    public override string Kind => "Rect";

    public override void Draw(Graphics graphics)
    {
        using var pen = CreatePen();
        var bounds = Bounds;
        graphics.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>Точка внутри прямоугольника (с допуском на толщину линии).</summary>
    public override bool Contains(Point point)
    {
        var bounds = Bounds;
        bounds.Inflate(Tolerance, Tolerance);
        return bounds.Contains(point);
    }
}
