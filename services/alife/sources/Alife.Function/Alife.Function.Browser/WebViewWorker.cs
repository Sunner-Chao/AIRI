using System.Collections.Concurrent;
using Alife.Basic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Alife.Function.Browser;

/// <summary>
/// 在独立的 STA 线程中管理 WebView2，以支持后台执行浏览器任务。
/// 浏览器窗口默认可见，用户可实时观察 AI 的浏览行为并手动介入。
/// </summary>
public class WebViewWorker : IDisposable
{
    public bool IsNavigating => isNavigating;
    public bool IsLoaded => isLoaded;

    public Task<T> AddFormTask<T>(Func<WebView2, Task<T>> action)
    {
        if (form == null || form.IsDisposed)
            throw new ObjectDisposedException(nameof(WebViewWorker));

        TaskCompletionSource<T> tcs = new();

        formTasks.Add(async () => {
            try
            {
                if (webView == null)
                    throw new ArgumentNullException(nameof(webView));
                T result = await action(webView);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    AlifeForm? form;
    WebView2? webView;
    readonly BlockingCollection<Func<Task>> formTasks = new();
    bool isNavigating;
    bool isLoaded;

    public WebViewWorker()
    {
        var thread = new Thread(() => {
            try
            {
                //创建窗口
                form = new AlifeForm {
                    Text = "Alife Browser",
                    Width = 1024,
                    Height = 768,
                    WindowState = FormWindowState.Minimized,
                    ShowInTaskbar = true,
                    FormBorderStyle = FormBorderStyle.Sizable,
                };
                webView = new WebView2 { Dock = DockStyle.Fill };

                form.Controls.Add(webView);
                //注入窗口初始化事件
                form.Load += OnFormOnLoad;

                Application.Run(form);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
    }

    public void Dispose()
    {
        formTasks.CompleteAdding();
        if (form is { IsDisposed: false })
            form.Invoke((Action)(() => form.Close()));
    }

    async void OnFormOnLoad(object? s, EventArgs e)
    {
        try
        {
            //初始化基本环境
            string userDataFolder = Path.Combine(AlifePath.StorageFolderPath, "WebView2Data");
            if (!Directory.Exists(userDataFolder))
                Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);

            await form!.InvokeAsync(async _ => {
                await webView!.EnsureCoreWebView2Async(env);
                //伪装普通浏览器
                webView.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36 Edge/122.0.0.0";
                //禁用新窗口跳转
                webView.CoreWebView2.NewWindowRequested += (_, ev) => {
                    ev.Handled = true;
                    webView.CoreWebView2.Navigate(ev.Uri);
                };
                //统计加载状态
                webView.CoreWebView2.NavigationStarting += (_, ev) => isNavigating = true;
                webView.CoreWebView2.NavigationCompleted += (_, ev) => isNavigating = false;
            });

            //持续处理分配的formTask任务
            isLoaded = true;
            await Task.Run(() => {
                foreach (Func<Task> formTask in formTasks.GetConsumingEnumerable())
                {
                    if (form.IsDisposed)
                        break;

                    try
                    {
                        Task task = form.Invoke(formTask);
                        task.Wait();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
    }
}

public class AlifeForm : Form
{
    const int CP_NOCLOSE_BUTTON = 0x200;

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams myCp = base.CreateParams;
            myCp.ClassStyle = myCp.ClassStyle | CP_NOCLOSE_BUTTON;
            return myCp;
        }
    }
}
