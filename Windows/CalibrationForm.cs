using Vrec;
namespace Vrec.Windows;
internal sealed class CalibrationForm : Form
{
    private readonly PictureBox picture;
    private readonly Bitmap source;
    private Point anchor;
    private Rectangle selection;
    public VisualTemplate? Result { get; private set; }
    public CalibrationForm(Bitmap image, MatchState state)
    {
        source = image; Text = $"Drag a distinctive, stable region for {state}; Enter saves, Escape cancels";
        Width = 1000; Height = 650; KeyPreview = true;
        picture = new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.StretchImage };
        Controls.Add(picture);
        picture.MouseDown += (_, e) => { anchor = e.Location; selection = Rectangle.Empty; };
        picture.MouseMove += (_, e) => {
            if (e.Button != MouseButtons.Left) return;
            selection = Rectangle.FromLTRB(Math.Max(0, Math.Min(anchor.X, e.X)), Math.Max(0, Math.Min(anchor.Y, e.Y)), Math.Min(picture.Width, Math.Max(anchor.X, e.X)), Math.Min(picture.Height, Math.Max(anchor.Y, e.Y))); picture.Invalidate();
        };
        picture.Paint += (_, e) => e.Graphics.DrawRectangle(Pens.Lime, selection);
        KeyDown += (_, e) => {
            if (e.KeyCode == Keys.Escape) Close();
            if (e.KeyCode != Keys.Enter || selection.Width < 32 || selection.Height < 18) return;
            double x = (double)selection.X / picture.Width, y = (double)selection.Y / picture.Height;
            double w = (double)selection.Width / picture.Width, h = (double)selection.Height / picture.Height;
            var pixels = ScreenDetector.Sample(source, x, y, w, h);
            var luminance = Enumerable.Range(0, 576).Select(i => .2126 * pixels[i * 3] + .7152 * pixels[i * 3 + 1] + .0722 * pixels[i * 3 + 2]).ToArray();
            double mean = luminance.Average();
            if (luminance.Average(v => Math.Pow(v - mean, 2)) < 100) { MessageBox.Show("Choose a distinctive region with visible detail, not a flat background."); return; }
            Result = new(state, x, y, w, h, pixels); DialogResult = DialogResult.OK; Close();
        };
    }
}
