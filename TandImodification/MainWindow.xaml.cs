using Microsoft.Data.Analysis;
using Microsoft.Win32;
using ScottPlot;
using ScottPlot.Plottables;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static System.Net.Mime.MediaTypeNames;

namespace TandImodification
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        bool mouseDown = false;
        Plot? ClickedPlot = null;
        Rectangle? SelectionRectangle = null; 

        List<TIRun> Runs =[];

        public MainWindow()
        {
            InitializeComponent();
            WpfPlot1.Multiplot.AddPlots(6);
            WpfPlot1.Multiplot.Layout = new ScottPlot.MultiplotLayouts.Grid(rows: 2, columns: 3);
            foreach(var plot in WpfPlot1.Multiplot.Subplots.GetPlots())
            {
                
            }
            WpfPlot1.Refresh();
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog()
            {
                Title = "Select a TI Folder"
            };

            if (openFolderDialog.ShowDialog() == true)
            {
                foreach (var file in Directory.GetFiles(openFolderDialog.FolderName))
                {
                    try
                    {
                        // Load the CSV file
                        var newData = DataFrame.LoadCsv(file);

                        // Add it to the list!!!!
                        Runs.Add(new(Path.GetFileNameWithoutExtension(file), newData));
                        
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error loading file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                RunsComboBox.ItemsSource = Runs;
            }
        }

        private void PlotData(TIRun run)
        {

            string[] loads = ["CL", "CD", "CSF", "CPM", "CRM", "CYM"];

            // add sample data to each subplot
            for (int i = 0; i < WpfPlot1.Multiplot.Subplots.Count; i++)
            {
                Plot plot = WpfPlot1.Multiplot.GetPlot(i);
                plot.Clear();

                plot.Title(loads[i]);
                plot.XLabel(run.SweepVariable);

                double[] xs = run.OriginalData[run.SweepVariable].Cast<object>().Select(x => Convert.ToDouble(x ?? 0))
                        .ToArray();
                double[] ys = run.OriginalData[loads[i]].Cast<object>().Select(x => Convert.ToDouble(x ?? 0))
                        .ToArray();

                plot.Add.Scatter(xs, ys);
                plot.Axes.AutoScale();
            }
        }

        private void RunsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RunsComboBox.SelectedItem is TIRun run)
            {
                PlotData(run);
                WpfPlot1.Refresh();
            }
        }

        private void WpfPlot1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Point p = e.GetPosition(WpfPlot1);
            ScottPlot.Pixel mousePixel = new(p.X * WpfPlot1.DisplayScale, p.Y * WpfPlot1.DisplayScale);
            ScottPlot.Plot? clickedPlot = WpfPlot1.Multiplot.GetPlotAtPixel(mousePixel);

            if (clickedPlot is not null)
            {
                WpfPlot1.UserInputProcessor.Disable();
                ClickedPlot = clickedPlot;
                ScottPlot.Coordinates plotCoordinates = clickedPlot.GetCoordinates(mousePixel);
                mouseDown = true;
                SelectionRectangle = clickedPlot.Add.Rectangle(plotCoordinates.X, plotCoordinates.X, plotCoordinates.Y, plotCoordinates.Y);

                WpfPlot1.Refresh();
            }
        }

        private void WpfPlot1_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mouseDown = false;
            if (ClickedPlot is not null && SelectionRectangle is not null)
            {


                // var selectedPoints = DataPoints.Where(x => MouseSlectionRect.Contains(x));
                if (RunsComboBox.SelectedItem is TIRun run)
                {
                    string[] loads = ["CL", "CD", "CSF", "CPM", "CRM", "CYM"];

                    var index = new List<Plot>(WpfPlot1.Multiplot.GetPlots()).IndexOf(ClickedPlot);

                    var filteredDf = run.OriginalData.Filter(
                        run.OriginalData[run.SweepVariable].ElementwiseGreaterThanOrEqual(SelectionRectangle.X1));
                    filteredDf = filteredDf.Filter(filteredDf[run.SweepVariable].ElementwiseLessThanOrEqual(SelectionRectangle.X2));
                    filteredDf = filteredDf.Filter(filteredDf[loads[index]].ElementwiseGreaterThanOrEqual(SelectionRectangle.Y2));
                    filteredDf = filteredDf.Filter(filteredDf[loads[index]].ElementwiseLessThanOrEqual(SelectionRectangle.Y1));


                    
                    for (int i = 0; i < WpfPlot1.Multiplot.Subplots.Count; i++)
                    {


                        Plot plot = WpfPlot1.Multiplot.GetPlot(i);
                        double[] xs = filteredDf[run.SweepVariable].Cast<object>().Select(x => Convert.ToDouble(x ?? 0)).ToArray();
                        double[] ys = filteredDf[loads[i]].Cast<object>().Select(x => Convert.ToDouble(x ?? 0)).ToArray();

                        for (int j = 0; j < xs.Length; j++)
                        {
                            var newMarker = plot.Add.Marker(xs[j], ys[j]);
                            newMarker.MarkerStyle.Shape = MarkerShape.OpenCircle;
                            newMarker.MarkerStyle.Size = 10;
                            newMarker.MarkerStyle.FillColor = Colors.Red.WithAlpha(.2);
                            newMarker.MarkerStyle.LineColor = Colors.Red;
                            newMarker.MarkerStyle.LineWidth = 1;
                        }
                    }
                }
                // Clear the selection rectangle
                ClickedPlot.Remove(SelectionRectangle);
                ClickedPlot = null;
                SelectionRectangle = null;
            }
            WpfPlot1.UserInputProcessor.Enable();
            WpfPlot1.Refresh();
        }

        private void WpfPlot1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown)
                return;

            if (ClickedPlot is not null && SelectionRectangle is not null)
            {
                Point p = e.GetPosition(WpfPlot1);
                ScottPlot.Pixel mousePixel = new(p.X * WpfPlot1.DisplayScale, p.Y * WpfPlot1.DisplayScale);

                ScottPlot.Coordinates plotCoordinates = ClickedPlot.GetCoordinates(mousePixel);
                SelectionRectangle.X2 = plotCoordinates.X;
                SelectionRectangle.Y2 = plotCoordinates.Y;

                WpfPlot1.Refresh();
            }
        }
    }
}