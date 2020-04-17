function copyToClipboard(value) {
    element = document.createElement('textarea');
    element.value = value;

    document.body.appendChild(element);
    element.select();

    document.execCommand('copy');
    document.body.removeChild(element);
} 