using Microsoft.Data.Analysis;

namespace TandImodification
{
    public class TIRun
    {
        // The loads plotted, in subplot order (2 rows x 3 columns).
        public static readonly string[] Loads = ["CL", "CD", "CSF", "CPM", "CRM", "CYM"];

        private static readonly string[] SweepAngles = ["Alpha", "Psi", "Phi"];

        public DataFrame OriginalData { get; }

        public string SweepVariable { get; }

        public string Name { get; }

        // Row indices (into OriginalData) the user has marked for exclusion.
        public HashSet<int> ExcludedRows { get; } = [];

        public TIRun(string name, DataFrame data)
        {
            Name = name;
            OriginalData = data;

            // The sweep variable is whichever angle actually varies the most.
            SweepVariable = SweepAngles.MaxBy(angle =>
            {
                double[] values = Column(angle);
                double avg = values.Average();
                double sumOfSquares = values.Sum(v => (v - avg) * (v - avg));
                return Math.Sqrt(sumOfSquares / values.Length);
            }) ?? "Alpha";
        }

        public int RowCount => (int)OriginalData.Rows.Count;

        // Reads a column as doubles in row order (index j == row j).
        public double[] Column(string name) =>
            [.. OriginalData[name].Cast<object>().Select(x => Convert.ToDouble(x ?? 0))];

        // The data that will actually be saved: original rows minus the excluded ones.
        public DataFrame GetKeptData()
        {
            var keep = new BooleanDataFrameColumn("keep", RowCount);
            for (int i = 0; i < RowCount; i++)
                keep[i] = !ExcludedRows.Contains(i);

            return OriginalData.Filter(keep);
        }
    }
}
