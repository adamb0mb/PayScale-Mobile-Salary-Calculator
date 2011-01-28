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
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            // check the input
            if (String.IsNullOrEmpty(JobTitle.Text))
                return;
            if (String.IsNullOrEmpty(ZipCode.Text) || !Regex.IsMatch(ZipCode.Text,@"^\d{5}$"))
                return;

            // hide the input fields and the search button... to make more room for the suggest jobs results
            InputPanel.Visibility = System.Windows.Visibility.Collapsed;
            
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
        }

        //
        // async callback for getting the user's search
        // This builds the list of possible jobs for the user to enter
        //
        private void getSuggestedJobsCallback(object sender, SimpleQuote.SuggestJobsCompletedEventArgs e)
        {
            if (e.Result.Count > 0)
                SuggestJobList.Visibility = System.Windows.Visibility.Visible;

            foreach (String jobTitle in e.Result)
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
            // POTENTIAL BUG: I'm never actually checking to see if the location webservice returns,
            //                I'm just assuming it too longer for the user to interact w/ the menu
            //                than it did for the location web service to respond.
            m_selectedJob = ((Button)e.OriginalSource).Content.ToString();
            SimpleQuote.SimpleQuoteServiceSoapClient client = new SimpleQuote.SimpleQuoteServiceSoapClient();
            client.GetQuoteCompleted += new EventHandler<SimpleQuote.GetQuoteCompletedEventArgs>(client_GetQuoteCompleted);
            client.GetQuoteAsync(m_selectedJob, m_city, m_state, m_country);
        }

        //
        //  Async callback from when we get a response from the quote
        //
        void client_GetQuoteCompleted(object sender, SimpleQuote.GetQuoteCompletedEventArgs e)
        {
            SuggestJobList.Visibility = System.Windows.Visibility.Collapsed;
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
            m_city = String.Empty;
            m_state = String.Empty;
            m_selectedJob = String.Empty;
        }
    }
}