using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace WebCrawler;

/// <summary>
/// The Find window: a small window that stays open beside the main window, so Find Next can be pressed again and again.
/// It only collects the search text; what a search does is up to <see cref="Searcher"/>, and the text that
/// returns is shown under the text box.
/// </summary>
public partial class FindForm : Form
{
    public FindForm()
    {
        InitializeComponent();
    }

    /// <summary>Runs one search for the given text and returns what to tell the user (for example where it was found).</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<string, string> Searcher { get; set; }

    /// <summary>Shows the window (or brings it forward when it is already open) with the search text selected, ready to type over.</summary>
    public void ShowFind(Form owner)
    {
        if (!Visible)
            Show(owner);
        else
            Activate();

        FocusSearchText();
    }

    private void FocusSearchText()
    {
        textBoxFind.Focus();
        textBoxFind.SelectAll();
    }

    // The first time it opens, it sits at the top right of the main window where it hides little of the list.
    private void FindForm_Load(object sender, EventArgs e)
    {
        if (Owner == null)
            return;

        Rectangle area = Screen.FromControl(Owner).WorkingArea;
        int x = Owner.Right - Width - LogicalToDeviceUnits(30);
        int y = Owner.Top + LogicalToDeviceUnits(110);
        x = Math.Max(area.Left, Math.Min(x, area.Right - Width));
        y = Math.Max(area.Top, Math.Min(y, area.Bottom - Height));
        Location = new Point(x, y);
    }

    private void buttonFindNext_Click(object sender, EventArgs e)
    {
        if (UrlSearch.ParseTerms(textBoxFind.Text).Length == 0)
        {
            labelStatus.Text = "Type the text to find.";
            textBoxFind.Focus();
            return;
        }

        if (Searcher != null)
            labelStatus.Text = Searcher(textBoxFind.Text);
    }

    private void buttonClose_Click(object sender, EventArgs e)
    {
        Hide();
    }

    private void textBoxFind_TextChanged(object sender, EventArgs e)
    {
        labelStatus.Text = "";
    }

    // Closing the window only hides it, so the search text is still there the next time. When the main window
    // closes, this window closes with it (the close reason then is not UserClosing).
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnFormClosing(e);
    }

    // Ctrl+F in this window goes back to the search text.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.F))
        {
            FocusSearchText();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
