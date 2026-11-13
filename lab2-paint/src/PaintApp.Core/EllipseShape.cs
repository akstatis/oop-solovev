using System.Drawing;

namespace PaintApp.Core;

/// <summary>Эллипс, вписанный в прямоугольник из двух точек.</summary>
public sealed class EllipseShape : Shape
{
    public EllipseShape(Point start, Color color, int width)
        : base(start, color, width)
    {
    }

    public override string Kind => "Ellipse";

    public override void Draw(Graphics graphics)
    {
        using var pen = CreatePen();
        var bounds = Bounds;
        graphics.DrawEllipse(pen, bounds);
    }

    /// <summary>Точка внутри эллипса: проверяем уравнение эллипса, а не прямоугольник.</summary>
    public override bool Contains(Point point)
    {
        var bounds = Bounds;
        bounds.Inflate(Tolerance, Tolerance);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        var radiusX = bounds.Width / 2.0;
        var radiusY = bounds.Height / 2.0;
        var centerX = bounds.X + radiusX;
        var centerY = bounds.Y + radiusY;

        var normalizedX = (point.X - centerX) / radiusX;
        var normalizedY = (point.Y - centerY) / radiusY;
        return (normalizedX * normalizedX) + (normalizedY * normalizedY) <= 1.0;
    }
}
