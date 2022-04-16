using System;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml.Controls;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;

namespace zhiqiong
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public bool isInGameBar = false;
        public MainPage()
        {

            InitializeComponent();
            WvInit();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var param = e.Parameter;
            if (param.ToString() == "GAMEBAR")
            {
                isInGameBar = true;
            }
        }
        /// <summary>
        /// Run JS in Webview2 on init
        /// </summary>
        public async void WvInit()
        {
            await webView.EnsureCoreWebView2Async();
            webView.CoreWebView2.Navigate("https://webstatic.mihoyo.com/app/ys-map-cn/index.html#/map/2");
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"!function(){const s = document.createElement('script')
                s.src = 'https://zhiqiong.vercel.app/sharedmap.user.js?t='+Math.floor(new Date().getTime()/(1000*3600*24))*(3600*24)
                s.onerror = () => { alert('共享地图加载失败，请检查是否可以连接到 https://zhiqiong.vercel.app '); }
                window.addEventListener('DOMContentLoaded',()=>{document.head.appendChild(s);window.addEventListener('contextmenu', (e)=>{e.stopImmediatePropagation()},true);})}()
                document.addEventListener('focus',(e)=>{if(e.target.tagName==='INPUT'||e.target.tagName==='TEXTAREA')window.chrome.webview.postMessage('INPUT')}, true);
            ");
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
            webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }
        /// <summary>
        /// Handle message
        /// </summary>
        public void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string j = e.WebMessageAsJson;
            var jValue = Windows.Data.Json.JsonValue.Parse(j).GetString();
            if (jValue == "INPUT" && isInGameBar)
            {
                InputBox();
            }
        }
        /// <summary>
        /// Change UserAgent
        /// </summary>
        public void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            var settings = webView.CoreWebView2.Settings;
            // don't change if modified
            if (settings.UserAgent.Contains("zhiqiong-uwp/"))
            {
                return;
            }
            settings.UserAgent = settings.UserAgent + " zhiqiong-uwp/" + (isInGameBar ? "gamebar" : "webview");
        }
        /// <summary>
        /// Open default browser instead
        /// </summary>
        public async void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // preventdefault
            e.Handled = true;
            if (this.isInGameBar)
            {
                // Opening window is not allowed in gamebar, so open prompt box
                ContentDialog dialog = new ContentDialog();
                TextBox inputTextBox = new TextBox();
                dialog.Content = inputTextBox;
                inputTextBox.Text = e.Uri;
                dialog.Title = "暂不支持在悬浮窗中打开外部链接，请复制到浏览器打开";
                dialog.PrimaryButtonText = "复制并关闭";
                dialog.SecondaryButtonText = "取消";
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    // copy to clipboard
                    Windows.ApplicationModel.DataTransfer.DataPackage dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetText(inputTextBox.Text);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                }
            }
            else
            {
                var uri = new Uri(e.Uri);
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }
        /// <summary>
        /// Change contextmenu
        /// </summary>
        public void CoreWebView2_ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            IList<CoreWebView2ContextMenuItem> menuList = args.MenuItems;
            // remove
            bool hasSelectAll = false;
            bool hasReload = false;
            string[] ignoredList = { "other", "saveImageAs", "copyImage", "copyImageLocation", "createQrCode", "saveAs", "print", "back", "forward" };
            for (int index = 0; index < menuList.Count; index++)
            {
                if (ignoredList.Contains(menuList[index].Name))
                {
                    menuList.RemoveAt(index);
                    index--;
                }
                else if (menuList[index].Name == "selectAll")
                {
                    hasSelectAll = true;
                }
                else if (menuList[index].Name == "reload")
                {
                    hasReload = true;
                }
            }
            if (!hasReload && !hasSelectAll)
            {
                // no ctxmenu on non-input elements
                args.Handled = true;
            }
            // add
            if (hasSelectAll && isInGameBar)
            {
                CoreWebView2ContextMenuItem subItem = webView.CoreWebView2.Environment.CreateContextMenuItem("无法输入请点我", null, CoreWebView2ContextMenuItemKind.Command);
                subItem.CustomItemSelected += delegate (CoreWebView2ContextMenuItem send, Object ex)
                {
                    this.InputBox();
                };
                menuList.Insert(0, subItem);
            }
        }
        public async void InputBox()
        {
            // Get value of current input
            var jCurrentValue = await webView.CoreWebView2.ExecuteScriptAsync(@"(document.activeElement.tagName==='INPUT'||document.activeElement.tagName==='TEXTAREA')?document.activeElement.value:''");
            var currentValue = Windows.Data.Json.JsonValue.Parse(jCurrentValue).GetString();
            // Get type of current input
            var jCurrentType = await webView.CoreWebView2.ExecuteScriptAsync(@"(document.activeElement.tagName==='INPUT')?document.activeElement.type:'other'");
            var currentType = Windows.Data.Json.JsonValue.Parse(jCurrentType).GetString();
            // Prompt using ContentDialog
            ContentDialog dialog = new ContentDialog();
            if (currentType == "password")
            {
                PasswordBox inputTextBox = new PasswordBox();
                dialog.Content = inputTextBox;
                inputTextBox.Password = currentValue;
            }
            else
            {
                TextBox inputTextBox = new TextBox();
                dialog.Content = inputTextBox;
                inputTextBox.Text = currentValue;
            }
            dialog.Title = "请按Win+G打开Xbox Game Bar后，在此输入" + (currentType == "password" ? "密码" : "内容");
            dialog.IsSecondaryButtonEnabled = true;
            dialog.PrimaryButtonText = "输入";
            dialog.SecondaryButtonText = "取消";
            IAsyncOperation<ContentDialogResult> tsk = dialog.ShowAsync();
            if (await tsk == ContentDialogResult.Primary)
            {
                // escape single quote
                string escapedInput = "";
                if (currentType == "password")
                {
                    PasswordBox inputTextBox = (PasswordBox)dialog.Content;
                    escapedInput = inputTextBox.Password.Replace("'", "\\'");
                }
                else
                {
                    TextBox inputTextBox = (TextBox)dialog.Content;
                    escapedInput = inputTextBox.Text.Replace("'", "\\'");
                }
                await webView.CoreWebView2.ExecuteScriptAsync(@"if(document.activeElement.tagName==='INPUT'||document.activeElement.tagName==='TEXTAREA'){
                            document.activeElement.value = '" + escapedInput + @"';
                            document.activeElement.dispatchEvent(new Event('input'));
                            document.activeElement.blur();
                        }");
            }
            else
            {
                // cancel
                await webView.CoreWebView2.ExecuteScriptAsync(@"if(document.activeElement.tagName==='INPUT'||document.activeElement.tagName==='TEXTAREA'){
                            document.activeElement.blur();
                        }");
            }
        }
    }

}