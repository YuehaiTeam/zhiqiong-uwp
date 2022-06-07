using System;
using System.Linq;
using System.IO;
using System.Net.Http;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.ApplicationModel;
using Windows.UI.Xaml.Controls;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml.Navigation;
using Microsoft.Gaming.XboxGameBar;
using System.Text.RegularExpressions;

namespace zhiqiong
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public bool isInGameBar = false;
        public bool hasInputBox = false;
        public int origWidth = 0;
        public int origHeight = 0;
        public string currentMap = "CN";
        public string mapCN = "https://webstatic.mihoyo.com/app/ys-map-cn/index.html#/map/2";
        public string mapOS = "https://act.hoyolab.com/ys/app/interactive-map/index.html#/map/2";
        public string mapJG = "https://yuanshen.site/index.html?locale=zh-CN";
        public XboxGameBarWidget gamebarWindow = null;
        public MainPage()
        {

            InitializeComponent();
            WvInit();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            var param = e.Parameter;
            if (param != null && typeof(XboxGameBarWidget) == param.GetType())
            {
                isInGameBar = true;
                gamebarWindow = param as XboxGameBarWidget;
            }
        }
        public IAsyncAction OpenInFullTrust(string url)
        {
            ApplicationData.Current.LocalSettings.Values["parameters"] = url;
            return FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }
        /// <summary>
        /// Toggle maximize mode
        /// </summary>
        public async void ToggleMaximize()
        {
            if (gamebarWindow == null) return;
            if (origHeight > 0 && origWidth > 0)
            {
                Size size = new Size(origWidth, origHeight);
                bool res = await gamebarWindow.TryResizeWindowAsync(size);
                await webView.CoreWebView2.ExecuteScriptAsync("console.log('RESIZE:" + res + "')");
                origWidth = 0;
                origHeight = 0;
            }
            else
            {
                // store original size from js
                string ret = await webView.CoreWebView2.ExecuteScriptAsync("({w:window.innerWidth,h:window.innerHeight,mh:screen.availHeight-100,mw:screen.availWidth*0.9})");
                var json = Windows.Data.Json.JsonObject.Parse(ret);
                origWidth = (int)json.GetNamedNumber("w");
                origHeight = (int)json.GetNamedNumber("h");
                Size size = new Size(json.GetNamedNumber("mw"), json.GetNamedNumber("mh"));
                bool res = await gamebarWindow.TryResizeWindowAsync(size);
                await webView.CoreWebView2.ExecuteScriptAsync("console.log('RESIZE:" + res + "')");
            }
        }
        public void loadMapPage()
        {
            var cachedMap = ApplicationData.Current.LocalSettings.Values["currentMap"];
            if (cachedMap != null)
            {
                currentMap = cachedMap.ToString();
                if (currentMap != "CN" && currentMap != "OS" && currentMap != "JG")
                {
                    currentMap = "CN";
                }
            }
            webView.CoreWebView2.Navigate(currentMap == "CN" ? mapCN : (currentMap == "OS" ? mapOS : mapJG));
        }
        /// <summary>
        /// Run JS in Webview2 on init
        /// </summary>
        public async void WvInit()
        {
            await webView.EnsureCoreWebView2Async();
            await webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.alert = (msg)=>{window.chrome.webview.postMessage({action:'ALERT',msg:msg.toString()})};
                !function(){const s = document.createElement('script')
                s.src = 'https://zhiqiong.vercel.app/sharedmap.user.js?t='+Math.floor(new Date().getTime()/(1000*3600*24))*(3600*24)
                s.onerror = () => { alert('共享地图加载失败，请检查是否可以连接到 https://zhiqiong.vercel.app '); }
                window.addEventListener('DOMContentLoaded',()=>{document.head.appendChild(s);window.addEventListener('contextmenu', (e)=>{e.stopImmediatePropagation()},true);})}()
                document.addEventListener('focus',(e)=>{if(e.target.tagName==='INPUT'||e.target.tagName==='TEXTAREA')window.chrome.webview.postMessage({action:'INPUT'})}, true);
                window.onload = ()=>{window.chrome.webview.postMessage({action:'LOAD'})};
            ");
            webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
            webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
            webView.CoreWebView2.AddWebResourceRequestedFilter("http://localhost:32333/*", CoreWebView2WebResourceContext.All);
            webView.CoreWebView2.AddWebResourceRequestedFilter("https://yuanshen.site/index.html*", CoreWebView2WebResourceContext.All);
            loadMapPage();
        }
        /// <summary>
        /// Set headers
        /// </summary>
        public void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            if (e.Request.Uri.ToString().Contains("yuanshen.site/index"))
            {
                // send http request
                var def = e.GetDeferral();
                var client = new HttpClient();
                var response = client.GetAsync(e.Request.Uri).GetAwaiter().GetResult();
                var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                // replace csp tag in content
                responseContent = responseContent.Replace("-Policy", "");
                // make a irandomaccessstream
                InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
                // write to stream
                DataWriter writer = new DataWriter(stream);
                writer.WriteString(responseContent);
                writer.StoreAsync().GetAwaiter().GetResult();
                // set response
                CoreWebView2WebResourceResponse newres = webView.CoreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", "Content-Type: text/html");
                e.Response = newres;
                // go
                def.Complete();
                return;
            }
            e.Request.Headers.SetHeader("Origin", "@zhiqiong");
        }
        /// <summary>
        /// Handle message
        /// </summary>
        public async void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string j = e.WebMessageAsJson;
            var jObject = Windows.Data.Json.JsonObject.Parse(j);
            // get action
            string action = jObject.GetNamedString("action");
            if (action == "PLUGIN")
            {
                string pluginToken = jObject.GetNamedString("token");
                string pluginQuery = pluginToken == "" ? "" : ("?local-auth=" + pluginToken);
                string pluginLaunch = "cocogoat-control://launch" + pluginQuery;
                if (this.isInGameBar)
                {
                    await OpenInFullTrust(pluginLaunch);
                }
                else
                {
                    var uri = new Uri(pluginLaunch);
                    await Windows.System.Launcher.LaunchUriAsync(uri);
                }
            }
            if (action == "INPUT" && isInGameBar && !hasInputBox)
            {
                InputBox();
            }
            if (action == "MAXIMIZE" && isInGameBar)
            {
                ToggleMaximize();
            }
            if (action == "LOAD")
            {
                await webView.CoreWebView2.ExecuteScriptAsync(@"
                    console.log('Zhiqiong-UWP: Load');
                    webControlMAP.ev.on('hotkey',(e)=>{if(e==='AltZ')window.chrome.webview.postMessage({action:'MAXIMIZE'})})
                    fetch('https://77.cocogoat.work/upgrade/zhiqiong-uwp.json?t='+Math.round(Date.now()/1000/3600)).then(e=>e.json()).then(e=>{
                        const targetVer = e.version;
                        const curVer = (navigator.userAgent.match(/zhiqiong-uwp\/([0-9.]*)/)||[])[1]||'0.0.0.0'
                        if($map.control.versionCompare(targetVer,curVer)>0){
                            window.chrome.webview.postMessage({action:'COPYALERT',url:'https://zhiqiong.cocogoat.work',msg:'发现新版本 v'+targetVer+'（当前版本 v'+curVer+'），请按Win+G打开Xbox Game Bar后复制下方地址手动下载更新'})
                        }
                    })
                ");
            }
            if (action == "ALERT")
            {
                string msg = jObject.GetNamedString("msg");
                await new MessageDialog(msg).ShowAsync();
            }
            if (action == "COPYALERT")
            {
                ContentDialog dialog = new ContentDialog();
                TextBox inputTextBox = new TextBox();
                dialog.Content = inputTextBox;
                inputTextBox.Text = jObject.GetNamedString("url");
                dialog.Title = jObject.GetNamedString("msg");
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
        }
        /// <summary>
        /// Change UserAgent
        /// </summary>
        public void CoreWebView2_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // get version
            var version = Windows.ApplicationModel.Package.Current.Id.Version;
            var settings = webView.CoreWebView2.Settings;
            string strver = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build, version.Revision);
            // don't change if modified
            if (settings.UserAgent.Contains("zhiqiong-uwp/"))
            {
                return;
            }
            settings.UserAgent = settings.UserAgent + " zhiqiong-uwp/" + strver;
            settings.UserAgent = settings.UserAgent + " zhiqiong-dim/" + (isInGameBar ? "gamebar" : "webview");
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
                await OpenInFullTrust(e.Uri);
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
            if (currentMap != "JG")
            {

                CoreWebView2ContextMenuItem subItem = webView.CoreWebView2.Environment.CreateContextMenuItem("切换到空荧酒馆", null, CoreWebView2ContextMenuItemKind.Command);
                subItem.CustomItemSelected += delegate (CoreWebView2ContextMenuItem send, Object ex)
                {
                    currentMap = "JG";
                    ApplicationData.Current.LocalSettings.Values["currentMap"] = currentMap;
                    loadMapPage();
                };
                menuList.Insert(0, subItem);
            }
            if (currentMap != "OS")
            {

                CoreWebView2ContextMenuItem subItem = webView.CoreWebView2.Environment.CreateContextMenuItem("切换到HoyoLab", null, CoreWebView2ContextMenuItemKind.Command);
                subItem.CustomItemSelected += delegate (CoreWebView2ContextMenuItem send, Object ex)
                {
                    currentMap = "OS";
                    ApplicationData.Current.LocalSettings.Values["currentMap"] = currentMap;
                    loadMapPage();
                };
                menuList.Insert(0, subItem);
            }
            if (currentMap != "CN")
            {

                CoreWebView2ContextMenuItem subItem = webView.CoreWebView2.Environment.CreateContextMenuItem("切换到米游社", null, CoreWebView2ContextMenuItemKind.Command);
                subItem.CustomItemSelected += delegate (CoreWebView2ContextMenuItem send, Object ex)
                {
                    currentMap = "CN";
                    ApplicationData.Current.LocalSettings.Values["currentMap"] = currentMap;
                    loadMapPage();
                };
                menuList.Insert(0, subItem);
            }

        }
        public async void InputBox()
        {
            hasInputBox = true;
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
            hasInputBox = false;
        }
    }

}