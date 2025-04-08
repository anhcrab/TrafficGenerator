using CefSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Terus_Traffic.Ultilities;

namespace Terus_Traffic.ViewModel
{
    public class ClientViewModel : INotifyPropertyChanged
    {
        private IWebBrowser _webBrowser;
        private Dispatcher _dispatcher;
        private TrafficClientController TrafficClientController = new TrafficClientController();
        private TrafficUrlItem Traffic;
        private string _keyword;
        private string _targetUrl;
        private int _currentPageNumber = 1;
        private const int MaxSearchPages = 5; // Chỉ duyệt tối đa 5 trang SERP

        private bool _startSession = false;
        public bool StartSession
        {
            get { return _startSession; }
            set 
            { 
                _startSession = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Start Sesion: " + value.ToString()); 
                if (value) ProcessWorkflow(); 
            }
        }

        private bool _isSearching = true;
        public bool IsSearching
        {
            get { return _isSearching; }
            set 
            { 
                _isSearching = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Is Searching: " + value.ToString()); 
                if (!value) PerformSearch(); 
            }
        }

        private bool _isCaptchaRequired = false;
        public bool IsCaptchaRequired
        {
            get { return _isCaptchaRequired; }
            set 
            { 
                _isCaptchaRequired = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Is Captcha Required: " + value.ToString()); 
                if (!value && IsSearching) PerformSearch(); 
            }
        }

        private bool _isSerpDisplayed = false;
        public bool IsSerpDisplayed
        {
            get { return _isSerpDisplayed; }
            set 
            { 
                _isSerpDisplayed = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Is SERP Displayed: " + value.ToString()); 
                if (value) CheckForTargetAndClick(); 
            }
        }

        private bool _isNavigatingToTarget = false;
        public bool IsNavigatingToTarget
        {
            get { return _isNavigatingToTarget; }
            set 
            { 
                _isNavigatingToTarget = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Update Target" + value.ToString()); 
                if (value) _ = ScrollToBottom(); 
            }
        }

        private bool _isScrolling = false;
        public bool IsScrolling
        {
            get { return _isScrolling; }
            set 
            {
                _isScrolling = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Is Scrolling: " + value.ToString()); 
            }
        }

