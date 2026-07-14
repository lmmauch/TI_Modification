using Microsoft.Data.Analysis;
using ScottPlot.Colormaps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TandImodification
{
    public class TIRun
    {
        public DataFrame OriginalData { get; set; }

        public DataFrame ModifiedData { get; set; }

        public string SweepVariable { get; private set; }

        public string Name { get; private set; }

        private static readonly List<string> sweepAngles = ["Alpha", "Psi", "Phi"];

        public TIRun(string name, DataFrame data)
        {
            Name = name;

            OriginalData = data;
            ModifiedData = data.Clone();

            SweepVariable = sweepAngles.MaxBy(angle =>
            { 
                double[] values = [..OriginalData[angle].Cast<object>().Select(x => Convert.ToDouble(x ?? 0))];
                double avg = values.Average();
                double sumOfSquares = values.Sum(val => (val - avg) * (val - avg));
                return Math.Sqrt(sumOfSquares / values.Length);
            }) ?? "Alpha";
        }
    }
}
