namespace Archlab.Desktop;

/// <summary>
/// First launch has no database and therefore no users. The seeder needs one admin to bootstrap;
/// asking for it beats generating a password the app would then have to store somewhere readable.
/// </summary>
internal sealed class FirstRunForm : Form
{
    private readonly TextBox _username;
    private readonly TextBox _password;
    private readonly TextBox _confirm;
    private readonly Label _error;

    public string Username => _username.Text.Trim();
    public string Password => _password.Text;

    public FirstRunForm()
    {
        Text = "ARCHNEXUS — primeiro acesso";
        Icon = AppIcon.Load();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 260);

        var intro = new Label
        {
            Text = "Crie o usuario administrador deste terminal.",
            Location = new Point(16, 16),
            Size = new Size(388, 20)
        };

        _username = AddField("Usuario", 50, out var usernameLabel);
        _password = AddField("Senha", 104, out var passwordLabel);
        _confirm = AddField("Confirmar senha", 158, out var confirmLabel);
        _password.UseSystemPasswordChar = true;
        _confirm.UseSystemPasswordChar = true;

        _error = new Label
        {
            ForeColor = Color.Firebrick,
            Location = new Point(16, 206),
            Size = new Size(280, 36)
        };

        var ok = new Button
        {
            Text = "Criar",
            DialogResult = DialogResult.None,
            Location = new Point(310, 212),
            Size = new Size(94, 30)
        };
        ok.Click += OnConfirm;

        Controls.AddRange([intro, usernameLabel, _username, passwordLabel, _password, confirmLabel, _confirm, _error, ok]);
        AcceptButton = ok;
    }

    private TextBox AddField(string label, int top, out Label labelControl)
    {
        labelControl = new Label { Text = label, Location = new Point(16, top), Size = new Size(388, 18) };
        return new TextBox { Location = new Point(16, top + 20), Size = new Size(388, 26) };
    }

    private void OnConfirm(object? sender, EventArgs e)
    {
        if (Username.Length < 3)
        {
            _error.Text = "O usuario precisa de ao menos 3 caracteres.";
            return;
        }

        // The seeder refuses the known development password outside Development, and this host
        // always runs as Production — catch it here, where the message can still be useful.
        if (_password.Text.Length < 8 || _password.Text == "admin123")
        {
            _error.Text = "A senha precisa de ao menos 8 caracteres e nao pode ser 'admin123'.";
            return;
        }

        if (_password.Text != _confirm.Text)
        {
            _error.Text = "As senhas nao conferem.";
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
