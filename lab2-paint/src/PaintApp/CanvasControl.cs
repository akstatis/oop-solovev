using System.Drawing;
using System.Drawing.Drawing2D;
using PaintApp.Core;

namespace PaintApp;

/// <summary>
/// Холст: только показывает рисунок. Все действия с фигурами делает окно,
/// холст ничего не решает — он рисует список фигур и предпросмотр.
/// </summary>
public sealed class CanvasControl : Control
{
    public CanvasControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Cursor = Cursors.Cross;
        SetStyle(
            ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable,
            true);
    }

    public Drawing? Drawing { get; set; }

    /// <summary>Фигура, которую пользователь рисует прямо сейчас.</summary>
    public Shape? Preview { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);
        Drawing?.Draw(e.Graphics);
        Preview?.Draw(e.Graphics);
        Drawing?.DrawSelection(e.Graphics);
    }
}
