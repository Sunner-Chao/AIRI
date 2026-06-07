using System.IO;
using Alife.Basic;

namespace Alife.Function.DeskPet;

/// <summary>
/// 负责桌宠的自主业务行为逻辑 (灵魂)
/// </summary>
public class PetActivity : IDisposable
{
    public PetActivity(PetProcess process, PetBridge bridge, PetModelMetadata metadata, MainWindow window)
    {
        this.process = process;
        this.bridge = bridge;
        this.metadata = metadata;
        this.window = window;

        detector = new MotionDetector();
        tracker = new MouseTracker();
        cancellationTokenSource = new CancellationTokenSource();

        this.bridge.OnReady += OnPetReady;
    }
    public void Dispose()
    {
        cancellationTokenSource.Dispose();
    }

    readonly PetProcess process;
    readonly PetBridge bridge;
    readonly MainWindow window;
    readonly PetModelMetadata metadata;
    readonly MotionDetector detector;
    readonly MouseTracker tracker;
    readonly CancellationTokenSource cancellationTokenSource;

    double lastWindowMouseX;
    double lastWindowMouseY;

    long lastMouseMoveTime;//上次鼠标移动时间
    int comboCount;//短时间累计点击数量
    long lastHitTime;//上次点击交互时间
    bool isDragging;

    async void OnPetReady()
    {
        try
        {
            //加载live2d
            await bridge.LoadModel(metadata.ModelPath);
            HandleInteraction("startup");

            //监听客户端输入
            process.InputReceived += OnCommandReceived;
            process.ListenInput();

            //监听桌宠端输入
            bridge.OnPoke += HandleMousePoke;
            bridge.OnInput += text => process.SendOutput(new InputEvent(text));
            bridge.OnDragStart += () => isDragging = true;
            bridge.OnDragEnd += () => isDragging = false;

            //监听鼠标移动
            tracker.MouseMoved += OnMouseMove;
            tracker.Start();

            //监听特殊运动交互
            detector.WindowShaken += () => {
                HandleInteraction("window_shake");
                process.SendOutput(new InteractionEvent("桌宠被大幅晃动"));
            };
            detector.WindowMoved += () => {
                HandleInteraction("window_move");
                process.SendOutput(new InteractionEvent("桌宠被快速移动"));
            };
            detector.MouseShaken += () => {
                HandleInteraction("mouse_shake");
                process.SendOutput(new InteractionEvent("鼠标在快速转圈"));
            };

            //自动重置视线功能
            HandleFocusReset(cancellationTokenSource.Token);

            process.SendOutput(new ReadyEvent());
        }
        catch (Exception e)
        {
            await File.AppendAllTextAsync("pet.log", e + Environment.NewLine);
        }
    }

    void OnMouseMove(int x, int y)
    {
        //计算窗口鼠标位置（收dpi影响，符合窗口坐标系的鼠标位置）
        lastMouseMoveTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        (double ScaleX, double ScaleY) dpi = window.GetDpi();
        double windowMouseX = x / dpi.ScaleX;
        double windowMouseY = y / dpi.ScaleY;

        {
            //传递鼠标事件给运动交互侦测器
            (double Left, double Top, double Width, double Height) layout = window.GetLayout();
            double centerX = layout.Left + layout.Width / 2;
            double centerY = layout.Top + layout.Height / 2;
            detector.Update(windowMouseX, windowMouseY, centerX, centerY, layout.Left, layout.Top);

            //传递给桌宠看鼠标
            double clientX = windowMouseX - layout.Left;
            double clientY = windowMouseY - layout.Top;
            bridge.SetFocus(clientX, clientY);

            //实现窗口拖动功能
            if (isDragging)
            {
                window.Left += windowMouseX - lastWindowMouseX;
                window.Top += windowMouseY - lastWindowMouseY;
            }
        }

        lastWindowMouseX = windowMouseX;
        lastWindowMouseY = windowMouseY;
    }
    void HandleMousePoke(List<string> areas)
    {
        //计算交互部位
        string? category = null;
        if (areas.Any(a => a.Contains("Head", StringComparison.OrdinalIgnoreCase))) category = "head";
        else if (areas.Any(a => a.Contains("Body", StringComparison.OrdinalIgnoreCase))) category = "body";
        if (category == null)
            return;//不支持

        //连击交互判定
        long now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (now - lastHitTime < 2500) comboCount++;
        else comboCount = 1;
        lastHitTime = now;
        if (comboCount != 0 && comboCount % 3 == 0)
        {
            HandleInteraction("mouse_combo");
            process.SendOutput(new InteractionEvent("桌宠被连续触摸：" + category));
            return;
        }

        //普通点击交互
        HandleInteraction(category);
    }
    void HandleInteraction(string type)
    {
        if (metadata.Interactions.TryGetValue(type, out List<InteractionItem>? pool) == false || pool.Count == 0)
            return;

        InteractionItem item = pool[Random.Shared.Next(pool.Count)];
        if (string.IsNullOrEmpty(item.Text) == false) bridge.ShowBubble(item.Text);
        if (string.IsNullOrEmpty(item.Exp) == false) bridge.PlayExpression(item.Exp);
        if (item.Mtn != null) bridge.PlayMotion(item.Mtn.Group, item.Mtn.Index);
    }
    async void HandleFocusReset(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(500, cancellationToken);
                if (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastMouseMoveTime > 3000)
                {
                    (double Left, double Top, double Width, double Height) layout = window.GetLayout();
                    bridge.SetFocus(layout.Width / 2, layout.Height / 2);
                }
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception e)
        {
            File.AppendAllText("pet.log", e + Environment.NewLine);
        }
    }

    void OnCommandReceived(IpcCommand cmd)
    {
        switch (cmd)
        {
            case WindowMoveCommand moveCmd:
                window.ProgrammaticMove(moveCmd.X, moveCmd.Y, moveCmd.Duration);
                break;
            case GetPositionCommand:
                (double Left, double Top, double Width, double Height) layout = window.GetLayout();
                (double ScaleX, double ScaleY) dpi = window.GetDpi();
                process.SendOutput(new PositionEvent((layout.Left + layout.Width / 2) * dpi.ScaleX, (layout.Top + layout.Height / 2) * dpi.ScaleY));
                break;
            case BubbleCommand b: bridge.ShowBubble(b.Text); break;
            case PlayExpressionCommand e: bridge.PlayExpression(e.Id); break;
            case MotionCommand m: bridge.PlayMotion(m.Group, m.Index); break;
            case HideBubbleCommand: bridge.HideBubble(); break;
            case StatusCommand s: bridge.SendStatus(s.Working); break;
        }
    }
}
