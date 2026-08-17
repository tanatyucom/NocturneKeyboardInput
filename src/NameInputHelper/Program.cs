using System.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

if (args.Length != 3) return 2;
string configPath = args[0];
string responsePath = args[1];
long.TryParse(args[2], out long gameWindowValue);
var values = LoadConfig(configPath);
ApplicationConfiguration.Initialize();
Application.Run(new NameForm(values.surname, values.given, values.nickname, responsePath, new IntPtr(gameWindowValue)));
return 0;

static (string surname, string given, string nickname) LoadConfig(string path)
{
    string surname = "嘉嶋", given = "尚紀", nickname = "人修羅";
    try
    {
        foreach (string raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            int split = raw.IndexOf('=');
            if (split <= 0) continue;
            string key = raw[..split].Trim();
            string value = raw[(split + 1)..].Trim();
            if (key.Equals("Surname", StringComparison.OrdinalIgnoreCase)) surname = value;
            else if (key.Equals("GivenName", StringComparison.OrdinalIgnoreCase)) given = value;
            else if (key.Equals("Nickname", StringComparison.OrdinalIgnoreCase)) nickname = value;
        }
    }
    catch { }
    return (surname, given, nickname);
}

sealed class NameForm : Form
{
    private readonly TextBox surname;
    private readonly TextBox given;
    private readonly TextBox nickname;
    private readonly string responsePath;
    private readonly IntPtr gameWindow;

    private const int SwRestore = 9;
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);

    public NameForm(string initialSurname, string initialGiven, string initialNickname, string responsePath, IntPtr gameWindow)
    {
        this.responsePath = responsePath;
        this.gameWindow = gameWindow;
        Text = "SMT3HD 名前入力 MOD";
        ClientSize = new System.Drawing.Size(420, 245);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        Font = new System.Drawing.Font("Yu Gothic UI", 11F);
        Controls.Add(new Label { Left = 20, Top = 15, Width = 380, Height = 42, Text = "日本語IMEで完成形を入力してください。\r\n姓・名は各4文字以内、通称は8文字以内です。" });
        Controls.Add(new Label { Left = 25, Top = 72, Width = 75, Text = "姓" });
        Controls.Add(new Label { Left = 25, Top = 112, Width = 75, Text = "名" });
        Controls.Add(new Label { Left = 25, Top = 152, Width = 75, Text = "通称" });
        surname = new TextBox { Left = 105, Top = 68, Width = 285, MaxLength = 4, Text = initialSurname, ImeMode = ImeMode.Hiragana };
        given = new TextBox { Left = 105, Top = 108, Width = 285, MaxLength = 4, Text = initialGiven, ImeMode = ImeMode.Hiragana };
        nickname = new TextBox { Left = 105, Top = 148, Width = 285, MaxLength = 8, Text = initialNickname, ImeMode = ImeMode.Hiragana };
        var cancel = new Button { Left = 105, Top = 195, Width = 85, Height = 35, Text = "取消", DialogResult = DialogResult.Cancel };
        var start = new Button { Left = 200, Top = 195, Width = 190, Height = 35, Text = "自動入力を開始" };
        start.Click += Accept;
        Controls.AddRange(new Control[] { surname, given, nickname, cancel, start });
        CancelButton = cancel;
        AcceptButton = start;
        Shown += (_, _) => { Show(); Activate(); BringToFront(); SetForegroundWindow(Handle); surname.Focus(); surname.SelectAll(); };
        FormClosed += (_, _) => RestoreGame();
    }

    private void Accept(object? sender, EventArgs e)
    {
        string s = surname.Text.Trim(), g = given.Text.Trim(), n = nickname.Text.Trim();
        if (s.Length is < 1 or > 4 || g.Length is < 1 or > 4 || n.Length is < 1 or > 8)
        {
            MessageBox.Show(this, "姓・名は1～4文字、通称は1～8文字で入力してください。", "文字数エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(responsePath)!);
        string temp = responsePath + ".tmp";
        File.WriteAllLines(temp, new[] { s, g, n }, new UTF8Encoding(false));
        File.Move(temp, responsePath, true);
        Close();
    }

    private void RestoreGame()
    {
        if (gameWindow == IntPtr.Zero) return;
        ShowWindowAsync(gameWindow, SwRestore);
        SetForegroundWindow(gameWindow);
    }
}
