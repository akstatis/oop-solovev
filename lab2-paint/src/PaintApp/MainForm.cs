using System.Drawing;
using PaintApp.Core;

namespace PaintApp;

public enum PaintTool
{
    Line = 0,
    Rect = 1,
    Ellipse = 2,
    Pencil = 3,
    Select = 4,
}

/// <summary>
/// Окно редактора: инструменты, мышь, файлы. Фигуры и правила живут в
/// <see cref="Drawing"/> и в наследниках <see cref="Shape"/>, окно только
/// передаёт им действия пользователя.
/// </summary>
public sealed class MainForm : Form
{
    private readonly Drawing _drawing = new();
    private readonly CommandHistory _history = new();
    private readonly CanvasControl _canvas = new();
    private readonly Dictionary<PaintTool, ToolStripButton> _toolButtons = new();
    private readonly ToolStripButton _undoButton = new();
    private readonly ToolStripButton _redoButton = new();
    private readonly ToolStripButton _colorButton = new();
    private readonly ToolStripStatusLabel _countLabel = new();
    private readonly ToolStripStatusLabel _hintLabel = new();
    private readonly NumericUpDown _widthBox = new();

    private PaintTool _tool = PaintTool.Line;
    private Color _color = Color.FromArgb(210, 40, 40);
    private int _width = 3;

    private Shape? _preview;
    private Shape? _draggedShape;
    private Point _lastPoint;
    private int _moveDx;
    private int _moveDy;

    private string? _currentPath;

    public MainForm(string? initialPath = null)
    {
        BuildLayout();
        SetTool(PaintTool.Line);
        if (!string.IsNullOrWhiteSpace(initialPath))
        {
            TryLoad(initialPath);
        }

        UpdateTitle();
        UpdateStatus();
    }

    private void BuildLayout()
    {
        Text = "Графический редактор";
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(820, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.75f);
        KeyPreview = true;

        _canvas.Dock = DockStyle.Fill;
        _canvas.Drawing = _drawing;
        _canvas.MouseDown += Canvas_MouseDown;
        _canvas.MouseMove += Canvas_MouseMove;
        _canvas.MouseUp += Canvas_MouseUp;
        _canvas.MouseEnter += (_, _) => _canvas.Focus();

        Controls.Add(_canvas);
        Controls.Add(BuildToolStrip());
        Controls.Add(BuildStatusStrip());
    }

    private ToolStrip BuildToolStrip()
    {
        var strip = new ToolStrip
        {
            GripStyle = ToolStripGripStyle.Hidden,
            Padding = new Padding(6, 4, 6, 4),
            ImageScalingSize = new Size(16, 16),
        };

        strip.Items.Add(MakeButton("Открыть…", "Открыть рисунок (Ctrl+O)", (_, _) => OpenDrawing()));
        strip.Items.Add(MakeButton("Сохранить…", "Сохранить рисунок (Ctrl+S)", (_, _) => SaveDrawing()));
        strip.Items.Add(MakeButton("PNG…", "Выгрузить в PNG (★★)", (_, _) => ExportPng()));
        strip.Items.Add(new ToolStripSeparator());
        _undoButton.Text = "Отменить";
        _undoButton.ToolTipText = "Отменить последнее действие (Ctrl+Z)";
        _undoButton.Click += (_, _) => Undo();
        _redoButton.Text = "Повторить";
        _redoButton.ToolTipText = "Повторить отменённое (Ctrl+Y)";
        _redoButton.Click += (_, _) => Redo();
        strip.Items.Add(_undoButton);
        strip.Items.Add(_redoButton);
        strip.Items.Add(MakeButton("Очистить", "Убрать все фигуры (отменяется через Ctrl+Z)", (_, _) => ClearCanvas()));
        strip.Items.Add(new ToolStripSeparator());

        AddToolButton(strip, PaintTool.Line, "Линия", "Линия: потяните мышью");
        AddToolButton(strip, PaintTool.Rect, "Прямоугольник", "Прямоугольник: тяните в любую сторону");
        AddToolButton(strip, PaintTool.Ellipse, "Эллипс", "Эллипс: тяните в любую сторону");
        AddToolButton(strip, PaintTool.Pencil, "Карандаш", "Карандаш: рисуйте кривую");
        AddToolButton(strip, PaintTool.Select, "Выбор", "Выбор: щёлкните фигуру, перетащите, Delete — удалить (★)");

        strip.Items.Add(new ToolStripSeparator());

        _colorButton.Text = "Цвет";
        _colorButton.ToolTipText = "Выбрать цвет";
        _colorButton.Image = CreateColorSwatch(_color);
        _colorButton.DisplayStyle = ToolStripItemDisplayStyle.ImageAndText;
        _colorButton.Click += (_, _) => ChooseColor();
        strip.Items.Add(_colorButton);

        strip.Items.Add(new ToolStripSeparator());
        strip.Items.Add(new ToolStripLabel("Толщина:"));
        _widthBox.Minimum = 1;
        _widthBox.Maximum = 32;
        _widthBox.Value = _width;
        _widthBox.TextAlign = HorizontalAlignment.Center;
        _widthBox.ValueChanged += (_, _) =>
        {
            _width = (int)_widthBox.Value;
            UpdateStatus($"Толщина: {_width} px");
        };

        strip.Items.Add(new ToolStripControlHost(_widthBox) { AutoSize = false, Width = 62 });
        return strip;
    }

