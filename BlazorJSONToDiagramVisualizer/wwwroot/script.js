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
    context.font = font || "16px Arial";

    const lines = text.split("\n");
    const lineHeight = 20; // pixels per line
    let maxWidth = 0;

    for (let line of lines) {
        const metrics = context.measureText(line);
        if (metrics.width > maxWidth) {
            maxWidth = metrics.width;
        }
    }

    const height = lines.length * lineHeight + 10; //10px padding
    maxWidth += 5; //5px padding
    return {
        width: maxWidth,
        height: height
    };
};


window.showError = (message) => {
    window.alert(message);
}

