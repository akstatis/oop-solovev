using System.Drawing;
using System.Drawing.Drawing2D;

namespace PaintApp.Core.Checks;

/// <summary>
/// Проверки модели без окна: фигуры, рисунок, команды отмены, файл, PNG.
/// Запуск: dotnet run --project tests/CoreChecks
/// </summary>
internal static class Program
{
    private static int _passed;
    private static int _failed;

    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        CheckShapeBasics();
        CheckContains();
        CheckPencil();
        CheckDrawing();
        CheckCommands();
        CheckFile();
        CheckBadFiles();
        CheckPixels();

        Console.WriteLine();
        Console.WriteLine($"Пройдено: {_passed}, провалено: {_failed}");
        return _failed == 0 ? 0 : 1;
    }

    private static void CheckShapeBasics()
    {
        Section("Фигуры: толщина, границы, сдвиг");

        var color = Color.Red;
        Throws<ArgumentOutOfRangeException>(() => new LineShape(new Point(0, 0), color, 0), "толщина 0 отклонена");
        Throws<ArgumentOutOfRangeException>(() => new RectShape(new Point(0, 0), color, 65), "толщина 65 отклонена");

        var line = new LineShape(new Point(10, 20), color, 3);
        line.MoveEnd(new Point(4, 8));
        Equal(new Rectangle(4, 8, 6, 12), line.Bounds, "линия тянется в левый верхний угол");
        Equal("Line", line.Kind, "тип линии для файла");

        var rect = new RectShape(new Point(100, 100), color, 4);
        rect.MoveEnd(new Point(40, 60));
        Equal(new Rectangle(40, 60, 60, 40), rect.Bounds, "прямоугольник тянется в любую сторону");
        Equal("Rect", rect.Kind, "тип прямоугольника");

        var ellipse = new EllipseShape(new Point(50, 50), color, 2);
        ellipse.MoveEnd(new Point(150, 120));
        Equal(new Rectangle(50, 50, 100, 70), ellipse.Bounds, "эллипс вписан в прямоугольник");
        Equal("Ellipse", ellipse.Kind, "тип эллипса");

        var before = line.Start;
        var endBefore = line.End;
        line.Translate(5, -7);
        Equal(new Point(before.X + 5, before.Y - 7), line.Start, "сдвиг меняет начало");
        Equal(new Point(endBefore.X + 5, endBefore.Y - 7), line.End, "сдвиг меняет и конец");
    }

    private static void CheckContains()
    {
        Section("Попадание мышью: Contains");

        var line = new LineShape(new Point(10, 10), Color.Black, 3);
        line.MoveEnd(new Point(110, 10));
        True(line.Contains(new Point(60, 10)), "точка на линии");
        True(line.Contains(new Point(60, 14)), "точка рядом с линией");
        False(line.Contains(new Point(60, 40)), "точка далеко от линии");

        var diagonal = new LineShape(new Point(0, 0), Color.Black, 1);
        diagonal.MoveEnd(new Point(100, 100));
        True(diagonal.Contains(new Point(50, 52)), "точка рядом с наклонной линией");
        False(diagonal.Contains(new Point(10, 90)), "точка в стороне от наклонной");

        var rect = new RectShape(new Point(20, 20), Color.Black, 2);
        rect.MoveEnd(new Point(120, 90));
        True(rect.Contains(new Point(70, 50)), "точка внутри прямоугольника");
        False(rect.Contains(new Point(0, 0)), "точка вне прямоугольника");

        var ellipse = new EllipseShape(new Point(0, 0), Color.Black, 2);
        ellipse.MoveEnd(new Point(100, 100));
        True(ellipse.Contains(new Point(50, 50)), "центр эллипса");
        False(ellipse.Contains(new Point(4, 4)), "угол описанного прямоугольника вне эллипса");
    }

    private static void CheckPencil()
    {
        Section("Карандаш: точки, попадание, сдвиг");

        var pencil = new PencilShape(new Point(0, 0), Color.Blue, 2);
        pencil.MoveEnd(new Point(10, 0));
        pencil.MoveEnd(new Point(20, 10));
        Equal(3, pencil.Points.Count, "три точки после двух движений");
        Equal(new Point(20, 10), pencil.End, "конец кривой — последняя точка");
        Equal(new Rectangle(0, 0, 20, 10), pencil.Bounds, "границы по всем точкам");
        True(pencil.Contains(new Point(5, 1)), "точка рядом с кривой");
        False(pencil.Contains(new Point(5, 40)), "точка далеко от кривой");

        pencil.Translate(10, 10);
        Equal(new Point(10, 10), pencil.Points[0], "сдвиг двигает все точки");
        Equal(new Point(30, 20), pencil.Points[^1], "сдвиг двигает последнюю точку");
        Equal("Pencil", pencil.Kind, "тип карандаша");
    }

    private static void CheckDrawing()
    {
        Section("Рисунок: список фигур, выбор, попадание");

        var drawing = new Drawing();
        var first = new LineShape(new Point(0, 0), Color.Black, 2);
        var second = new RectShape(new Point(0, 0), Color.Green, 2);
        second.MoveEnd(new Point(50, 50));

        drawing.Add(first);
        drawing.Add(second);
        Equal(2, drawing.Count, "две фигуры добавлены");
        Equal(first, drawing.Shapes[0], "порядок сохраняется");

        Equal(second, drawing.HitTest(new Point(25, 25)), "под точкой — верхняя фигура");
        Equal(null, drawing.HitTest(new Point(200, 200)), "пустое место — null");

        drawing.Select(second);
        Equal(second, drawing.Selected, "фигура выбрана");
        drawing.RemoveAt(1);
        Equal(null, drawing.Selected, "удалённая фигура снимается с выбора");
        Equal(1, drawing.Count, "осталась одна фигура");

        drawing.Insert(0, second);
        Equal(second, drawing.Shapes[0], "вставка на место");
        Equal(0, drawing.IndexOf(second), "индекс найден");

        False(drawing.Remove(new EllipseShape(new Point(0, 0), Color.Black, 1)), "чужую фигуру удалить нельзя");
        Throws<ArgumentOutOfRangeException>(() => drawing.Insert(99, first), "вставка за пределы списка отклонена");
        Throws<InvalidOperationException>(() => drawing.Select(new LineShape(new Point(0, 0), Color.Red, 1)), "выбрать чужую фигуру нельзя");

        drawing.Clear();
        Equal(0, drawing.Count, "очистка убирает все фигуры");
    }

    private static void CheckCommands()
    {
        Section("Команды: отмена и повтор");

        var drawing = new Drawing();
        var history = new CommandHistory();
        var line = new LineShape(new Point(0, 0), Color.Black, 2);
        line.MoveEnd(new Point(100, 0));

        history.Execute(new AddShapeCommand(drawing, line));
        Equal(1, drawing.Count, "рисование добавило фигуру");
        True(history.CanUndo, "отмена доступна");
        False(history.CanRedo, "повтор пока недоступен");

        Equal("рисование (Line)", history.Undo(), "отмена вернула описание действия");
        Equal(0, drawing.Count, "после отмены фигуры нет");
        True(history.CanRedo, "повтор доступен");

        history.Redo();
        Equal(1, drawing.Count, "повтор вернул фигуру");

        var second = new EllipseShape(new Point(10, 10), Color.Blue, 3);
        history.Execute(new AddShapeCommand(drawing, second));
        Equal(2, drawing.Count, "вторая фигура добавлена");

        history.Execute(new DeleteShapeCommand(drawing, line));
        Equal(1, drawing.Count, "удаление убрало фигуру");
        history.Undo();
        Equal(2, drawing.Count, "отмена удаления вернула фигуру");
        Equal(line, drawing.Shapes[0], "фигура вернулась на своё место");

        history.Execute(new ClearDrawingCommand(drawing));
        Equal(0, drawing.Count, "очистка убрала всё");
        history.Undo();
        Equal(2, drawing.Count, "отмена очистки вернула обе фигуры");
        Equal(line, drawing.Shapes[0], "порядок после отмены очистки");

        var beforeX = drawing.Shapes[0].Start.X;
        history.Execute(new MoveShapeCommand(drawing.Shapes[0], 30, 0));
        Equal(beforeX + 30, drawing.Shapes[0].Start.X, "перемещение сдвинуло фигуру");
        history.Undo();
        Equal(beforeX, drawing.Shapes[0].Start.X, "отмена перемещения вернула фигуру");

        var fresh = new CommandHistory();
        Equal(null, fresh.Undo(), "отменять нечего");
        Equal(null, fresh.Redo(), "повторять нечего");

        history.Execute(new AddShapeCommand(drawing, new RectShape(new Point(0, 0), Color.Red, 1)));
        False(history.CanRedo, "новое действие сбрасывает повтор");
    }

    private static void CheckFile()
    {
        Section("Файл: сохранение и загрузка JSON");

        var directory = Path.Combine(Path.GetTempPath(), "paint-checks-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "picture.paint.json");

        try
        {
            var drawing = new Drawing();
            var line = new LineShape(new Point(5, 6), Color.FromArgb(255, 10, 20, 30), 3);
            line.MoveEnd(new Point(70, 80));
            var rect = new RectShape(new Point(100, 100), Color.FromArgb(255, 200, 0, 0), 5);
            rect.MoveEnd(new Point(160, 140));
            var ellipse = new EllipseShape(new Point(20, 200), Color.FromArgb(255, 0, 150, 0), 1);
            ellipse.MoveEnd(new Point(120, 260));
            var pencil = new PencilShape(new Point(200, 200), Color.FromArgb(255, 0, 0, 200), 2);
            pencil.MoveEnd(new Point(210, 210));
            pencil.MoveEnd(new Point(220, 200));

            drawing.Add(line);
            drawing.Add(rect);
            drawing.Add(ellipse);
            drawing.Add(pencil);
            drawing.Save(path);

            True(File.Exists(path), "файл создан");
            var text = File.ReadAllText(path);
            True(text.Contains("\"Line\"") && text.Contains("\"Pencil\""), "в файле записан тип фигур");
            True(text.Contains("#FF0A141E"), "цвет записан как #AARRGGBB");

            var loaded = new Drawing();
            loaded.Load(path);
            Equal(4, loaded.Count, "загружены все четыре фигуры");
            Equal("Line", loaded.Shapes[0].Kind, "тип первой фигуры");
            Equal(3, loaded.Shapes[0].Width, "толщина линии");
            Equal(Color.FromArgb(255, 10, 20, 30).ToArgb(), loaded.Shapes[0].Color.ToArgb(), "цвет линии");
            Equal(new Point(70, 80), loaded.Shapes[0].End, "вторая точка линии");
            Equal("Rect", loaded.Shapes[1].Kind, "тип второй фигуры");
            Equal(new Rectangle(100, 100, 60, 40), loaded.Shapes[1].Bounds, "прямоугольник восстановлен");
            Equal("Ellipse", loaded.Shapes[2].Kind, "тип третьей фигуры");
            Equal("Pencil", loaded.Shapes[3].Kind, "тип четвёртой фигуры");
            var pencilLoaded = (PencilShape)loaded.Shapes[3];
            Equal(3, pencilLoaded.Points.Count, "точки карандаша восстановлены");
            Equal(new Point(220, 200), pencilLoaded.Points[^1], "последняя точка карандаша");

            var missing = new Drawing();
            Throws<FileNotFoundException>(() => missing.Load(Path.Combine(directory, "no-such.json")), "нет файла — понятная ошибка");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static void CheckBadFiles()
    {
        Section("Файл: повреждённые данные");

        var directory = Path.Combine(Path.GetTempPath(), "paint-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var broken = Path.Combine(directory, "broken.json");
            File.WriteAllText(broken, "{ это не json");
            Throws<InvalidDataException>(() => new Drawing().Load(broken), "битый JSON отклонён");

            var unknown = Path.Combine(directory, "unknown.json");
            File.WriteAllText(unknown, """
                { "Version": 1, "Shapes": [
                  { "Type": "Triangle", "Color": "#FF000000", "Width": 2, "X1": 0, "Y1": 0, "X2": 10, "Y2": 10 } ] }
                """);
            Throws<InvalidDataException>(() => new Drawing().Load(unknown), "неизвестный тип фигуры отклонён");

            var badWidth = Path.Combine(directory, "width.json");
            File.WriteAllText(badWidth, """
                { "Version": 1, "Shapes": [
                  { "Type": "Line", "Color": "#FF000000", "Width": 0, "X1": 0, "Y1": 0, "X2": 10, "Y2": 10 } ] }
                """);
            Throws<InvalidDataException>(() => new Drawing().Load(badWidth), "нулевая толщина в файле отклонена");

            var badColor = Path.Combine(directory, "color.json");
            File.WriteAllText(badColor, """
                { "Version": 1, "Shapes": [
                  { "Type": "Line", "Color": "red", "Width": 2, "X1": 0, "Y1": 0, "X2": 10, "Y2": 10 } ] }
                """);
            Throws<InvalidDataException>(() => new Drawing().Load(badColor), "непонятный цвет отклонён");

            var empty = Path.Combine(directory, "empty.json");
            File.WriteAllText(empty, "null");
            Throws<InvalidDataException>(() => new Drawing().Load(empty), "пустой файл отклонён");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void CheckPixels()
    {
        Section("Рисование на картинке и экспорт PNG");

        var size = new Size(200, 150);
        var background = Color.White;

        using var bitmap = new Bitmap(size.Width, size.Height);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(background);

            var rect = new RectShape(new Point(10, 10), Color.Black, 4);
            rect.MoveEnd(new Point(90, 70));
            var line = new LineShape(new Point(120, 20), Color.Red, 3);
            line.MoveEnd(new Point(180, 20));

            var drawing = new Drawing();
            drawing.Add(rect);
            drawing.Add(line);
            drawing.Draw(graphics);
        }

        True(IsColored(bitmap.GetPixel(10, 40), background), "рамка прямоугольника нарисована");
        True(IsColored(bitmap.GetPixel(150, 20), background), "линия нарисована");
        False(IsColored(bitmap.GetPixel(50, 40), background), "внутри прямоугольника чисто");

        var directory = Path.Combine(Path.GetTempPath(), "paint-png-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var png = Path.Combine(directory, "picture.png");
        try
        {
            var drawing = new Drawing();
            var ellipse = new EllipseShape(new Point(20, 20), Color.Blue, 6);
            ellipse.MoveEnd(new Point(120, 100));
            drawing.Add(ellipse);
            drawing.ExportPng(png, new Size(160, 130), background);

            True(File.Exists(png), "PNG создан");
            using var loaded = new Bitmap(png);
            Equal(160, loaded.Width, "ширина картинки");
            Equal(130, loaded.Height, "высота картинки");
            Equal(background.ToArgb(), loaded.GetPixel(0, 0).ToArgb(), "фон картинки");
            var colored = false;
            for (var x = 20; x < 120 && !colored; x++)
            {
                colored = IsColored(loaded.GetPixel(x, 20), background);
            }

            True(colored, "эллипс попал в PNG");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static bool IsColored(Color pixel, Color background) =>
        Math.Abs(pixel.R - background.R) > 40 || Math.Abs(pixel.G - background.G) > 40 || Math.Abs(pixel.B - background.B) > 40;

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(title);
    }

    private static void True(bool condition, string description)
    {
        if (condition)
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        else
        {
            _failed++;
            Console.WriteLine("  FAIL " + description);
        }
    }

    private static void False(bool condition, string description) => True(!condition, description);

    private static void Equal<T>(T expected, T actual, string description)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual))
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        else
        {
            _failed++;
            Console.WriteLine($"  FAIL {description}: ожидалось «{expected}», получено «{actual}»");
        }
    }

    private static void Throws<TException>(Action action, string description)
        where TException : Exception
    {
        try
        {
            action();
            _failed++;
            Console.WriteLine($"  FAIL {description}: исключения {typeof(TException).Name} не было");
        }
        catch (TException)
        {
            _passed++;
            Console.WriteLine("  OK   " + description);
        }
        catch (Exception ex)
        {
            _failed++;
            Console.WriteLine($"  FAIL {description}: ожидалось {typeof(TException).Name}, получено {ex.GetType().Name}");
        }
    }
}
