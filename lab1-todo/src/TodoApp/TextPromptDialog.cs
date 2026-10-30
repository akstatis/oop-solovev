namespace TodoApp;

/// <summary>Маленькое окно «введите строку»: используется для переименования задачи.</summary>
public sealed class TextPromptDialog : Form
{
    private readonly TextBox _input = new();

    private TextPromptDialog(string caption, string label, string initialValue)
    {
        Text = caption;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(420, 130);
        Font = new Font("Segoe UI", 9.75f);
        Padding = new Padding(12);

        var captionLabel = new Label
        {
            Text = label,
            Dock = DockStyle.Top,
            Height = 22,
            AutoSize = false,
        };

        _input.Dock = DockStyle.Top;
        _input.Text = initialValue;
        _input.SelectAll();

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 38,
            Padding = new Padding(0, 6, 0, 0),
        };

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Width = 90,
            Height = 28,
            Margin = new Padding(6, 0, 0, 0),
        };

        var cancelButton = new Button
        {
            Text = "Отмена",
            DialogResult = DialogResult.Cancel,
            Width = 90,
            Height = 28,
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);

        Controls.Add(buttons);
        Controls.Add(_input);
        Controls.Add(captionLabel);

        AcceptButton = okButton;
        CancelButton = cancelButton;
        ActiveControl = _input;
    }

    /// <summary>Спросить строку. Возвращает null, если пользователь отказался.</summary>
    public static string? Ask(IWin32Window owner, string caption, string label, string initialValue)
    {
        using var dialog = new TextPromptDialog(caption, label, initialValue);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._input.Text : null;
    }
}
