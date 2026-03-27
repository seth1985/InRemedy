namespace InRemedy.Desktop;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(18, 20, 23);
        this.ClientSize = new System.Drawing.Size(1440, 900);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Name = "Form1";
    }
}
