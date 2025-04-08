window.attachResizeEvents = (dotNetHelper) => {
    function mouseMoveHandler(e) {
        dotNetHelper.invokeMethodAsync('OnMouseMove', e.clientX);
    }

    function mouseUpHandler(e) {
        dotNetHelper.invokeMethodAsync('OnMouseUp');
        document.removeEventListener('mousemove', mouseMoveHandler);
        document.removeEventListener('mouseup', mouseUpHandler);
    }
    document.addEventListener('mousemove', mouseMoveHandler);
    document.addEventListener('mouseup', mouseUpHandler);
}

window.getWindowWidth = () => {
    return window.innerWidth;
};

