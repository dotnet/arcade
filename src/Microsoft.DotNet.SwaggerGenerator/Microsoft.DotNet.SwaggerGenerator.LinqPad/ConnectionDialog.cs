using System.Drawing;
using System.Windows.Forms;

namespace Microsoft.DotNet.SwaggerGenerator.LinqPad
{
    public class ConnectionDialog : Form
    {
        private TextBox inputTextBox;
        private Button submitButton;

        public ConnectionDialog(SwaggerProperties properties)
        {
            Properties = properties;
            InitializeComponent();
            submitButton.Click += (sender, e) =>
            {
                Properties.Uri = inputTextBox.Text;
                DialogResult = DialogResult.OK;
                Close();
            };
        }

        public SwaggerProperties Properties { get; }

        private void InitializeComponent()
        {
            var flowLayoutPanel1 = new FlowLayoutPanel();
            inputTextBox = new TextBox();
            submitButton = new Button();
            var label1 = new Label();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Controls.Add(label1);
            flowLayoutPanel1.Controls.Add(inputTextBox);
            flowLayoutPanel1.Controls.Add(submitButton);
            flowLayoutPanel1.Dock = DockStyle.Fill;
            flowLayoutPanel1.FlowDirection = FlowDirection.TopDown;
            flowLayoutPanel1.Location = new Point(0, 0);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(433, 218);
            flowLayoutPanel1.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(3, 10);
            label1.Margin = new Padding(3, 10, 3, 0);
            label1.Name = "label1";
            label1.Size = new Size(178, 20);
            label1.TabIndex = 0;
            label1.Text = "Swagger Document Uri:";
            // 
            // inputTextBox
            // 
            inputTextBox.Location = new Point(3, 33);
            inputTextBox.Name = "inputTextBox";
            inputTextBox.Size = new Size(400, 26);
            inputTextBox.TabIndex = 1;
            inputTextBox.Text = Properties.Uri;
            // 
            // submitButton
            // 
            submitButton.AutoSize = true;
            submitButton.Location = new Point(3, 65);
            submitButton.Name = "submitButton";
            submitButton.Size = new Size(75, 30);
            submitButton.TabIndex = 2;
            submitButton.Text = "Submit";
            submitButton.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            AcceptButton = submitButton;
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(433, 218);
            StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(flowLayoutPanel1);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            Name = "ConnectionDialog";
            Text = "Connect To Swagger API";
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
        }
    }
}
