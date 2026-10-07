// FOCUS 360 — File download helper (triggered via JS Interop from Blazor)
window.focus360DownloadStream = async (fileName, streamRef) => {
    try {
        const arrayBuffer = await streamRef.arrayBuffer();
        const blob = new Blob([arrayBuffer], { type: "application/pdf" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = fileName || "download.pdf";
        a.click();
        URL.revokeObjectURL(url);
    } catch (err) {
        console.error("PDF download failed:", err);
    }
};

window.focus360Download = function (fileName, mimeType, byteArray) {
    const blob = new Blob([new Uint8Array(byteArray)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};