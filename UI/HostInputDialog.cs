using System.Drawing;
using System.Windows.Forms;

namespace AWLM.UI
{
    /// <summary>
    /// Minimal modal prompt for typing a new remote host to target. Validates via
    /// RemoteInputParser and records the entry in RemoteHistoryStore on success.
    /// </summary>
    internal static class HostInputDialog
    {
        public static string PromptForHost(IWin32Window owner)
        {
            using (var form = new Form
            {
                Text = "Enter Remote Host",
                Size = new Size(360, 150),
                MinimumSize = new Size(320, 150),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterScreen,
                Icon = SystemIcons.Shield
            })
            {
                var label = new Label
                {
                    Text = "Computer name (NetBIOS or FQDN):",
                    AutoSize = true,
                    Location = new Point(12, 14)
                };

                var inputBox = new TextBox
                {
                    Location = new Point(12, 36),
                    Width = 320,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Font = new Font("Consolas", 10f)
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Location = new Point(176, 74),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(257, 74),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right
                };

                form.Controls.Add(label);
                form.Controls.Add(inputBox);
                form.Controls.Add(okButton);
                form.Controls.Add(cancelButton);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                string result = null;

                okButton.Click += (s, e) =>
                {
                    string machineName = RemoteInputParser.TryParse(inputBox.Text, out string error);
                    if (machineName == null)
                    {
                        MessageBox.Show(form, error, "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        inputBox.Focus();
                        form.DialogResult = DialogResult.None; // keep the dialog open
                        return;
                    }

                    RemoteHistoryStore.Add(RemoteHistoryStore.Load(), machineName);
                    result = machineName;
                };

                return form.ShowDialog(owner) == DialogResult.OK ? result : null;
            }
        }
    }
}
