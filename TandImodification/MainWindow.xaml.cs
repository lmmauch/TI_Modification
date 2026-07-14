using Microsoft.Data.Analysis;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TandImodification
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool mouseDown = false;
        bool excludeMode = true;        // left-drag excludes, right-drag un-excludes
        Plot? ClickedPlot = null;
        Rectangle? SelectionRectangle = null;

        List<TIRun> Runs = [];

        public MainWindow()
        {
            InitializeComponent();
            WpfPlot1.Multiplot.AddPlots(6);
            WpfPlot1.Multiplot.Layout = new ScottPlot.MultiplotLayouts.Grid(rows: 2, columns: 3);

            for (int i = 0; i < TIRun.Loads.Length; i++)
                StyleDark(WpfPlot1.Multiplot.GetPlot(i));

            ConfigureInteractions();

            // Start the Size slider at the display's own scale so it matches reality.
            ScaleSlider.Value = WpfPlot1.DisplayScale;
            WpfPlot1.Refresh();
        }

        // Applies a dark colour scheme to a single subplot.
        private static void StyleDark(Plot plot)
        {
            plot.FigureBackground.Color = Color.FromHex("#1E1E1E");
            plot.DataBackground.Color = Color.FromHex("#252526");
            plot.Axes.Color(Color.FromHex("#C8C8C8"));
            plot.Grid.MajorLineColor = Color.FromHex("#3A3A3A");
            plot.Legend.BackgroundColor = Color.FromHex("#252526");
            plot.Legend.FontColor = Color.FromHex("#C8C8C8");
            plot.Legend.OutlineColor = Color.FromHex("#3A3A3A");
        }

        // Left/right drag are reserved for exclude/restore (see the mouse handlers),
        // so navigation lives on the middle button and the wheel.
        private void ConfigureInteractions()
        {
            var responses = WpfPlot1.UserInputProcessor.UserActionResponses;
            WpfPlot1.UserInputProcessor.IsEnabled = true;
            responses.Clear();

            // Pan with the middle button.
            responses.Add(new ScottPlot.Interactivity.UserActionResponses.MouseDragPan(
                ScottPlot.Interactivity.StandardMouseButtons.Middle));

            // Zoom with the mouse wheel (Ctrl/Shift lock to one axis).
            responses.Add(new ScottPlot.Interactivity.UserActionResponses.MouseWheelZoom(
                ScottPlot.Interactivity.StandardKeys.Control,
                ScottPlot.Interactivity.StandardKeys.Shift));

            // Middle double-click autoscales every subplot.
            responses.Add(new ScottPlot.Interactivity.UserActionResponses.DoubleClickResponse(
                ScottPlot.Interactivity.StandardMouseButtons.Middle,
                (_, _) => { AutoscaleAll(); WpfPlot1.Refresh(); }));
        }

        private void AutoscaleAll()
        {
            for (int i = 0; i < TIRun.Loads.Length; i++)
                WpfPlot1.Multiplot.GetPlot(i).Axes.AutoScale();
        }

        private TIRun? CurrentRun => RunsComboBox.SelectedItem as TIRun;

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new()
            {
                Title = "Select a TI Folder"
            };

            if (openFolderDialog.ShowDialog() != true)
                return;

            Runs = [];
            foreach (var file in Directory.GetFiles(openFolderDialog.FolderName, "*.csv"))
            {
                try
                {
                    var newData = DataFrame.LoadCsv(file);
                    Runs.Add(new TIRun(Path.GetFileNameWithoutExtension(file), newData));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading {Path.GetFileName(file)}: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            RunsComboBox.ItemsSource = Runs;
            if (Runs.Count > 0)
                RunsComboBox.SelectedIndex = 0;
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (Runs.Count == 0)
            {
                MessageBox.Show("Open a folder first.", "Nothing to save",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenFolderDialog saveFolderDialog = new()
            {
                Title = "Select an output folder for the modified files"
            };

            if (saveFolderDialog.ShowDialog() != true)
                return;

            try
            {
                foreach (var run in Runs)
                {
                    string outPath = Path.Combine(saveFolderDialog.FolderName, run.Name + ".csv");
                    DataFrame.SaveCsv(run.GetKeptData(), outPath, header: true);
                }

                int totalExcluded = Runs.Sum(r => r.ExcludedRows.Count);
                MessageBox.Show($"Saved {Runs.Count} file(s), excluding {totalExcluded} point(s) total.",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving files: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentRun is TIRun run)
            {
                run.ExcludedRows.Clear();
                RedrawCurrentRun();
            }
        }

        private void RunsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RedrawCurrentRun();
        }

        // ----- Run navigation -----

        private void PrevButton_Click(object sender, RoutedEventArgs e) => StepRun(-1);

        private void NextButton_Click(object sender, RoutedEventArgs e) => StepRun(+1);

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.PageUp) { StepRun(-1); e.Handled = true; }
            else if (e.Key == Key.PageDown) { StepRun(+1); e.Handled = true; }
        }

        // Moves the run selection by one, clamped to the ends of the list.
        private void StepRun(int direction)
        {
            if (RunsComboBox.Items.Count == 0)
                return;

            int next = Math.Clamp(RunsComboBox.SelectedIndex + direction, 0, RunsComboBox.Items.Count - 1);
            RunsComboBox.SelectedIndex = next;
        }

        // ----- Plot size -----

        // The slider sets the plots' display scale (bigger = larger labels/markers).
        private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // WpfPlot1 isn't built yet during initial XAML load; guard against it.
            if (WpfPlot1 is null)
                return;

            WpfPlot1.DisplayScale = (float)e.NewValue;
            WpfPlot1.Refresh();
        }

        // Draws every subplot for the current run: the full Original series plus the
        // Edited series (kept points only) overlaid, so the change is visible directly.
        private void RedrawCurrentRun()
        {
            if (CurrentRun is not TIRun run)
                return;

            double[] xs = run.Column(run.SweepVariable);

            for (int i = 0; i < TIRun.Loads.Length; i++)
            {
                Plot plot = WpfPlot1.Multiplot.GetPlot(i);
                plot.Clear();
                plot.Title(TIRun.Loads[i]);
                plot.XLabel(run.SweepVariable);

                double[] ys = run.Column(TIRun.Loads[i]);

                // Original: all rows, drawn underneath.
                var original = plot.Add.Scatter(xs, ys);
                original.Color = Colors.Gray;
                original.MarkerSize = 5;
                original.LegendText = "Original";

                // Edited: kept rows only, prominent, drawn on top.
                var keptX = new List<double>();
                var keptY = new List<double>();
                for (int j = 0; j < xs.Length; j++)
                {
                    if (!run.ExcludedRows.Contains(j))
                    {
                        keptX.Add(xs[j]);
                        keptY.Add(ys[j]);
                    }
                }

                var edited = plot.Add.Scatter(keptX.ToArray(), keptY.ToArray());
                edited.Color = Colors.Blue;
                edited.MarkerSize = 6;
                edited.LegendText = "Edited";

                plot.Legend.FontSize = 14;
                plot.Legend.Alignment = Alignment.LowerRight;
                plot.ShowLegend();

                StyleDark(plot);
                plot.Axes.AutoScale();
            }

            StatusText.Text = $"{run.ExcludedRows.Count} of {run.RowCount} points excluded  " +
                              "(left-drag: exclude, right-drag: restore, middle-drag: pan, " +
                              "wheel: zoom, middle dbl-click: autoscale)";
            WpfPlot1.Refresh();
        }

        private void WpfPlot1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (CurrentRun is null)
                return;

            Point p = e.GetPosition(WpfPlot1);
            ScottPlot.Pixel mousePixel = new(p.X * WpfPlot1.DisplayScale, p.Y * WpfPlot1.DisplayScale);
            Plot? clickedPlot = WpfPlot1.Multiplot.GetPlotAtPixel(mousePixel);

            if (clickedPlot is not null)
            {
                WpfPlot1.UserInputProcessor.Disable();
                ClickedPlot = clickedPlot;
                excludeMode = e.ChangedButton != MouseButton.Right;
                ScottPlot.Coordinates c = clickedPlot.GetCoordinates(mousePixel);
                mouseDown = true;
                SelectionRectangle = clickedPlot.Add.Rectangle(c.X, c.X, c.Y, c.Y);
                WpfPlot1.Refresh();
            }
        }

        private void WpfPlot1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown || ClickedPlot is null || SelectionRectangle is null)
                return;

            Point p = e.GetPosition(WpfPlot1);
            ScottPlot.Pixel mousePixel = new(p.X * WpfPlot1.DisplayScale, p.Y * WpfPlot1.DisplayScale);
            ScottPlot.Coordinates c = ClickedPlot.GetCoordinates(mousePixel);
            SelectionRectangle.X2 = c.X;
            SelectionRectangle.Y2 = c.Y;
            WpfPlot1.Refresh();
        }

        private void WpfPlot1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (ClickedPlot is not null && SelectionRectangle is not null && CurrentRun is TIRun run)
            {
                int plotIndex = new List<Plot>(WpfPlot1.Multiplot.GetPlots()).IndexOf(ClickedPlot);

                // Normalize the drag box (it may have been drawn in any direction).
                double xMin = Math.Min(SelectionRectangle.X1, SelectionRectangle.X2);
                double xMax = Math.Max(SelectionRectangle.X1, SelectionRectangle.X2);
                double yMin = Math.Min(SelectionRectangle.Y1, SelectionRectangle.Y2);
                double yMax = Math.Max(SelectionRectangle.Y1, SelectionRectangle.Y2);

                double[] xs = run.Column(run.SweepVariable);
                double[] ys = run.Column(TIRun.Loads[plotIndex]);

                for (int j = 0; j < xs.Length; j++)
                {
                    if (xs[j] >= xMin && xs[j] <= xMax && ys[j] >= yMin && ys[j] <= yMax)
                    {
                        if (excludeMode)
                            run.ExcludedRows.Add(j);
                        else
                            run.ExcludedRows.Remove(j);
                    }
                }

                ClickedPlot.Remove(SelectionRectangle);
            }

            mouseDown = false;
            ClickedPlot = null;
            SelectionRectangle = null;
            WpfPlot1.UserInputProcessor.Enable();
            RedrawCurrentRun();
        }
    }
}
