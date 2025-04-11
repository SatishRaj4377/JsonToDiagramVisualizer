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
