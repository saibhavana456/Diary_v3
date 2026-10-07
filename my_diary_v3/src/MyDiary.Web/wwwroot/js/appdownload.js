// Triggers a browser download from base64 content (used for Excel export).
window.appDownloadFile = (fileName, base64, contentType) => {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64}`;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
};
