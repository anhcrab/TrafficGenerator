using CefSharp;
using CefSharp.Wpf;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web;
using System.Windows;
using System.Windows.Threading;
using Terus_Traffic.Ultilities;
using Terus_Traffic.ViewModel;

namespace Terus_Traffic
{
    /// <summary>
    /// Interaction logic for ClientWindow.xaml
    /// </summary>
    public partial class ClientWindow : Window
    {
        private readonly System.Windows.Forms.NotifyIcon NotifyIcon;
        private ChromiumWebBrowser WebBrowser;
        private TrafficClientController TrafficClientController = new TrafficClientController();
        private TrafficUrlItem Traffic;
        private bool BrowserLoaded = false;
        private bool GoogleCaptcha = false;
        private bool IsLoadContent = true;
        private bool IsSearchTraffic = false;
        private string GetUrl;
        private string TargetUrl = null;
        private string Keyword = null;
        private readonly DispatcherTimer Timer = new DispatcherTimer();
        private readonly DispatcherTimer ScrollTimer = new DispatcherTimer();
        private bool StartSession = false;
        private int SerpPage = 0;
        private bool FoundTarget = false;
        private bool IsScrolling = false;
        private bool IsScrollEnd = false;
        private int ScrollCountdown = 30;
        private int InternalLinkComplete = 0;
        private readonly string[] InternalLinks;

        public ClientWindow()
        {
            InitializeComponent();
            Height = SystemParameters.MaximizedPrimaryScreenHeight;
            Width = SystemParameters.MaximizedPrimaryScreenWidth;

            var bitmap = new Bitmap("D:\\Visual Studio\\projects\\Terus Traffic\\icon.png");
            var icon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
            NotifyIcon = new System.Windows.Forms.NotifyIcon
            {
                BalloonTipText = "Terus Traffic Client has been minimised. Double click this icon to show.",
                Text = "Terus Traffic Client",
                Icon = icon
            };
            NotifyIcon.DoubleClick += new EventHandler(NotifyIcon_DoubleClick);
            NotifyIcon.Visible = true;

            Application.Current.SessionEnding += new SessionEndingCancelEventHandler(CurrentSession_Cancel);
            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 1);
            ScrollTimer.Tick += ScrollTimer_Tick;
            ScrollTimer.Interval = new TimeSpan(0, 0, 1);
            InternalLinks = new string[]
            {
                "https://terusvn.com/dich-vu-seo-tong-the-uy-tin-hieu-qua-tai-terus/",
                "https://terusvn.com/thiet-ke-website-tai-hcm/",
                "https://terusvn.com/dich-vu-quang-cao-google-tai-terus/",
                "https://terusvn.com/dich-vu-facebook-ads-tai-terus/"
            };

            Hide();

            GenerateUserAgent();
        }

        private void InitBrowser(string userAgent, int width, int height)
        {
            try
            {
                CefSharpSettings.SubprocessExitIfParentProcessClosed = true;
                //CefSharpSettings.RuntimeStyle = CefRuntimeStyle.Chrome;
                CefSettings settings = new CefSettings();
                settings.CefCommandLineArgs.Add("proxy-server", "200.29.191.149:3128");
                CefSharpSettings.Proxy = new ProxyOptions("117.0.200.23", "40599", username: "yiekd_phanp", password: "HsRDWj87");
                settings.CefCommandLineArgs.Add("disable-application-cache", "1");
                settings.CefCommandLineArgs.Add("disable-session-storage", "1");
                settings.CefCommandLineArgs.Add("mute-audio", "1");
                settings.CefCommandLineArgs["autoplay-policy"] = "no-user-gesture-required";
                settings.CefCommandLineArgs.Add("log-severity", "disable");
                settings.Locale = "vi";
                if (!string.IsNullOrEmpty(userAgent))
                    settings.UserAgent = userAgent;
                if (!Cef.IsInitialized.HasValue || Cef.IsInitialized == false)
                    Cef.Initialize(settings);
                WebBrowser = new ChromiumWebBrowser();
                if (width != 0 && height != 0)
                {
                    WebBrowser.Width = width;
                    WebBrowser.Height = height;
                    Width = width + width * 0.1;
                    Height = height + height * 0.12;
                }
                else
                {
                    MinWidth = 1366.0;
                    MinHeight = 768.0;
                }
                WebBrowser.Loaded += WebBrowser_Load;
                WebBrowser.FrameLoadEnd += WebBrowser_LoadEnd;
                WebBrowser.Load("https://www.google.com");
                IsLoadContent = true;
                GridBrowser.Children.Add(WebBrowser);
                WindowState = WindowState.Maximized;
                MinWidth = 1366.0;
                MinHeight = 768.0;

                //DataContext = new ClientViewModel(
                //    WebBrowser,
                //    "https://terusvn.com/thiet-ke-website-tai-hcm/",
                //    "Thiết kế web Terus",
                //    Dispatcher
                //);

                Hide();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Your PC need install Visual C++ 2015 before run Live Traffic Client" + Environment.NewLine + ex.Message);
                Application.Current.Shutdown();
            }
        }

