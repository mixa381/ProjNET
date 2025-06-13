using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
namespace ProjNET
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }
        public string GenerateApiUrl()
        {
            // Uzmi izabrane vrednosti iz ListBox-a
            string baseCurrency = comboBox1.SelectedItem?.ToString();
            string targetCurrency = comboBox2.SelectedItem?.ToString();
            // Dobijanje datuma za start i end
            DateTime today = DateTime.Today;
            DateTime startDate = today.AddMonths(-6);  // Start je 6 meseci unazad
            string startDateStr = startDate.ToString("yyyy-MM-dd");
            string endDateStr = today.ToString("yyyy-MM-dd");  // End je danas
            
            // Formiranje URL-a
            string url = $"https://api.fxratesapi.com/timeseries?start_date={startDateStr}&end_date={endDateStr}&currencies={targetCurrency}&base={baseCurrency}&places=5";

            return url;
        }
        private async Task<Dictionary<string, decimal>> GetLast6MonthsExchangeRatesAsync()
        {
            string url = GenerateApiUrl();
            using (var client = new HttpClient())
            {
                try
                {

                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();

                    string json = await response.Content.ReadAsStringAsync();
                    JObject data = JObject.Parse(json);

                    var rates = data["rates"] as JObject;
                    if (rates == null || rates.Count == 0)
                        return null;

                    // Parsiranje i sortiranje datuma
                    var parsedRates = new Dictionary<string, decimal>();

                    foreach (var item in rates.Properties())
                    {
                        string date = item.Name;
                        var rateToken = item.Value[comboBox2.SelectedItem?.ToString()];
                        if (rateToken != null)
                        {
                            parsedRates[date] = rateToken.Value<decimal>();
                        }
                    }
                    DateTime sixMonthsAgo = DateTime.Today.AddMonths(-6);

                    var last6Months = parsedRates
                        .Where(kvp => DateTime.Parse(kvp.Key) >= sixMonthsAgo)
                        .OrderBy(kvp => DateTime.Parse(kvp.Key))
                        .Reverse()
                        .ToDictionary(k => k.Key, v => v.Value);

                    return last6Months;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }


        private void AdjustChartZoom()
        {
            // Dobijanje svih X i Y vrednosti sa tačaka
            var xValues = chart1.Series[0].Points.Select(p => p.XValue).ToList();
            var yValues = chart1.Series[0].Points.Select(p => p.YValues[0]).ToList();

            // Ako nema podataka, ništa ne radimo
            if (xValues.Count == 0 || yValues.Count == 0)
                return;

            double xMin = xValues.Min();
            double xMax = xValues.Max();

            DateTime minDate = DateTime.FromOADate(xMin);
            DateTime maxDate = DateTime.FromOADate(xMax);

            DateTime sixMonthsAgo = DateTime.Today.AddMonths(-6);

            if (minDate > sixMonthsAgo)
            {
                minDate = sixMonthsAgo;
            }

            double yMin = yValues.Min();
            double yMax = yValues.Max();
            double yRange = yMax - yMin;
            double yMargin = yRange * 0.05;

            
            chart1.ChartAreas[0].AxisY.Minimum = yMin - yMargin;
            chart1.ChartAreas[0].AxisY.Maximum = yMax + yMargin;

            chart1.ChartAreas[0].AxisX.Minimum = sixMonthsAgo.ToOADate(); 

            
            chart1.ChartAreas[0].AxisX.IntervalType = DateTimeIntervalType.Days;
            chart1.ChartAreas[0].AxisX.Interval = 30;

            // Prilagodite format prikaza na X osi
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd.MM.yy";
            chart1.ChartAreas[0].AxisX.MajorGrid.Enabled = false;
            chart1.ChartAreas[0].AxisY.MajorGrid.Enabled = false;

            // Osvežimo graf da se nove postavke primene
            chart1.Invalidate();
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            label1.Text = "Loading...";
            
            var rates = await GetLast6MonthsExchangeRatesAsync();
            if (rates == null || rates.Count == 0)
            {
                label1.Text = "Greska pri ucitavanju.";
                return;
            }

            // Očistite prethodne podatke sa grafa
            chart1.Series.Clear();
            chart1.ChartAreas.Clear();
            chart1.ChartAreas.Add(new ChartArea("MainArea"));

            // Dodavanje novih podataka u seriju
            var series = new Series("USD to RSD")
            {
                ChartType = SeriesChartType.Line,
                BorderWidth = 3
            };
            int dayCounter = 0;
            
            foreach (var kvp in rates)
            {
                DateTime date = DateTime.Parse(kvp.Key);

                // Dodajemo samo svaku 5. tačku
                if (dayCounter % 5 == 0)
                {
                    series.Points.AddXY(date, kvp.Value);
                }

                dayCounter++;
            }

            chart1.Series.Add(series);

            AdjustChartZoom(); // Automatsko prilagođavanje

            // Postavite format za X osu
            chart1.ChartAreas[0].AxisX.LabelStyle.Format = "dd.MM.yyyy";
            chart1.ChartAreas[0].AxisX.LabelStyle.ForeColor = Color.Gray;
            chart1.ChartAreas[0].AxisY.LabelStyle.ForeColor =   Color.Gray;

            if (series.Points.Count > 0)
            {
                double lastYValue = series.Points.First().YValues[0];
                label1.Text = $"1{comboBox1.SelectedItem.ToString()} = {lastYValue:F4}{comboBox2.SelectedItem.ToString()}";
            }
            
        }

        private void chart1_MouseMove(object sender, MouseEventArgs e)
        {
            // Detekcija da li je mišem prešao preko tačke na grafu
            HitTestResult result = chart1.HitTest(e.X, e.Y);

            // Ako je korisnik prešao preko serije podataka (DataPoint), prikaži tooltip
            if (result.ChartElementType == ChartElementType.DataPoint)
            {
                var dataPoint = chart1.Series[0].Points[result.PointIndex];
                double pointXValue = dataPoint.XValue;

                DateTime pointDate = DateTime.FromOADate(pointXValue);
                double pointValue = dataPoint.YValues[0];

                toolTip1.SetToolTip(chart1, $"Datum: {pointDate.ToShortDateString()} - Vrednost: {pointValue:F2}");

                // Dodaj tačku na graf na mestu gde je pokazivač
                AddDotOnChart(result.ChartElementType, result.PointIndex);
            }
        }

        private void AddDotOnChart(ChartElementType chartElementType, int pointIndex)
        {
            // Ako su podaci u grafu i postoji serija, dodajemo marker samo na tački
            if (chartElementType == ChartElementType.DataPoint)
            {
                // Ukloni prethodnu marker seriju ako postoji
                if (chart1.Series.IndexOf("Marker") >= 0)
                {
                    chart1.Series.Remove(chart1.Series["Marker"]);
                }

                var markerSeries = chart1.Series.Add("Marker");
                markerSeries.ChartType = SeriesChartType.Point;
                markerSeries.MarkerStyle = MarkerStyle.Circle; 
                markerSeries.MarkerSize = 10;
                markerSeries.Color = Color.FromArgb(150, 250, 150);

                // Dodajemo marker na tačno poziciju DataPoint-a
                var dataPoint = chart1.Series[0].Points[pointIndex];
                markerSeries.Points.AddXY(dataPoint.XValue, dataPoint.YValues[0]);
            }
        }
    }
}
