#Include Generated_Win32.ahk

; Exemplo 1: Movendo o mouse usando a classe gerada
Win32User.SetCursorPos(500, 500)

; Exemplo 2: Usando uma constante que o gerador extraiu
if (Win32Const.INPUT_MOUSE == 0) {
    MsgBox "O tipo de input para mouse é 0, como confirmado pelos metadados!"
}

; Exemplo 3: Capturando um erro de GDI+ (que o gerador configurou para dar throw)
try {
    ; Tentar destravar um bitmap sem ter travado antes
    Win32Gdiplus.GdipBitmapUnlockBits(0, 0)
} catch Error as e {
    MsgBox "Erro capturado: " . e.Message
}
