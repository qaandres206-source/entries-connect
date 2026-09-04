// Si una imagen de una nota no se puede servir (ConnectWise rechazó la petición, la
// sesión caducó o el contenedor se reinició), en lugar de dejar el icono de imagen rota
// se muestra el enlace para abrirla directamente en ConnectWise.
window.cwNoteImageFailed = (img) => {
    const wrap = img.closest('.note-img-wrap');
    if (!wrap) return;
    const btn = wrap.querySelector('.note-img-btn');
    const fallback = wrap.querySelector('.note-img-fallback');
    if (btn) btn.hidden = true;
    if (fallback) fallback.hidden = false;
};

// Descarga un archivo generado en el servidor (base64) desde el navegador.
window.downloadFileFromBase64 = (fileName, base64, contentType) => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
    }
    const blob = new Blob([bytes], { type: contentType || 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
