using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;

namespace PaintApp.Core;

/// <summary>
/// Рисунок: хранит список фигур и умеет с ним работать — добавить, убрать,
/// нарисовать, сохранить, загрузить, выгрузить в PNG. Окна не знает.
/// </summary>
public sealed class Drawing
{
    private const int FileVersion = 1;

    private readonly List<Shape> _shapes = new();

    public IReadOnlyList<Shape> Shapes => _shapes;

    public int Count => _shapes.Count;

    /// <summary>Выбранная фигура (доп. задание ★).</summary>
    public Shape? Selected { get; private set; }

    public void Add(Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        _shapes.Add(shape);
    }

    public void Insert(int index, Shape shape)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (index < 0 || index > _shapes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Индекс за пределами списка фигур.");
        }

        _shapes.Insert(index, shape);
    }

    public Shape RemoveAt(int index)
    {
        if (index < 0 || index >= _shapes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Индекс за пределами списка фигур.");
        }

        var shape = _shapes[index];
        _shapes.RemoveAt(index);
        if (ReferenceEquals(Selected, shape))
        {
            Selected = null;
        }

        return shape;
    }

    public int IndexOf(Shape shape) => _shapes.IndexOf(shape);

    public bool Remove(Shape shape)
    {
        var index = _shapes.IndexOf(shape);
        if (index < 0)
        {
            return false;
        }

        RemoveAt(index);
        return true;
    }

    public void Clear()
    {
        _shapes.Clear();
        Selected = null;
    }

    public void Select(Shape? shape)
    {
        if (shape is not null && !_shapes.Contains(shape))
        {
            throw new InvalidOperationException("Этой фигуры нет в рисунке.");
        }

        Selected = shape;
    }

    /// <summary>Самая верхняя фигура под точкой — или null.</summary>
    public Shape? HitTest(Point point)
    {
        for (var i = _shapes.Count - 1; i >= 0; i--)
        {
            if (_shapes[i].Contains(point))
            {
                return _shapes[i];
            }
        }

        return null;
    }

    /// <summary>Один цикл на все фигуры: тип каждой выбирается полиморфно.</summary>
    public void Draw(Graphics graphics)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        foreach (var shape in _shapes)
        {
            shape.Draw(graphics);
        }
    }

    /// <summary>Рамка вокруг выбранной фигуры.</summary>
    public void DrawSelection(Graphics graphics)
    {
        if (Selected is null)
        {
            return;
        }

        var bounds = Selected.Bounds;
        bounds.Inflate(4, 4);
        using var pen = new Pen(Color.FromArgb(64, 120, 220), 1) { DashStyle = DashStyle.Dash };
        graphics.DrawRectangle(pen, bounds);
    }

    /// <summary>Сохранить рисунок в JSON: тип, цвет, толщина и точки каждой фигуры.</summary>
    public void Save(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу.", nameof(path));
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var file = new DrawingFile();
        foreach (var shape in _shapes)
        {
            file.Shapes.Add(new ShapeData
            {
                Type = shape.Kind,
                Color = "#" + shape.Color.ToArgb().ToString("X8", CultureInfo.InvariantCulture),
                Width = shape.Width,
                X1 = shape.Start.X,
                Y1 = shape.Start.Y,
                X2 = shape.End.X,
                Y2 = shape.End.Y,
                Points = shape is PencilShape pencil
                    ? pencil.Points.Select(p => new PointData { X = p.X, Y = p.Y }).ToList()
                    : null,
            });
        }

        var json = JsonSerializer.Serialize(file, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>Загрузить рисунок: по полю Type создаётся объект нужного класса.</summary>
    public void Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу.", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Файл рисунка не найден.", path);
        }

        DrawingFile? file;
        try
        {
            var json = File.ReadAllText(path);
            file = JsonSerializer.Deserialize<DrawingFile>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Файл рисунка повреждён: не удалось прочитать JSON.", ex);
        }

        if (file is null)
        {
            throw new InvalidDataException("Файл рисунка пустой.");
        }

        if (file.Version != FileVersion)
        {
            throw new InvalidDataException($"Неизвестная версия файла рисунка: {file.Version}.");
        }

        var loaded = new List<Shape>();
        foreach (var data in file.Shapes)
        {
            loaded.Add(CreateShape(data));
        }

        _shapes.Clear();
        _shapes.AddRange(loaded);
        Selected = null;
    }

    /// <summary>Выгрузить рисунок в PNG.</summary>
    public void ExportPng(string path, Size size, Color background)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Не указан путь к файлу.", nameof(path));
        }

        if (size.Width <= 0 || size.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size), "Размер картинки должен быть больше нуля.");
        }

        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(background);
            Draw(graphics);
        }

        bitmap.Save(path, ImageFormat.Png);
    }

    private static Shape CreateShape(ShapeData data)
    {
        if (data.Width is < 1 or > 64)
        {
            throw new InvalidDataException($"Недопустимая толщина фигуры: {data.Width}.");
        }

        var color = ParseColor(data.Color);
        var start = new Point(data.X1, data.Y1);
        var end = new Point(data.X2, data.Y2);

        return data.Type switch
        {
            "Line" => Finish(new LineShape(start, color, data.Width), end),
            "Rect" => Finish(new RectShape(start, color, data.Width), end),
            "Ellipse" => Finish(new EllipseShape(start, color, data.Width), end),
            "Pencil" => new PencilShape(
                (data.Points ?? throw new InvalidDataException("У кривой нет точек."))
                    .Select(p => new Point(p.X, p.Y)),
                color,
                data.Width),
            _ => throw new InvalidDataException($"Неизвестный тип фигуры: «{data.Type}»."),
        };
    }

    private static Shape Finish(Shape shape, Point end)
    {
        shape.MoveEnd(end);
        return shape;
    }

    private static Color ParseColor(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !text.StartsWith('#') || text.Length != 9)
        {
            throw new InvalidDataException($"Недопустимый цвет фигуры: «{text}».");
        }

        if (!int.TryParse(text[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            throw new InvalidDataException($"Недопустимый цвет фигуры: «{text}».");
        }

        return Color.FromArgb(argb);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed class DrawingFile
    {
        public int Version { get; set; } = FileVersion;

        public List<ShapeData> Shapes { get; set; } = new();
    }

    private sealed class ShapeData
    {
        public string Type { get; set; } = "";

        public string Color { get; set; } = "#FF000000";

        public int Width { get; set; }

        public int X1 { get; set; }

        public int Y1 { get; set; }

        public int X2 { get; set; }

        public int Y2 { get; set; }

        public List<PointData>? Points { get; set; }
    }

    private sealed class PointData
    {
        public int X { get; set; }

        public int Y { get; set; }
    }
}