        private bool _isScrollEnd = false;
        public bool IsScrollEnd
        {
            get { return _isScrollEnd; }
            set 
            { 
                _isScrollEnd = value; 
                OnPropertyChanged(); 
                Console.WriteLine("Is Scroll End" + value.ToString()); 
                if (value) WorkflowCompleted(); 
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ClientViewModel(IWebBrowser webBrowserInstance, string targetUrl, string keyword, Dispatcher dispatcher)
        {
            _webBrowser = webBrowserInstance;
            _targetUrl = targetUrl;
            _keyword = keyword;
            _dispatcher = dispatcher;
            _webBrowser.FrameLoadEnd += OnFrameLoadEnd;
        }

        private async void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            /**
             * check session
             * check search
             * check captcha
             * check serp
             * found target
             * view target
             * scroll
             * scroll end
             * internal
             * 
             */
            if (e.Frame.IsMain)
            {
                await _dispatcher.BeginInvoke(new Action(async () =>
                {
                    if (!StartSession) StartSession = true;
                    //else if (CheckForOutSERP())
                    //{
                    //    IsNavigatingToTarget = true;
                    //    IsSerpDisplayed = false;
                    //    IsSearching = false;
                    //    _currentPageNumber = 1; // Reset số trang
                    //    return;
                    //}
                    else
                    {
                        CheckForCaptcha();
                        if (!IsCaptchaRequired && IsSearching && _webBrowser.Address.Contains("search?"))
                        {
                            IsSerpDisplayed = true;
                        }
                        if (IsNavigatingToTarget && !_isScrolling && _webBrowser.Address.StartsWith(_targetUrl))
                        {
                            IsSearching = false;
                            IsNavigatingToTarget = false;
                            IsScrolling = true;
                            await ScrollToBottom();
                            IsScrollEnd = true;

                        }
                    }
                }));
            }
        }

        private void ProcessWorkflow()
        {
            if (_webBrowser != null && !StartSession)
            {
                _webBrowser.Load("https://www.google.com");
            }
            else if (StartSession && IsSearching)
            {
                IsSearching = false;
            }
        }

        private async void PerformSearch()
        {
            if (_webBrowser != null && !string.IsNullOrEmpty(_keyword))
            {
                IsSearching = true;
                Console.WriteLine("Perform Search");
                string script = "var els=document.getElementsByName('q')[0];els.value = '" + _keyword + "';setTimeout(() => {document.querySelector('form').submit();}, 1000);";
                await _webBrowser.EvaluateScriptAsync(script);
            }
        }

        private void CheckForCaptcha()
        {
            IsCaptchaRequired = _webBrowser.Address.Contains("google.com/sorry");
        }

        private bool CheckForOutSERP()
        {
            return ! _webBrowser.Address.Contains("google.com");
        }

        private async void CheckForTargetAndClick()
        {
            if (_webBrowser != null && !string.IsNullOrEmpty(_targetUrl))
            {
                var script = $"var targetUrl = '{_targetUrl}';var page = {_currentPageNumber + 1};";
                script += @"
                        var results = document.querySelectorAll(`a[href='${targetUrl}']`)
                        if (results.length > 0) {
                            setTimeout(() => { results[0].click(); }, 1000);
                        } else {
                            setTimeout(() => { document.querySelector(`#botstuff table a[aria-label='Page ${page}']`).click(); });
                        }
                    ";
                await _webBrowser.EvaluateScriptAsync(script, new TimeSpan?(), false);
                // Logic tìm kiếm target URL trên trang hiện tại
                string findTargetScript = $"const links = document.querySelectorAll('a[href=\"{_targetUrl}\"] h3'); if (links.length > 0) {{ links[0].click(); }}";
                Console.WriteLine($"{findTargetScript}");
                var targetFoundResponse = await _webBrowser.EvaluateScriptAsync(findTargetScript);
                Thread.Sleep(500);

                if (targetFoundResponse.Success && targetFoundResponse.Result is bool targetFoundAndClicked && targetFoundAndClicked)
                {
                    IsNavigatingToTarget = true;
                    IsSerpDisplayed = false;
                    IsSearching = false;
                    _currentPageNumber = 1; // Reset số trang
                    return;
                }
                else
                {
                    // Nếu không tìm thấy và chưa đạt đến trang cuối cùng
                    if (_currentPageNumber < MaxSearchPages)
                    {
                        // Tìm nút "Trang sau" và click
                        string nextPageScript = "const nextPageButton = document.querySelector('#botstuff table a[aria-label=\"Page " + _currentPageNumber + "\"]'); if (nextPageButton) { nextPageButton.click(); return true; } return false;";
                        var nextPageResponse = await _webBrowser.EvaluateScriptAsync(nextPageScript);

                    }
                    else
                    {
                        Console.WriteLine($"Target URL '{_targetUrl}' not found after {MaxSearchPages} pages.");
                        WorkflowCompleted();
                    }
                }
            }
        }

        private async Task ScrollToBottom()
        {
            if (_webBrowser != null)
            {
                IsScrolling = true;
                string script = "window.scrollTo(0, document.body.scrollHeight);";
                await _webBrowser.EvaluateScriptAsync(script);
                await Task.Delay(2000);
                IsScrolling = false;
                IsScrollEnd = true;
            }
        }

        private void WorkflowCompleted()
        {
            StartSession = false;
            _webBrowser.Load("https://www.google.com");
            Console.WriteLine($"Workflow completed for keyword '{_keyword}' and target URL '{_targetUrl}'.");
        }
    }
}
