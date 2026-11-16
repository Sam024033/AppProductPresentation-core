package com.liberty3d.viewer;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.pdf.PdfRenderer;
import android.os.ParcelFileDescriptor;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;

/** Local PDF rasterization; no network, one page in memory at a time. */
public final class PdfBridge {
    private static PdfRenderer renderer;
    private static ParcelFileDescriptor descriptor;
    public static int open(String path) throws IOException {
        close();
        descriptor = ParcelFileDescriptor.open(new File(path), ParcelFileDescriptor.MODE_READ_ONLY);
        renderer = new PdfRenderer(descriptor);
        return renderer.getPageCount();
    }
    public static byte[] render(int index, int width) throws IOException {
        PdfRenderer.Page page = renderer.openPage(index);
        Bitmap bitmap = null;
        try {
            int height = Math.max(1, (int)((double)width * page.getHeight() / page.getWidth()));
            // Bound memory for unusually tall technical drawings.
            if (height > 4096) { width = Math.max(1, width * 4096 / height); height = 4096; }
            bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888);
            new Canvas(bitmap).drawColor(Color.WHITE);
            page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY);
            ByteArrayOutputStream output = new ByteArrayOutputStream();
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, output);
            return output.toByteArray();
        } finally { page.close(); if (bitmap != null) bitmap.recycle(); }
    }
    public static void close() throws IOException {
        if (renderer != null) { renderer.close(); renderer = null; }
        if (descriptor != null) { descriptor.close(); descriptor = null; }
    }
}
