; --- Win32Bridge: Automation & Constants (x64) ---
; Baseado nos metadados de: C:\Users\Morimenes W10\Documents\Win32Metadata\Win32MetadataProject\Win32Metadata.Exporter\output

class Win32Const {
}

class Win32Bridge {
    static SmartClick(x, y) {
        buf := Buffer(40, 0)
        mX := (x * 65536) // A_ScreenWidth
        mY := (y * 65536) // A_ScreenHeight
        NumPut('UInt', Win32Const.INPUT_MOUSE, buf, 0)
        NumPut('Int', mX, 'Int', mY, buf, 8)
        NumPut('UInt', Win32Const.MOUSEEVENTF_MOVE | Win32Const.MOUSEEVENTF_ABSOLUTE | Win32Const.MOUSEEVENTF_LEFTDOWN | Win32Const.MOUSEEVENTF_LEFTUP, buf, 20)
        return Win32User.SendInput(1, buf, buf.Size)
    }
    static GetPixelColor(pBitmap, x, y) {
        rect := Buffer(16, 0), NumPut('Int', x, 'Int', y, 'Int', 1, 'Int', 1, rect)
        bd := Buffer(32, 0) ; x64 BitmapData size
        Win32Gdiplus.GdipBitmapLockBits(pBitmap, rect, 1, 0x26200A, bd)
        pScan0 := NumGet(bd, 16, 'Ptr')
        color := NumGet(pScan0, 'UInt')
        Win32Gdiplus.GdipBitmapUnlockBits(pBitmap, bd)
        return color
    }
}
