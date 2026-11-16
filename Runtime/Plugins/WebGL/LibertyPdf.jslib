mergeInto(LibraryManager.library, {
  LibertyPdfLoad: function(ptr, length, receiverPtr) {
    var data = HEAPU8.slice(ptr, ptr + length), receiver = UTF8ToString(receiverPtr);
    var state = { receiver: receiver, pdf: null, task: null };
    if (window.libertyPdfState && window.libertyPdfState.task) window.libertyPdfState.task.destroy();
    window.libertyPdfState = state;
    import(new URL('StreamingAssets/LibertyPdf/pdf.mjs', document.baseURI).href).then(async function(pdfjs) {
      if (window.libertyPdfState !== state) return;
      pdfjs.GlobalWorkerOptions.workerSrc = new URL('StreamingAssets/LibertyPdf/pdf.worker.mjs', document.baseURI).href;
      state.task = pdfjs.getDocument({data: data, cMapUrl: new URL('StreamingAssets/LibertyPdf/cmaps/', document.baseURI).href, cMapPacked: true, standardFontDataUrl: new URL('StreamingAssets/LibertyPdf/standard_fonts/', document.baseURI).href, wasmUrl: new URL('StreamingAssets/LibertyPdf/wasm/', document.baseURI).href});
      state.pdf = await state.task.promise;
      if (window.libertyPdfState === state) SendMessage(receiver, 'WebLoaded', String(state.pdf.numPages));
    }).catch(function(e) { if (window.libertyPdfState === state) SendMessage(receiver, 'WebError', e.message); });
  },
  LibertyPdfRender: function(index, width) {
    var state = window.libertyPdfState;
    if (!state || !state.pdf) return;
    (async function() {
      var page = await state.pdf.getPage(index + 1), base = page.getViewport({scale: 1});
      var scale = Math.min(width / base.width, 4096 / base.height);
      var viewport = page.getViewport({scale: scale}), canvas = document.createElement('canvas');
      canvas.width = Math.ceil(viewport.width); canvas.height = Math.ceil(viewport.height);
      await page.render({canvasContext: canvas.getContext('2d'), viewport: viewport}).promise;
      if (window.libertyPdfState === state) SendMessage(state.receiver, 'WebPage', canvas.toDataURL('image/png').split(',')[1]);
      page.cleanup(); canvas.width = canvas.height = 0;
    })().catch(function(e) { if (window.libertyPdfState === state) SendMessage(state.receiver, 'WebError', e.message); });
  },
  LibertyPdfClose: function() {
    var state = window.libertyPdfState; window.libertyPdfState = null;
    if (state && state.task) state.task.destroy();
  }
});