        private void GenerateUserAgent()
        {
            string userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/100.0.4896.127 Safari/537.36";
            int width = 0;
            int height = 0;
            InitBrowser(userAgent, width, height);
        }
            
        private void WebBrowser_Load(object sender, RoutedEventArgs e)
        {
            BrowserLoaded = true;
        }

        private void WebBrowser_LoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            try
            {
                if (!e.Frame.IsMain) return;
                Dispatcher.Invoke(() =>
                {
                    IsLoadContent = true;
                    //Console.WriteLine("Address: " + WebBrowser.Address);
                    var uri = new Uri(WebBrowser.Address);
                    if (WebBrowser.Address.Contains("google.com/sorry") || uri.Host == "ipv6.google.com" || uri.Host == "ipv4.google.com" || uri.Host == "www.ipv6.google.com" || uri.Host == "www.ipv4.google.com")
                    {
                        Console.WriteLine("Google reCaptcha");
                        GoogleCaptcha = true;
                        new Thread((ThreadStart)(() =>
                        {
                            MessageBox.Show("Vô làm Google Captcha đi!");
                            Thread.CurrentThread.IsBackground = true;
                        })).Start();
                        //IsLoadContent = false;
                        //IsSearchTraffic = false;
                        //WebBrowser.Address = TargetUrl;
                        //SerpPage = 1;
                    }
                    if (WebBrowser.Address.Contains("google.com/search") && SerpPage < 10)
                    {
                        if (GoogleCaptcha) GoogleCaptcha = false;
                        SerpPage++;
                    }
                    LoadURL();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Load failed" + Environment.NewLine + ex.Message);
            }
        }

        private void CurrentSession_Cancel(object sender, SessionEndingCancelEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = WindowState.Maximized;
            BringIntoView();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!StartSession) return;
                if (!IsLoadContent) return;
                if (!IsSearchTraffic)
                {
                    if (!IsScrolling && !IsScrollEnd && InternalLinkComplete <= 0 && !GoogleCaptcha)
                    {
                        ScrollToTheEndOfPage(30 * 1000);
                        ScrollTimer.Start();
                    }
                    else if (IsScrollEnd && InternalLinkComplete < 4)
                    {
                        HandleInternalLink();
                    }
                    else if (InternalLinkComplete >= 4)
                    {
                        //TrafficClientController.UpdateAndReleaseItem(Traffic);
                        //GoogleCaptcha = false;
                        //IsSearchTraffic = false;
                        //GetUrl = null;
                        //TargetUrl = null;
                        //Keyword = null;
                        //StartSession = false;
                        //SerpPage = 0;
                        //FoundTarget = false;
                        //IsScrolling = false;
                        //IsScrollEnd = false;
                        //InternalLinkComplete = 0;
                        //WebBrowser.Address = "https://www.google.com";
                    }
                }
                else
                {
                    if (!FoundTarget)
                    {
                        if (SerpPage == 0 && !GoogleCaptcha)
                        {
                            HandleSearch();
                        }
                        else if (SerpPage > 0 && !GoogleCaptcha)
                        {
                            FindResult();
                        }
                        else
                        {
                            return;
                        }
                    }
                    else if (FoundTarget && !IsScrolling && !IsScrollEnd)
                    {
                        ScrollToTheEndOfPage(30 * 1000);
                        ScrollTimer.Start();
                    }
                    else if (FoundTarget && IsScrollEnd && InternalLinkComplete < 4)
                    {
                        HandleInternalLink();
                    }
                    else if (FoundTarget && InternalLinkComplete > 4)
                    {
                        TrafficClientController.UpdateAndReleaseItem(Traffic);
                        GoogleCaptcha = false;
                        IsSearchTraffic = false;
                        GetUrl = null;
                        TargetUrl = null;
                        Keyword = null;
                        StartSession = false;
                        SerpPage = 0;
                        FoundTarget = false;
                        IsScrolling = false;
                        IsScrollEnd = false;
                        InternalLinkComplete = 0;
                        WebBrowser.Address = "https://www.google.com";
                    }
                    else
                    {
                        // Lỗi
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        private void ScrollTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (ScrollCountdown <= 0)
                {
                    ScrollTimer.Stop();
                    IsScrolling = false;
                    IsScrollEnd = true;
                    Console.WriteLine("Finished scroll");
                    ScrollCountdown = 30;
                    if (FoundTarget && InternalLinkComplete >= 4)
                    {
                        TrafficClientController.UpdateAndReleaseItem(Traffic);
                        GoogleCaptcha = false;
                        IsSearchTraffic = false;
                        GetUrl = null;
                        TargetUrl = null;
                        Keyword = null;
                        StartSession = false;
                        SerpPage = 0;
                        FoundTarget = false;
                        IsScrolling = false;
                        IsScrollEnd = false;
                        InternalLinkComplete = 0;
                        WebBrowser.Address = "https://www.google.com";
                    }
                }
                else
                {
                    ScrollCountdown--;
                    Console.WriteLine("Scroll countdown: " + ScrollCountdown);
                }
            }
            catch (Exception ex)
            {
                // Xử lý lỗi khi không thể lấy thông tin cuộn
                Console.WriteLine("Error Check Scroll: " + Environment.NewLine + ex.Message);
                ScrollTimer.Stop();
                IsScrolling = false;
                IsScrollEnd = false;
                //MessageBox.Show("Lỗi Scroll Timer: " + Environment.NewLine + ex.Message);
                //ResourceShutdown();
                //Close();
            }
        }

        private void LoadURL()
        {
            try
            {
                if (!BrowserLoaded)
                {
                    Application.Current.Shutdown();
                    return;
                }
                if (!StartSession)
                {
                    Traffic = TrafficClientController.GetNextItemToProcess();
                    if (Traffic == null) WebBrowser.Address = "http://www.bing.com";
                    if(Traffic.Type == "Search")
                    {
                        GetUrl = $"{Traffic.Url}*{Traffic.Keyword}";
                    }
                    else
                    {
                        GetUrl = Traffic.Url;
                    }
                    RunSession();
                }
                else
                {
                    for (int i = 0; i < InternalLinks.Length; i++)
                    {
                        if (InternalLinks[i] == WebBrowser.Address)
                        {
                            InternalLinkComplete = i + 1;
                            break;
                        }
                    }
                    if (InternalLinkComplete == 0)
                    {
                        FoundTarget = WebBrowser.Address == TargetUrl;
                    }
                    else
                    {
                        FoundTarget = true;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot Load URL:" + Environment.NewLine + ex.Message);
            }
        }

        private void RunSession()
        {
            try
            {
                WebBrowser.Stop();
                StartSession = true;
                Timer.Start();
                if (GetUrl.Contains("*"))
                {
                    IsSearchTraffic = true;
                    string[] strArr = GetUrl.Split('*');
                    TargetUrl = strArr[0];
                    Keyword = strArr[1];
                }
                else
                {
                    IsSearchTraffic = false;
                    WebBrowser.Address = TargetUrl;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Cannot View:" + Environment.NewLine + ex.Message);
            }
        }

        private void HandleSearch()
        {
            try
            {
                Console.WriteLine("Seaching");
                var script = "var els=document.getElementsByName('q')[0];els.value = '" + Keyword + "';setTimeout(() => {document.querySelector('form').submit();}, 1000);";
                WebBrowser.EvaluateScriptAsync(script, new TimeSpan?(), false);
                IsLoadContent = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Search Error:" + Environment.NewLine + ex.Message);
            }
        }

        private void FindResult()
        {
            try
            {
                if (WebBrowser.CanExecuteJavascriptInMainFrame)
                {
                    Console.WriteLine("In SERP Page: " + SerpPage);
                    var script = $"var targetUrl = '{TargetUrl}';var page = {SerpPage + 1};";
                    script += @"
                        var results = document.querySelectorAll(`a[href='${targetUrl}']`)
                        if (results.length > 0) {
                            setTimeout(() => { results[0].click(); }, 1000);
                        } else {
                            setTimeout(() => { document.querySelector(`#botstuff table a[aria-label='Page ${page}']`).click(); });
                        }
                    ";
                    WebBrowser.EvaluateScriptAsync(script, new TimeSpan?(), false);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + Environment.NewLine, ex.Message);
            }
        }

        private async void ScrollToTheEndOfPage(int miliseconds)
        {
            if (WebBrowser != null && WebBrowser.CanExecuteJavascriptInMainFrame)
            {
                IsScrolling = true;
                string script = $"const totalDuration = {miliseconds}";
                script += @"
                    const pauseDuration = 1000;
                    const start = window.scrollY;
                    const end = document.body.scrollHeight;
                    const startTime = performance.now();
                    let lastScrollTime = startTime;
                    let currentY = start;

                    function scrollStep() {
                        const currentTime = performance.now();
                        const timeElapsed = currentTime - startTime;

                        if (timeElapsed >= totalDuration) {
                            window.scrollTo(0, end);
                            return;
                        }

                        const timeSinceLastScroll = currentTime - lastScrollTime;

                        if (timeSinceLastScroll >= pauseDuration) {
                            const remainingTime = totalDuration - timeElapsed;
                            const scrollableDistance = end - start;
                            const remainingScrolls = Math.ceil(remainingTime / pauseDuration); // Estimate remaining scrolls

                            // Calculate the distance to scroll in this step (distribute remaining distance)
                            const scrollAmount = remainingScrolls > 0 ? scrollableDistance / remainingScrolls : end - currentY;

                            currentY += scrollAmount;
                            window.scrollTo(0, Math.min(currentY, end)); // Ensure we don't overshoot

                            lastScrollTime = performance.now();
                        }

                        requestAnimationFrame(scrollStep);
                    }

                    requestAnimationFrame(scrollStep);
                ";
                await WebBrowser.EvaluateScriptAsync(script, new TimeSpan?(), false);
            }
        }

        private void HandleInternalLink()
        {
            try
            {
                if (!WebBrowser.CanExecuteJavascriptInMainFrame) return;
                var script = $"document.querySelector(`.footer-link[href='{InternalLinks[InternalLinkComplete]}']`).click();";
                WebBrowser.EvaluateScriptAsync(script, new TimeSpan?(), false);
                Console.WriteLine("Handle internal: " + InternalLinks[InternalLinkComplete]);
                IsScrolling = false;
                IsScrollEnd = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error:" + Environment.NewLine, ex.Message);
            }
        }

        private int CheckRank(string url, string keyword)
        {
            try
            {
                var webClient = new WebClient();
                webClient.Headers.Add("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/72.0.3626.121 Safari/537.36");
                int num = 0;
                int p = 0;
                string l = "Not in top 100";
                var address = new Uri("https://www.google.com/search?num=100&q=" + keyword);
                foreach ( object match in Regex.Matches( HttpUtility.HtmlDecode( webClient.DownloadString(address)), "(?<=<div class=\"zReHs\"><a href=\".*?url=)(.*?)(?=&ved)", RegexOptions.Singleline) )
                {
                    ++num;
                    if (num <= 100)
                    {
                        if (match.ToString().Contains(url))
                        {
                            l = match.ToString();
                            p = num;
                            break;
                        }
                    }
                    else
                        break;
                }
                return p;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            WebBrowser.Dispose();
            WebBrowser = null;
            Timer.Stop();
            ScrollTimer.Stop();
        }

        private void Exit()
        {
            new Thread((ThreadStart)(() =>
            {
                Thread.CurrentThread.IsBackground = true;
            })).Start();
        }

        private void ResourceShutdown()
        {
            Exit();
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }
}
