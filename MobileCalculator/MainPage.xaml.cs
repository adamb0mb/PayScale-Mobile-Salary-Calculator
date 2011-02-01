using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using System.Threading;

namespace MobileCalculator
{
    public partial class MainPage : PhoneApplicationPage
    {
        private string m_selectedJob = String.Empty;
        private string m_city;
        private string m_state;
        private string m_country;
        // Constructor
        public MainPage()
        {
            InitializeComponent();
        }

        //
        // For when the user hits the search button
        //
        AutoResetEvent m_suggestedJobsWait = new AutoResetEvent(false);
        AutoResetEvent m_zipCodeLookupWait = new AutoResetEvent(false);
        Timer m_timer;
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            // check the input
            if (String.IsNullOrEmpty(JobTitle.Text))
                return;
            if (String.IsNullOrEmpty(ZipCode.Text) || !Regex.IsMatch(ZipCode.Text,@"^\d{5}$"))
                return;

            // hide the input fields and the search button... to make more room for the suggest jobs results
            InputPanel.Visibility = System.Windows.Visibility.Collapsed;
            ContactInfo.Visibility = System.Windows.Visibility.Collapsed;

            // calling to get a listing of job suggestions
            SimpleQuote.SimpleQuoteServiceSoapClient client = new SimpleQuote.SimpleQuoteServiceSoapClient();
            client.SuggestJobsCompleted += new EventHandler<SimpleQuote.SuggestJobsCompletedEventArgs>(getSuggestedJobsCallback);
            client.SuggestJobsAsync(JobTitle.Text);

            // getting the city/state info by zipcode.
            // Using a totally randon zipcode service I found in the internets
            // http://www.webservicex.net/uszip.asmx
            ZipcodeService.USZipSoapClient zipclient = new ZipcodeService.USZipSoapClient();
            zipclient.GetInfoByZIPCompleted += new EventHandler<ZipcodeService.GetInfoByZIPCompletedEventArgs>(getZipcodeCallback);
            zipclient.GetInfoByZIPAsync(ZipCode.Text);

            ShowLoadingScreen();
        }

        void ShowLoadingScreen()
        {
            InputPanel.Visibility = System.Windows.Visibility.Collapsed;
            SuggestJobList.Visibility = System.Windows.Visibility.Collapsed;
        }

        //
        // async callback for getting the zipcode data
        //
        private void getZipcodeCallback(object sender, ZipcodeService.GetInfoByZIPCompletedEventArgs e) 
        {
            // This webservice was returning crap XML (or I didn't know how to get it into the right format or something)
            // so I just regex'd it instead... seems to work
            m_city = Regex.Match(e.Result.ToString(), "<CITY>(.+?)</CITY>").Groups[1].Value;
            m_state = Regex.Match(e.Result.ToString(), "<STATE>(.+?)</STATE>").Groups[1].Value;
            m_country = "United States";
            m_zipCodeLookupWait.Set();
        }

        //
        // async callback for getting the user's search
        //
        string[] m_suggestedJobs;
        private void getSuggestedJobsCallback(object sender, SimpleQuote.SuggestJobsCompletedEventArgs e)
        {
            if (e.Result.Count > 0)
            {
                m_suggestedJobs = e.Result.ToArray();
            }

            m_suggestedJobsWait.Set();
            ShowSuggestedJobsData();
        }

        //
        // This builds the list of possible jobs for the user to enter
        //
        void ShowSuggestedJobsData()
        {
            LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
            SuggestJobList.Visibility = System.Windows.Visibility.Visible;

            if (m_suggestedJobs == null || m_suggestedJobs.Length < 1) // error
            {
                TextBlock webserviceerror = new TextBlock();
                webserviceerror.TextWrapping = TextWrapping.Wrap;
                webserviceerror.Text = "There was an error retreiving the data from the web service";
                SuggestJobList.Children.Insert(SuggestJobList.Children.Count, webserviceerror);
                return;
            }

            foreach (String jobTitle in m_suggestedJobs)
            {
                Button button = new Button();
                button.Name = jobTitle;
                button.Content = jobTitle;
                button.Height = 100;
                button.Click += new RoutedEventHandler(suggestedJob_Click);
                SuggestJobList.Children.Insert(SuggestJobList.Children.Count, button);
            }
        }

