const ui = {
    bubble: document.getElementById("bubble"),
    bubbleContainer: document.getElementById("bubble-container"),
    thinkingIndicator: document.getElementById("thinking-indicator"),
    chatInput: document.getElementById("chat-input"),
    sendBtn: document.getElementById("send-btn")
};

const app = new PIXI.Application({
    view: document.getElementById("canvas"),
    autoStart: true,
    resizeTo: window,
    transparent: true,
    backgroundAlpha: 0,
});

let model = null;
let isDragging = false;

//输入功能
window.chrome.webview.addEventListener("message", (e) => {
    const msg = e.data;
    switch (msg.type) {
        //加载live2d功能
        case "load":
            loadModel(msg.url);
            break;
        //修改表情
        case "expression":
            model.expression(msg.id);
            break;
        //修改动作
        case "motion":
            model.motion(msg.group, msg.index, PIXI.live2d.MotionPriority.FORCE);
            break;
        //修改气泡文字
        case "bubble":
            ui.bubble.innerText = msg.text;
            ui.bubbleContainer.classList.add("show");
            break;
        case "hide-bubble":
            ui.bubbleContainer.classList.remove("show");
            break;
        //修改注视目标
        case "look":
            model.focus(msg.x, msg.y, msg.instant);
            break;
        //修改状态反馈
        case "status":
            if (msg.working) {
                ui.thinkingIndicator.classList.add("show");
            } else {
                ui.thinkingIndicator.classList.remove("show");
            }
            break;
    }

    async function loadModel(url) {
        if (model) app.stage.removeChild(model);
        model = await PIXI.live2d.Live2DModel.from(url, {autoInteract: false});
        app.stage.addChild(model);

        // 设置动画曲线
        const ctrl = model.internalModel.focusController;
        if (ctrl) {
            ctrl.acceleration = 0.04;
            ctrl.deceleration = 0.08;
        }

        // 设置符合窗口的大小
        const scale = (window.innerHeight * 0.9) / model.height;
        model.scale.set(scale);
        model.anchor.set(0.5, 0.5);
        model.position.set(window.innerWidth / 2, window.innerHeight / 2);
        model.interactive = true;

        postMessage({type: "loaded"});
    }
});


//输出功能
{
    function postMessage(data) {
        window.chrome.webview.postMessage(data);
    }

    //双击触摸反馈
    window.addEventListener("dblclick", async (e) => {
        if (e.target.tagName !== "CANVAS") return;
        const hitAreas = await model.hitTest(e.clientX, e.clientY);
        if (hitAreas.length > 0) postMessage({type: "poke", areas: hitAreas});
    });

    //单击拖动反馈
    window.addEventListener("mousedown", async (e) => {
        if (e.button !== 0 || e.target.tagName !== "CANVAS") return;
        const hitAreas = await model.hitTest(e.clientX, e.clientY);
        if (!hitAreas || hitAreas.length === 0) {
            isDragging = true;
            postMessage({type: "drag_start"});
        }
    });
    window.addEventListener("mouseup", async (e) => {
        if (isDragging === true) {
            isDragging = false;
            postMessage({type: "drag_end"});
        }
    })

    //文本输入反馈
    const onSend = () => {
        const text = ui.chatInput.value.trim();
        if (text) {
            postMessage({type: "input", text});
            ui.chatInput.value = "";
        }
    };
    ui.sendBtn.onclick = onSend;
    ui.chatInput.onkeydown = (e) => {
        if (e.key === "Enter") onSend();
    };

}

postMessage({type: "ready"});
