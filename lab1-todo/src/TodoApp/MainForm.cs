using TodoApp.Core;

namespace TodoApp;

/// <summary>
/// Окно списка дел. Только показывает модель и передаёт ей действия пользователя:
/// вся логика и все проверки живут в <see cref="TodoList"/> и <see cref="TodoItem"/>.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TodoList _list;
    private readonly string _storagePath;

    private readonly TextBox _titleBox = new();
    private readonly ComboBox _priorityBox = new();
    private readonly Button _addButton = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _filterBox = new();
    private readonly ComboBox _sortBox = new();
    private readonly ListView _listView = new();
    private readonly Button _renameButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _toggleButton = new();
    private readonly Button _priorityButton = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _countsLabel = new();
    private readonly ToolStripStatusLabel _fileLabel = new();

    private bool _refreshing;
    private bool _uiReady;
    private Font? _doneFont;
    private Font? _highFont;

    public MainForm(TodoList list, string storagePath)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _storagePath = storagePath ?? throw new ArgumentNullException(nameof(storagePath));

        BuildLayout();
        LoadFromDisk();
        RefreshView();
    }

    private TodoItem? SelectedItem => _listView.SelectedItems.Count == 0
        ? null
        : _listView.SelectedItems[0].Tag as TodoItem;

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _uiReady = true;
    }

    private TodoFilter CurrentFilter =>
        _filterBox.SelectedIndex < 0 ? TodoFilter.All : (TodoFilter)_filterBox.SelectedIndex;

    private TodoSort CurrentSort =>
        _sortBox.SelectedIndex < 0 ? TodoSort.Manual : (TodoSort)_sortBox.SelectedIndex;

    private Priority CurrentPriority =>
        _priorityBox.SelectedIndex < 0 ? Priority.Normal : PriorityNames.All[_priorityBox.SelectedIndex];

    private void BuildLayout()
    {
        Text = "Список дел — лабораторная 1";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 480);
        ClientSize = new Size(880, 560);
        Font = new Font("Segoe UI", 9.75f);
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        root.Controls.Add(BuildInputPanel(), 0, 0);
        root.Controls.Add(BuildList(), 0, 1);
        root.Controls.Add(BuildActionPanel(), 0, 2);

        Controls.Add(root);
        Controls.Add(BuildStatusStrip());
    }

    private Control BuildInputPanel()
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8),
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));

        var addRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0),
        };
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140f));
        addRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120f));

        _titleBox.Dock = DockStyle.Fill;
        _titleBox.PlaceholderText = "Что нужно сделать?";
        _titleBox.Margin = new Padding(0, 2, 12, 4);
        _titleBox.KeyDown += TitleBox_KeyDown;

        _priorityBox.Dock = DockStyle.Fill;
        _priorityBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _priorityBox.Margin = new Padding(0, 2, 12, 4);
        foreach (var priority in PriorityNames.All)
        {
            _priorityBox.Items.Add(PriorityNames.Of(priority));
        }

        _priorityBox.SelectedIndex = 1;

        _addButton.Text = "Добавить";
        _addButton.Dock = DockStyle.Fill;
        _addButton.Margin = new Padding(0, 1, 0, 3);
        _addButton.Click += AddButton_Click;

        var priorityCaption = MakeCaption("Приоритет:");
        priorityCaption.Margin = new Padding(0, 2, 8, 4);

        addRow.Controls.Add(_titleBox, 0, 0);
        addRow.Controls.Add(priorityCaption, 1, 0);
        addRow.Controls.Add(_priorityBox, 2, 0);
        addRow.Controls.Add(_addButton, 3, 0);

        var filterRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Margin = new Padding(0),
        };
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filterRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170f));

        _searchBox.Dock = DockStyle.Fill;
        _searchBox.PlaceholderText = "Поиск по названию";
        _searchBox.Margin = new Padding(0, 2, 12, 0);
        _searchBox.TextChanged += (_, _) => RefreshView();

        var filterCaption = MakeCaption("Показать:");
        filterCaption.Margin = new Padding(0, 2, 8, 0);

        _filterBox.Dock = DockStyle.Fill;
        _filterBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _filterBox.Margin = new Padding(0, 2, 16, 0);
        _filterBox.Items.AddRange(new object[] { "Все задачи", "Только активные", "Только выполненные" });
        _filterBox.SelectedIndex = 0;

        var sortCaption = MakeCaption("Сортировка:");
        sortCaption.Margin = new Padding(0, 2, 8, 0);

        _sortBox.Dock = DockStyle.Fill;
        _sortBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _sortBox.Margin = new Padding(0, 2, 0, 0);
        _sortBox.Items.AddRange(new object[] { "Как добавили", "По названию", "По приоритету", "По статусу" });
        _sortBox.SelectedIndex = 0;

        // Обработчики вешаем после выбора значений: иначе они сработают при сборке окна.
        _filterBox.SelectedIndexChanged += (_, _) => RefreshView();
        _sortBox.SelectedIndexChanged += (_, _) => RefreshView();

        filterRow.Controls.Add(_searchBox, 0, 0);
        filterRow.Controls.Add(filterCaption, 1, 0);
        filterRow.Controls.Add(_filterBox, 2, 0);
        filterRow.Controls.Add(sortCaption, 3, 0);
        filterRow.Controls.Add(_sortBox, 4, 0);

        container.Controls.Add(addRow, 0, 0);
        container.Controls.Add(filterRow, 0, 1);
        return container;
    }

    private Control BuildList()
    {
        _listView.Dock = DockStyle.Fill;
        _listView.View = View.Details;
        _listView.CheckBoxes = true;
        _listView.FullRowSelect = true;
        _listView.GridLines = true;
        _listView.HideSelection = false;
        _listView.MultiSelect = false;
        _listView.ShowItemToolTips = true;
        _listView.Columns.Add("Название", 380);
        _listView.Columns.Add("Приоритет", 120);
        _listView.Columns.Add("Статус", 150);
        _listView.Resize += (_, _) => LayoutColumns();
        _listView.ItemChecked += ListView_ItemChecked;
        _listView.SelectedIndexChanged += (_, _) => UpdateButtons();
        _listView.DoubleClick += (_, _) => RenameSelected();
        _listView.KeyDown += ListView_KeyDown;
        return _listView;
    }

    private Control BuildActionPanel()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 10, 0, 0),
            Margin = new Padding(0),
        };

        ConfigureActionButton(_toggleButton, "Отметить", ToggleSelected_Click);
        ConfigureActionButton(_renameButton, "Переименовать", (_, _) => RenameSelected());
        ConfigureActionButton(_priorityButton, "Приоритет", PriorityButton_Click);
        ConfigureActionButton(_deleteButton, "Удалить", DeleteSelected_Click);
        _deleteButton.ForeColor = Color.FromArgb(164, 32, 32);

        panel.Controls.Add(_toggleButton);
        panel.Controls.Add(_renameButton);
        panel.Controls.Add(_priorityButton);
        panel.Controls.Add(_deleteButton);
        return panel;
    }

    /// <summary>Колонка с названием растягивается, две служебные — фиксированной ширины.</summary>
    private void LayoutColumns()
    {
        if (_listView.Columns.Count < 3 || _listView.ClientSize.Width <= 0)
        {
            return;
        }

        const int priorityWidth = 120;
        const int statusWidth = 140;
        var scrollBar = _listView.Items.Count * 24 > _listView.ClientSize.Height
            ? SystemInformation.VerticalScrollBarWidth
            : 0;

        var forTitle = _listView.ClientSize.Width - scrollBar - priorityWidth - statusWidth - 4;
        _listView.Columns[0].Width = Math.Max(200, forTitle);
        _listView.Columns[1].Width = priorityWidth;
        _listView.Columns[2].Width = statusWidth;
    }

    private static void ConfigureActionButton(Button button, string text, EventHandler onClick)    {
        button.Text = text;
        button.AutoSize = true;
        button.Padding = new Padding(10, 4, 10, 4);
        button.Margin = new Padding(0, 0, 8, 0);
        button.Click += onClick;
    }

    private Control BuildStatusStrip()
    {
        _countsLabel.Text = "";
        _fileLabel.Alignment = ToolStripItemAlignment.Right;
        _fileLabel.Text = "Файл: " + Path.GetFileName(_storagePath);
        _fileLabel.ToolTipText = _storagePath;

        _statusStrip.Items.Add(_countsLabel);
        _statusStrip.Items.Add(new ToolStripStatusLabel { Spring = true });
        _statusStrip.Items.Add(_fileLabel);
        _statusStrip.SizingGrip = false;
        return _statusStrip;
    }

    private static Label MakeCaption(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left,
        TextAlign = ContentAlignment.MiddleLeft,
    };

    private void LoadFromDisk()
    {
        try
        {
            _list.Load(_storagePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(
                this,
                $"Не удалось загрузить задачи, начнём с пустого списка.\n\n{ex.Message}",
                "Загрузка",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void SaveToDisk()
    {
        try
        {
            _list.Save(_storagePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Не удалось сохранить задачи.\n\n{ex.Message}",
                "Сохранение",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void AddButton_Click(object? sender, EventArgs e) => AddFromInput();

    private void TitleBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            AddFromInput();
        }
    }

    private void AddFromInput()
    {
        try
        {
            var item = _list.Add(_titleBox.Text, CurrentPriority);
            _titleBox.Clear();
            SaveToDisk();
            RefreshView(item);
            _titleBox.Focus();
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Нельзя добавить задачу", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _titleBox.Focus();
            _titleBox.SelectAll();
        }
    }

    private void ListView_ItemChecked(object? sender, ItemCheckedEventArgs e)
    {
        // Пока окно не показано, ListView только расставляет галочки по модели —
        // это не действие пользователя, реагировать на него нельзя.
        if (!_uiReady || _refreshing || e.Item.Tag is not TodoItem item)
        {
            return;
        }

        // Список перерисовывается заново, и ListView иногда присылает это событие
        // ещё раз для уже выставленной галочки. Реагируем только на настоящее изменение.
        if (e.Item.Checked == item.IsDone)
        {
            return;
        }

        try
        {
            if (e.Item.Checked)
            {
                _list.Complete(item);
            }
            else
            {
                _list.Reopen(item);
            }

            SaveToDisk();
            BeginInvoke(new Action(() => RefreshView(item)));
        }
        catch (InvalidOperationException ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ToggleSelected_Click(object? sender, EventArgs e)
    {
        var item = RequireSelection();
        if (item is null)
        {
            return;
        }

        if (item.IsDone)
        {
            _list.Reopen(item);
        }
        else
        {
            _list.Complete(item);
        }

        SaveToDisk();
        RefreshView(item);
    }

    private void RenameSelected()
    {
        var item = RequireSelection();
        if (item is null)
        {
            return;
        }

        var newTitle = TextPromptDialog.Ask(this, "Переименовать задачу", "Новое название:", item.Title);
        if (newTitle is null)
        {
            return;
        }

        try
        {
            _list.Rename(item, newTitle);
            SaveToDisk();
            RefreshView(item);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "Нельзя переименовать", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void PriorityButton_Click(object? sender, EventArgs e)
    {
        var item = RequireSelection();
        if (item is null)
        {
            return;
        }

        var menu = new ContextMenuStrip();
        foreach (var priority in PriorityNames.All)
        {
            var entry = new ToolStripMenuItem(PriorityNames.Of(priority))
            {
                Checked = item.Priority == priority,
            };
            var chosen = priority;
            entry.Click += (_, _) =>
            {
                _list.SetPriority(item, chosen);
                SaveToDisk();
                RefreshView(item);
            };
            menu.Items.Add(entry);
        }

        menu.Show(_priorityButton, new Point(0, _priorityButton.Height));
    }

    private void DeleteSelected_Click(object? sender, EventArgs e) => DeleteSelected();

    private void ListView_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2)
        {
            RenameSelected();
            e.Handled = true;
        }
    }

    private void DeleteSelected()
    {
        var item = RequireSelection();
        if (item is null)
        {
            return;
        }

        var answer = MessageBox.Show(
            this,
            $"Удалить задачу «{item.Title}»?",
            "Удаление",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        _list.Remove(item);
        SaveToDisk();
        RefreshView();
    }

    private TodoItem? RequireSelection()
    {
        var item = SelectedItem;
        if (item is null)
        {
            MessageBox.Show(
                this,
                "Сначала выберите задачу в списке.",
                "Ничего не выбрано",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        return item;
    }

    private void RefreshView(TodoItem? select = null)
    {
        var keep = select ?? SelectedItem;

        _refreshing = true;
        _listView.BeginUpdate();
        try
        {
            _listView.Items.Clear();
            foreach (var item in _list.BuildView(CurrentFilter, CurrentSort, _searchBox.Text))
            {
                var row = new ListViewItem(item.Title)
                {
                    Tag = item,
                    Checked = item.IsDone,
                    ToolTipText = $"{item.Title}\nПриоритет: {PriorityNames.Of(item.Priority)}",
                };
                row.SubItems.Add(PriorityNames.Of(item.Priority));
                row.SubItems.Add(item.IsDone ? "выполнено" : "активна");
                StyleRow(row, item);
                _listView.Items.Add(row);

                if (ReferenceEquals(item, keep))
                {
                    row.Selected = true;
                    row.Focused = true;
                }
            }
        }
        finally
        {
            _listView.EndUpdate();
            _refreshing = false;
        }

        UpdateCounts();
        UpdateButtons();
        LayoutColumns();
    }

    private void StyleRow(ListViewItem row, TodoItem item)
    {
        if (item.IsDone)
        {
            row.ForeColor = Color.Gray;
            row.Font = _doneFont ??= new Font(_listView.Font, FontStyle.Strikeout);
        }
        else if (item.Priority == Priority.High)
        {
            row.ForeColor = Color.FromArgb(178, 34, 34);
            row.Font = _highFont ??= new Font(_listView.Font, FontStyle.Bold);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _doneFont?.Dispose();
            _highFont?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateCounts()
    {
        _countsLabel.Text = $"Всего: {_list.Count}    Активных: {_list.ActiveCount}    Выполнено: {_list.DoneCount}";

        var visible = _listView.Items.Count;
        if (visible != _list.Count)
        {
            _countsLabel.Text += $"    Показано: {visible}";
        }
    }

    private void UpdateButtons()
    {        var item = SelectedItem;
        var hasSelection = item is not null;

        _renameButton.Enabled = hasSelection;
        _deleteButton.Enabled = hasSelection;
        _toggleButton.Enabled = hasSelection;
        _priorityButton.Enabled = hasSelection;
        _toggleButton.Text = item is { IsDone: true } ? "Вернуть в работу" : "Отметить выполненной";
    }
}