        //
        // For when the user selects a job from the list
        //
        void suggestedJob_Click(object sender, RoutedEventArgs e)
        {
            m_selectedJob = ((Button)e.OriginalSource).Content.ToString();

            SimpleQuote.SimpleQuoteServiceSoapClient client = new SimpleQuote.SimpleQuoteServiceSoapClient();
            client.GetQuoteCompleted += new EventHandler<SimpleQuote.GetQuoteCompletedEventArgs>(client_GetQuoteCompleted);
            m_zipCodeLookupWait.WaitOne(); // wait for the location data
            client.GetQuoteAsync(m_selectedJob, m_city, m_state, m_country);

            SuggestJobList.Visibility = System.Windows.Visibility.Collapsed;
            LoadingPanel.Visibility = System.Windows.Visibility.Visible;
        }


        //
        //  Async callback from when we get a response from the quote
        //
        void client_GetQuoteCompleted(object sender, SimpleQuote.GetQuoteCompletedEventArgs e)
        {
            LoadingPanel.Visibility = System.Windows.Visibility.Collapsed;
            ResultsPanel.Visibility = System.Windows.Visibility.Visible;
            
            // HACK: Silverlight ONLY SUPPORTS JPEG images.  The PayScale webservice only returns PNGs.  I found this little script
            //       in the PHP documentation, and ported it to my needs.  hopefully nobody abuses the script (or roots my wedding website)
            string url = "http://www.adamandmckenna.com/tmp/png2jpg.php?img=http://www.payscale.com" + e.Result.ChartUrl;

            // Get the Image
            WebClient webClientImgDownloader = new WebClient();
            webClientImgDownloader.OpenReadCompleted += new OpenReadCompletedEventHandler(webClientImgDownloader_OpenReadCompleted);
            webClientImgDownloader.OpenReadAsync(new Uri(url));
            
            // Formatting the lines of text for display
            TwentyFifth.Text = "25th Percentile: $" + Math.Round(e.Result.Percentile25,2).ToString();
            Fiftieth.Text = "50th Percentile: $" + Math.Round(e.Result.Median,2).ToString();
            SeventyFifth.Text = "75th Percentile: $" + Math.Round(e.Result.Percentile75,2).ToString();
            Location.Text = e.Result.LocationType + ": " + e.Result.LocationRegion;
            Job.Text = "Job: " + m_selectedJob;
        }

        //
        // Async callback method for downloading the chart image
        //
        void webClientImgDownloader_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            // I found this snippet on someone's website.  It seems to work.  I don't know much about the BitmapImage class
            BitmapImage bitmap = new BitmapImage();
            bitmap.SetSource(e.Result);
            ResultImage.Source = bitmap;
        }

        //
        // Puts the app back to it's initial stated.
        //
        private void ResetBtn_Click(object sender, RoutedEventArgs e)
        {
            SuggestJobList.Visibility = System.Windows.Visibility.Collapsed;
            ResultsPanel.Visibility = System.Windows.Visibility.Collapsed;
            InputPanel.Visibility = System.Windows.Visibility.Visible;
            SuggestJobList.Children.Clear();
            TextBlock suggestText = new TextBlock();
            suggestText.Text = "Select a Job";
            SuggestJobList.Children.Add(suggestText);
            m_city = String.Empty;
            m_state = String.Empty;
            m_selectedJob = String.Empty;
        }

        private void MoreDetailsBtn_Click(object sender, RoutedEventArgs e)
        {
            WebBrowserTask wbt = new WebBrowserTask();
            wbt.URL = String.Format("http://www.payscale.com/wizards/choose.aspx?tk=wp7&job={0}&city={1}&state={2}&country={3}", m_selectedJob, m_city, m_state, m_country);
            wbt.URL = wbt.URL.Replace(" ", "+");
            wbt.Show();
        }
    }
}