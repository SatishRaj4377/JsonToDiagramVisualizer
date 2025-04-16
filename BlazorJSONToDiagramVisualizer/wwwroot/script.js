window.attachResizeEvents = (dotNetHelper) => {
    document.body.style.userSelect = "none";
    function mouseMoveHandler(e) {
        e.preventDefault();
        dotNetHelper.invokeMethodAsync('OnMouseMove', e.clientX);
    }

    function mouseUpHandler(e) {
        dotNetHelper.invokeMethodAsync('OnMouseUp');
        document.body.style.userSelect = "";
        document.removeEventListener('mousemove', mouseMoveHandler);
        document.removeEventListener('mouseup', mouseUpHandler);
    }
    document.addEventListener('mousemove', mouseMoveHandler);
    document.addEventListener('mouseup', mouseUpHandler);
}

window.getWindowWidth = () => {
    return window.innerWidth;
};

window.getTextSize = (text, font) => {
    const canvas = document.createElement("canvas");
    const context = canvas.getContext("2d");
    context.font = font || "16px Consolas";

    const lines = text.split("\n");
    const lineHeight = 20; // average line height in px
    let maxWidth = 0;

    for (let line of lines) {
        const metrics = context.measureText(line);
        if (metrics.width > maxWidth) {
            maxWidth = metrics.width;
        }
    }

    let width = maxWidth;
    let height = lines.length * lineHeight;

    height += 10; // Add 10px vertical padding

    return {
        width: width,
        height: height
    };
};

window.exportJsonFile = (data, filename) => {
    try {
        const blob = new Blob([data], { type: "application/json" });
        const link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = filename || "data.json";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (err) {
        console.error("Export failed:", err);
    }
};


window.importJsonFromFile = (dotNetHelper) => {
    if (!dotNetHelper || !dotNetHelper.invokeMethodAsync) {
        console.warn("DotNet helper is not ready.");
        return;
    }

    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".json";
    input.style.display = "none";

    input.onchange = async (e) => {
        const file = e.target.files[0];
        if (!file) return;

        const reader = new FileReader();
        reader.onload = function () {
            const content = reader.result;

            try {
                dotNetHelper.invokeMethodAsync("ReceiveImportedJson", content);
            } catch (err) {
                console.error("Interop error: ", err);
            }
        };
        reader.readAsText(file);
    };

    document.body.appendChild(input);
    input.click();
    document.body.removeChild(input);
};

window.setNodeStyle = function (domId, stroke, fill, strokeWidth) {
    const element = document.getElementById(domId);
    if (element) {
        element.setAttribute("stroke", stroke);
        element.setAttribute("fill", fill);
        element.setAttribute("stroke-width", strokeWidth);
    }
};

window.setTheme = (themeUrl) => {
    const themeLink = document.getElementById('theme-link');
    if (themeLink) {
        themeLink.href = themeUrl;
    }
};

window.changeDiagramBackground = (color) =>{
    document.querySelector('.e-diagram').style.backgroundColor = color;
}
