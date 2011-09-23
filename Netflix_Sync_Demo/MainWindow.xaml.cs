using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Xml.Linq;

namespace CS_Netflix_WPF_Sync
{
    public partial class MainWindow : Window
    {
        XNamespace xa = "http://www.w3.org/2005/Atom";
        XNamespace xd = "http://schemas.microsoft.com/ado/2007/08/dataservices";
        XNamespace xm = "http://schemas.microsoft.com/ado/2007/08/dataservices/metadata";

        string query = "http://odata.netflix.com/Catalog/Titles?$filter=ReleaseYear eq {0}&$skip={1}&$top={2}&$select=Url,BoxArt";

        class Movie
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public string BoxArtUrl { get; set; }
        }

        CancellationTokenSource cts;

        public MainWindow()
        {
            InitializeComponent();
            textBox.Focus();
        }

        private void searchButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMovies(Int32.Parse(textBox.Text));
            // Timeouts: there isn't a good way to do them in the sync version
        }

        private void textBox1_TextChanged(object sender, TextChangedEventArgs e)
        {
            int year;
            searchButton.IsEnabled = Int32.TryParse(textBox.Text, out year) && year >= 1900 && year <= 2099;
        }

        private void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (cts != null)
            {
                cts.Cancel();
                statusText.Text = "Cancelled";
            }
        }

        void LoadMovies(int year)
        {
            resultsPanel.Children.Clear();
            cts = new CancellationTokenSource();
            statusText.Text = "";
            var pageSize = 10;
            var imageCount = 0;
            try
            {
                while (true)
                {
                    statusText.Text = string.Format("Searching...  {0} Titles", imageCount);
                    // (status text doesn't work because the UI never has a breather to show it)
                    QueryMoviesAsync(year, imageCount, pageSize, cts.Token, movies =>
                    {
                        if (movies.Length == 0) break;
                        DisplayMovies(movies);
                        imageCount += movies.Length;
                    });
                }
                statusText.Text = string.Format("{0} Titles", imageCount);
            }
            catch (TaskCanceledException)
            {
                if (statusText.Text != "Timeout") statusText.Text = "Cancelled";
            }
            cts = null;

        }

        void QueryMoviesAsync(int year, int first, int count, CancellationToken ct, Action<Movie[]> processMovies)
        {
            var client = new WebClient();
            var url = String.Format(query, year, first, count);

            client.DownloadStringAsync(new Uri(url));
            client.DownloadStringCompleted += (sender, e) =>
            {
                var data = e.Result;
                var movies =
                    from entry in XDocument.Parse(data).Descendants(xa + "entry")
                    let properties = entry.Element(xm + "properties")
                    select new Movie
                    {
                        Title = (string)entry.Element(xa + "title"),
                        Url = (string)properties.Element(xd + "Url"),
                        BoxArtUrl = (string)properties.Element(xd + "BoxArt").Element(xd + "LargeUrl")
                    };
                processMovies(movies.ToArray());
            };
        }

        void DisplayMovies(Movie[] movies)
        {
            foreach (var movie in movies)
            {
                var bitmap = new BitmapImage(new Uri(movie.BoxArtUrl));
                var image = new Image();
                image.Source = bitmap;
                image.Width = 110;
                image.Height = 150;
                image.Margin = new Thickness(5);
                var tt = new ToolTip();
                tt.Content = movie.Title;
                image.ToolTip = tt;
                var url = movie.Url;
                image.MouseDown += (sender, e) => System.Diagnostics.Process.Start(url);
                resultsPanel.Children.Add(image);
            }
        }

    }
}
