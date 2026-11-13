namespace PaintApp.Core;

/// <summary>Действие, которое можно выполнить и отменить (доп. задание ★★).</summary>
public interface ICommand
{
    string Title { get; }

    void Execute();

    void Undo();
}

/// <summary>Рисование фигуры.</summary>
public sealed class AddShapeCommand : ICommand
{
    private readonly Drawing _drawing;
    private readonly Shape _shape;

    public AddShapeCommand(Drawing drawing, Shape shape)
    {
        _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
    }

    public string Title => $"рисование ({_shape.Kind})";

    public void Execute() => _drawing.Add(_shape);

    public void Undo() => _drawing.Remove(_shape);
}

/// <summary>Удаление фигуры — с запоминанием места, чтобы вернуть её туда же.</summary>
public sealed class DeleteShapeCommand : ICommand
{
    private readonly Drawing _drawing;
    private readonly Shape _shape;
    private int _index;

    public DeleteShapeCommand(Drawing drawing, Shape shape)
    {
        _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
        _index = drawing.IndexOf(shape);
        if (_index < 0)
        {
            throw new InvalidOperationException("Этой фигуры нет в рисунке.");
        }
    }

    public string Title => $"удаление ({_shape.Kind})";

    public void Execute()
    {
        var current = _drawing.IndexOf(_shape);
        if (current < 0)
        {
            return;
        }

        _index = current;
        _drawing.RemoveAt(current);
    }

    public void Undo() => _drawing.Insert(Math.Min(_index, _drawing.Count), _shape);
}

/// <summary>Очистка холста — с запоминанием всех фигур.</summary>
public sealed class ClearDrawingCommand : ICommand
{
    private readonly Drawing _drawing;
    private List<Shape> _removed = new();

    public ClearDrawingCommand(Drawing drawing)
    {
        _drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
    }

    public string Title => "очистка холста";

    public void Execute()
    {
        _removed = _drawing.Shapes.ToList();
        _drawing.Clear();
    }

    public void Undo()
    {
        foreach (var shape in _removed)
        {
            _drawing.Add(shape);
        }
    }
}

/// <summary>Перетаскивание фигуры (доп. задание ★).</summary>
public sealed class MoveShapeCommand : ICommand
{
    private readonly Shape _shape;
    private readonly int _dx;
    private readonly int _dy;

    public MoveShapeCommand(Shape shape, int dx, int dy)
    {
        _shape = shape ?? throw new ArgumentNullException(nameof(shape));
        _dx = dx;
        _dy = dy;
    }

    public string Title => "перемещение";

    public void Execute() => _shape.Translate(_dx, _dy);

    public void Undo() => _shape.Translate(-_dx, -_dy);
}

/// <summary>Стопки отмены и повтора: каждое действие — объект с Execute и Undo.</summary>
public sealed class CommandHistory
{
    private readonly Stack<ICommand> _undo = new();
    private readonly Stack<ICommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? LastUndoTitle => CanUndo ? _undo.Peek().Title : null;

    public string? LastRedoTitle => CanRedo ? _redo.Peek().Title : null;

    public void Execute(ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Execute();
        _undo.Push(command);
        _redo.Clear();
    }

    /// <summary>Отменить последнее действие. Возвращает описание действия или null.</summary>
    public string? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }

        var command = _undo.Pop();
        command.Undo();
        _redo.Push(command);
        return command.Title;
    }

    /// <summary>Повторить отменённое действие. Возвращает описание действия или null.</summary>
    public string? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }

        var command = _redo.Pop();
        command.Execute();
        _undo.Push(command);
        return command.Title;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
