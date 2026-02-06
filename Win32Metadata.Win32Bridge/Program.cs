using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace Win32Metadata.Win32Bridge;

class Program
{
    private const int INPUT_STRUCT_SIZE = 40; 
    private const int MOUSEINPUT_OFFSET = 8;  

    static int Main(string[] args)
    {
        // Localizar a pasta output do Exporter dinamicamente
        string? currentDir = AppContext.BaseDirectory;
        string jsonDir = "";
        while (currentDir != null) {
            string potentialPath = Path.Combine(currentDir, "Win32Metadata.Exporter", "output");
            if (Directory.Exists(potentialPath)) { jsonDir = potentialPath; break; }
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }

        if (string.IsNullOrEmpty(jsonDir)) {
            Console.WriteLine("Erro: Pasta de metadados não encontrada.");
            return 1;
        }

        string outputFile = Path.Combine(Environment.CurrentDirectory, "Generated_Bridge.ahk");

        StringBuilder ahk = new StringBuilder();
        ahk.AppendLine("; --- Win32Bridge: Automation & Constants (x64) ---");
        ahk.AppendLine($"; Baseado nos metadados de: {jsonDir}\n");

        var enumsToExtract = new List<string> { 
            "MOUSEEVENTF", "KEYBDEVENTF", "INPUT_TYPE", "GdiplusStatus" 
        };
        
        GenerateDynamicConstants(ahk, jsonDir, enumsToExtract);
        GenerateHighLevelHelpers(ahk);

        File.WriteAllText(outputFile, ahk.ToString(), Encoding.UTF8);
        Console.WriteLine($"Ponte Win32Bridge construída em: {outputFile}");
        return 0;
    }

    static void GenerateDynamicConstants(StringBuilder sb, string jsonDir, List<string> enumNames)
    {
        sb.AppendLine("class Win32Const {");
        foreach (var file in Directory.GetFiles(jsonDir, "*.json"))
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            if (doc.RootElement.TryGetProperty("Enums", out var enums))
            {
                foreach (var e in enums.EnumerateObject()) // Ajustado para ler o dicionário de enums
                {
                    if (enumNames.Contains(e.Name))
                    {
                        sb.AppendLine($"    ; --- {e.Name} ---");
                        var data = e.Value.GetProperty("Data");
                        foreach (var member in data.EnumerateObject())
                        {
                            sb.AppendLine($"    static {member.Name} := {member.Value}");
                        }
                    }
                }
            }
        }
        sb.AppendLine("}\n");
    }

    static void GenerateHighLevelHelpers(StringBuilder sb)
    {
        sb.AppendLine("class Win32Bridge {");
        
        sb.AppendLine("    static SmartClick(x, y) {");
        sb.AppendLine($"        buf := Buffer({INPUT_STRUCT_SIZE}, 0)");
        sb.AppendLine("        mX := (x * 65536) // A_ScreenWidth");
        sb.AppendLine("        mY := (y * 65536) // A_ScreenHeight");
        sb.AppendLine("        NumPut('UInt', Win32Const.INPUT_MOUSE, buf, 0)");
        sb.AppendLine($"        NumPut('Int', mX, 'Int', mY, buf, {MOUSEINPUT_OFFSET})");
        sb.AppendLine($"        NumPut('UInt', Win32Const.MOUSEEVENTF_MOVE | Win32Const.MOUSEEVENTF_ABSOLUTE | Win32Const.MOUSEEVENTF_LEFTDOWN | Win32Const.MOUSEEVENTF_LEFTUP, buf, {MOUSEINPUT_OFFSET + 12})");
        sb.AppendLine("        return Win32User.SendInput(1, buf, buf.Size)");
        sb.AppendLine("    }");

        sb.AppendLine("    static GetPixelColor(pBitmap, x, y) {");
        sb.AppendLine("        rect := Buffer(16, 0), NumPut('Int', x, 'Int', y, 'Int', 1, 'Int', 1, rect)");
        sb.AppendLine("        bd := Buffer(32, 0) ; x64 BitmapData size");
        sb.AppendLine("        Win32Gdiplus.GdipBitmapLockBits(pBitmap, rect, 1, 0x26200A, bd)");
        sb.AppendLine("        pScan0 := NumGet(bd, 16, 'Ptr')");
        sb.AppendLine("        color := NumGet(pScan0, 'UInt')");
        sb.AppendLine("        Win32Gdiplus.GdipBitmapUnlockBits(pBitmap, bd)");
        sb.AppendLine("        return color");
        sb.AppendLine("    }");
        
        sb.AppendLine("}");
    }
}