    private void AddToolButton(ToolStrip strip, PaintTool tool, string text, string hint)
    {
        var button = new ToolStripButton(text)
        {
            CheckOnClick = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = $"{hint} (клавиша {(int)tool + 1})",
        };
        button.Click += (_, _) => SetTool(tool);
        _toolButtons[tool] = button;
        strip.Items.Add(button);
    }

    private static ToolStripButton MakeButton(string text, string hint, EventHandler onClick)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            ToolTipText = hint,
        };
        button.Click += onClick;
        return button;
    }

    private Control BuildStatusStrip()
    {
        var strip = new StatusStrip { SizingGrip = false };
        _countLabel.Text = "Фигур: 0";
        _hintLabel.Spring = true;
        _hintLabel.TextAlign = ContentAlignment.MiddleLeft;
        _hintLabel.Text = "";
        strip.Items.Add(_countLabel);
        strip.Items.Add(_hintLabel);
        return strip;
    }

    private void SetTool(PaintTool tool)
    {
        _tool = tool;
        foreach (var (kind, button) in _toolButtons)
        {
            button.Checked = kind == tool;
            button.BackColor = kind == tool ? Color.FromArgb(210, 228, 255) : Color.Transparent;
        }

        _canvas.Cursor = tool == PaintTool.Select ? Cursors.Hand : Cursors.Cross;
        UpdateStatus();
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { Color = _color, FullOpen = true };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _color = dialog.Color;
        _colorButton.Image?.Dispose();
        _colorButton.Image = CreateColorSwatch(_color);
        UpdateStatus($"Цвет: {_color.Name}");
    }

    private static Bitmap CreateColorSwatch(Color color)
    {
        var bitmap = new Bitmap(18, 18);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        using var brush = new SolidBrush(color);
        graphics.FillRectangle(brush, 1, 1, 16, 16);
        using var pen = new Pen(Color.FromArgb(90, 90, 90));
        graphics.DrawRectangle(pen, 0, 0, 17, 17);
        return bitmap;
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_tool == PaintTool.Select)
        {
            SelectAt(e.Location);
            return;
        }

        _preview = _tool switch
        {
            PaintTool.Line => new LineShape(e.Location, _color, _width),
            PaintTool.Rect => new RectShape(e.Location, _color, _width),
            PaintTool.Ellipse => new EllipseShape(e.Location, _color, _width),
            PaintTool.Pencil => new PencilShape(e.Location, _color, _width),
            _ => null,
        };

        _canvas.Preview = _preview;
        _canvas.Invalidate();
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_preview is not null)
        {
            _preview.MoveEnd(e.Location);
            _canvas.Invalidate();
            return;
        }

        if (_draggedShape is null)
        {
            return;
        }

        var dx = e.X - _lastPoint.X;
        var dy = e.Y - _lastPoint.Y;
        if (dx == 0 && dy == 0)
        {
            return;
        }

        _draggedShape.Translate(dx, dy);
        _lastPoint = e.Location;
        _moveDx += dx;
        _moveDy += dy;
        _canvas.Invalidate();
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_preview is not null)
        {
            var shape = _preview;
            _preview = null;
            _canvas.Preview = null;

            if (IsTooSmall(shape))
            {
                _canvas.Invalidate();
                UpdateStatus("Фигура слишком маленькая — не добавлена");
                return;
            }

            _history.Execute(new AddShapeCommand(_drawing, shape));
            _canvas.Invalidate();
            UpdateStatus();
            return;
        }

        if (_draggedShape is null)
        {
            return;
        }

        var moved = _draggedShape;
        var dx = _moveDx;
        var dy = _moveDy;
        _draggedShape = null;
        _moveDx = 0;
        _moveDy = 0;

        if (dx != 0 || dy != 0)
        {
            // Возвращаем фигуру на место и проводим перемещение командой,
            // чтобы Ctrl+Z отменял его так же, как остальные действия.
            moved.Translate(-dx, -dy);
            _history.Execute(new MoveShapeCommand(moved, dx, dy));
            UpdateStatus($"Перемещено на {dx}; {dy}");
        }

        _canvas.Invalidate();
    }

    private void SelectAt(Point point)
    {
        var hit = _drawing.HitTest(point);
        _drawing.Select(hit);

        if (hit is null)
        {
            _draggedShape = null;
            UpdateStatus("Под курсором нет фигуры");
        }
        else
        {
            _draggedShape = hit;
            _lastPoint = point;
            _moveDx = 0;
            _moveDy = 0;
            UpdateStatus($"Выбрана фигура: {hit.Kind}");
        }

        _canvas.Invalidate();
    }

    private static bool IsTooSmall(Shape shape) =>
        shape is not PencilShape &&
        Math.Abs(shape.End.X - shape.Start.X) < 3 &&
        Math.Abs(shape.End.Y - shape.Start.Y) < 3;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 1–5 — быстрый выбор инструмента.
        if (keyData is >= Keys.D1 and <= Keys.D5)
        {
            SetTool((PaintTool)(keyData - Keys.D1));
            return true;
        }

        if (keyData == (Keys.Control | Keys.Z))
        {
            Undo();
            return true;
        }

        if (keyData == (Keys.Control | Keys.Y))
        {
            Redo();
            return true;
        }

        if (keyData == (Keys.Control | Keys.S))
        {
            SaveDrawing();
            return true;
        }

        if (keyData == (Keys.Control | Keys.O))
        {
            OpenDrawing();
            return true;
        }

        if (keyData == Keys.Delete)
        {
            DeleteSelected();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            _preview = null;
            _canvas.Preview = null;
            _drawing.Select(null);
            _canvas.Invalidate();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void Undo()
    {
        var title = _history.Undo();
        _canvas.Invalidate();
        UpdateStatus(title is null ? "Отменять нечего" : $"Отменено: {title}");
    }

    private void Redo()
    {
        var title = _history.Redo();
        _canvas.Invalidate();
        UpdateStatus(title is null ? "Повторять нечего" : $"Повторено: {title}");
    }

    private void DeleteSelected()
    {
        var shape = _drawing.Selected;
        if (shape is null)
        {
            Info("Сначала выберите фигуру инструментом «Выбор».");
            return;
        }

        _history.Execute(new DeleteShapeCommand(_drawing, shape));
        _canvas.Invalidate();
        UpdateStatus($"Удалено: {shape.Kind}");
    }

    private void ClearCanvas()
    {
        if (_drawing.Count == 0)
        {
            Info("Холст уже пуст.");
            return;
        }

        _history.Execute(new ClearDrawingCommand(_drawing));
        _canvas.Invalidate();
        UpdateStatus("Холст очищен (Ctrl+Z вернёт фигуры)");
    }

    private void SaveDrawing()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Сохранить рисунок",
            Filter = "Рисунок (*.paint.json)|*.paint.json|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            DefaultExt = "paint.json",
            AddExtension = true,
            FileName = _currentPath is null ? "рисунок.paint.json" : Path.GetFileName(_currentPath),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _drawing.Save(dialog.FileName);
            _currentPath = dialog.FileName;
            UpdateTitle();
            UpdateStatus($"Сохранено: {Path.GetFileName(_currentPath)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Error("Не удалось сохранить рисунок", ex);
        }
    }

    private void OpenDrawing()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Открыть рисунок",
            Filter = "Рисунок (*.paint.json)|*.paint.json|JSON (*.json)|*.json|Все файлы (*.*)|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (TryLoad(dialog.FileName))
        {
            UpdateTitle();
            UpdateStatus($"Открыто: {Path.GetFileName(_currentPath)}");
        }
    }

    /// <summary>Загрузить рисунок; ошибку показывает окном, но не падает.</summary>
    private bool TryLoad(string path)
    {
        try
        {
            _drawing.Load(path);
            _history.Clear();
            _currentPath = path;
            _canvas.Invalidate();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            Error("Не удалось открыть рисунок", ex);
            return false;
        }
    }

    private void ExportPng()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Выгрузить в PNG",
            Filter = "PNG (*.png)|*.png",
            DefaultExt = "png",
            AddExtension = true,
            FileName = "рисунок.png",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var size = new Size(Math.Max(1, _canvas.ClientSize.Width), Math.Max(1, _canvas.ClientSize.Height));
            _drawing.ExportPng(dialog.FileName, size, _canvas.BackColor);
            UpdateStatus($"PNG сохранён: {Path.GetFileName(dialog.FileName)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Error("Не удалось выгрузить PNG", ex);
        }
    }

    private void UpdateTitle()
    {
        var name = _currentPath is null ? "без имени" : Path.GetFileName(_currentPath);
        Text = $"Графический редактор — лабораторная 2 — {name}";
    }

    private void UpdateStatus(string? message = null)
    {
        _countLabel.Text = $"Фигур: {_drawing.Count}   Выбрано: {(_drawing.Selected is null ? "—" : _drawing.Selected.Kind)}";
        _undoButton.Enabled = _history.CanUndo;
        _redoButton.Enabled = _history.CanRedo;

        var toolName = _tool switch
        {
            PaintTool.Line => "линия",
            PaintTool.Rect => "прямоугольник",
            PaintTool.Ellipse => "эллипс",
            PaintTool.Pencil => "карандаш",
            _ => "выбор",
        };

        _hintLabel.Text = message ?? $"Инструмент: {toolName}. Цвет: {_color.Name}, толщина {_width} px. Ctrl+Z — отменить, Ctrl+Y — повторить, Delete — удалить.";
    }

    private void Info(string message) => MessageBox.Show(this, message, "Подсказка", MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void Error(string title, Exception ex) =>
        MessageBox.Show(this, $"{title}.\n\n{ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _colorButton.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
