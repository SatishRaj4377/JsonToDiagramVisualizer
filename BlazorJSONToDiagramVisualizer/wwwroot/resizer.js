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

