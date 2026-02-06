using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Win32Metadata.AHKRuntimeGen;

class Program
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Int32", "Int" }, { "UInt32", "UInt" }, { "Int64", "Int64" }, { "UInt64", "Int64" },
        { "Bool", "Int" }, { "Boolean", "Int" }, { "HRESULT", "Int" },
        { "PCWSTR", "Str" }, { "PWSTR", "Str" }, { "LPCWSTR", "Str" },
        { "HDC", "Ptr" }, { "HWND", "Ptr" }, { "HBITMAP", "Ptr" }, { "HANDLE", "Ptr" }, { "HGDIOBJ", "Ptr" },
        { "PVOID", "Ptr" }, { "LPVOID", "Ptr" }
    };

    static string MapType(string win32Type)
    {
        string clean = win32Type.Trim();
        
        if (clean.Contains("*") || 
            ((clean.StartsWith("P") || clean.StartsWith("LP")) && 
            !clean.StartsWith("PCWSTR") && !clean.StartsWith("PWSTR") && !clean.StartsWith("LPCWSTR")))
        {
            return "Ptr";
        }

        string baseType = clean.Replace("*", "");
        if (TypeMap.TryGetValue(baseType, out string? ahkType)) 
        {
            return ahkType;
        }

        return "Ptr"; 
    }

    static int Main(string[] args)
    {
        try
        {
            string? currentDir = AppContext.BaseDirectory;
            string jsonDir = "";
            while (currentDir != null) {
                string potentialPath = Path.Combine(currentDir, "Win32Metadata.Exporter", "output");
                if (Directory.Exists(potentialPath)) { jsonDir = potentialPath; break; }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            if (string.IsNullOrEmpty(jsonDir)) {
                Console.WriteLine("Erro: Pasta 'output' do Exporter não encontrada.");
                return 1;
            }

            string outputFile = Path.Combine(Environment.CurrentDirectory, "Generated_Win32.ahk");

            if (args.Length == 0) {
                Console.WriteLine("Uso: AHKRuntimeGen.exe FuncName1 FuncName2 ...");
                return 1;
            }

            var requestedFunctions = new HashSet<string>(args, StringComparer.OrdinalIgnoreCase);
            StringBuilder ahk = new StringBuilder();
            ahk.AppendLine("; --- Win32Metadata.AHKRuntimeGen Engine ---");
            ahk.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

            InjectGdiplusManager(ahk);

            var dllGroups = new Dictionary<string, List<string>>();

            foreach (var file in Directory.GetFiles(jsonDir, "*.json"))
            {
                // Pular o arquivo de tipos globais para não confundir com definições de DLL
                if (Path.GetFileName(file).StartsWith("_Win32Types")) continue;

                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("Dll", out var dllProp)) continue;
                string currentDllFile = dllProp.GetString() ?? "unknown.dll";

                foreach (var func in root.GetProperty("Functions").EnumerateArray())
                {
                    string funcName = func.GetProperty("Name").GetString() ?? "";
                    if (requestedFunctions.Contains(funcName))
                    {
                        if (!dllGroups.ContainsKey(currentDllFile)) 
                            dllGroups[currentDllFile] = new List<string>();
                        
                        dllGroups[currentDllFile].Add(GenerateAhkMethod(func, currentDllFile));
                    }
                }
            }

            foreach (var entry in dllGroups)
            {
                string rawName = Path.GetFileNameWithoutExtension(entry.Key).ToLower();
                string formattedName = char.ToUpper(rawName[0]) + rawName.Substring(1).Replace("32", "");
                string className = $"Win32{formattedName}";

                ahk.AppendLine($"class {className} {{");
                foreach (var methodCode in entry.Value) ahk.AppendLine(methodCode.TrimEnd());
                ahk.AppendLine("}\n");
            }

            // --- PROCESSAMENTO DE CONSTANTES (ENUMS) ---
            ahk.AppendLine("class Win32Const {");

            var targetKeywords = new List<string> { 
                "PixelFormat", "Status", "ImageLockMode", "MOUSEEVENTF", "KEYBD", "INPUT_TYPE" 
            };

            string typesFile = Path.Combine(jsonDir, "_Win32Types.json");
            if (File.Exists(typesFile))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(typesFile));
                foreach (var typeProperty in doc.RootElement.EnumerateObject())
                {
                    string typeName = typeProperty.Name;
                    JsonElement details = typeProperty.Value;

                    // Uso de TryGetProperty para evitar KeyNotFoundException
                    if (details.TryGetProperty("Kind", out var kind) && kind.GetString() == "Enum")
                    {
                        if (targetKeywords.Any(k => typeName.Contains(k, StringComparison.OrdinalIgnoreCase)))
                        {
                            ahk.AppendLine($"    ; --- {typeName} ---");
                            
                            JsonElement members = default;
                            bool found = false;
                            // Tenta múltiplos nomes de propriedades comuns em serialização de Dicionários/Modelos
                            foreach (string possibleName in new[] { "Fields", "Values", "Members", "Data" })
                            {
                                if (details.TryGetProperty(possibleName, out members))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if (found && (members.ValueKind == JsonValueKind.Object || members.ValueKind == JsonValueKind.Array))
                            {
                                if (members.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (var member in members.EnumerateObject())
                                        ahk.AppendLine($"    static {member.Name} := {member.Value}");
                                }
                                else // Caso seja uma lista de objetos { Name, Value }
                                {
                                    foreach (var member in members.EnumerateArray())
                                    {
                                        string mName = member.GetProperty("Name").GetString()!;
                                        var mValue = member.GetProperty("Value");
                                        ahk.AppendLine($"    static {mName} := {mValue}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            ahk.AppendLine("}");

            File.WriteAllText(outputFile, ahk.ToString(), new UTF8Encoding(true));
            Console.WriteLine($"Sucesso! {requestedFunctions.Count} APIs e Enums processados.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro Crítico: {ex.Message}");
            File.WriteAllText("bridge_error.txt", ex.ToString());
            return -1;
        }
    }

    static string GenerateAhkMethod(JsonElement func, string dllPath)
    {
        string name = func.GetProperty("Name").GetString()!;
        var parameters = func.GetProperty("Parameters").EnumerateArray().ToList();
        string rawRetType = func.GetProperty("ReturnType").GetString() ?? "Int";
        string returnType = MapType(rawRetType);
        
        string pNames = string.Join(", ", parameters.Select(p => p.GetProperty("Name").GetString()));
        bool isGdip = dllPath.ToLower().Contains("gdiplus");

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"    static {name}({pNames}) {{");
        if (isGdip) sb.AppendLine("        Win32GdiplusManager.Start()");
        
        sb.Append($"        res := DllCall(\"{dllPath}\\{name}\"");
        foreach (var p in parameters)
        {
            string type = MapType(p.GetProperty("Type").GetString() ?? "Ptr");
            sb.Append($", \"{type}\", {p.GetProperty("Name").GetString()}");
        }
        sb.AppendLine($", \"{returnType}\")");
        
        if (rawRetType == "HRESULT")
        {
            sb.AppendLine("        if (res < 0) {");
            sb.AppendLine($"            throw Error('HRESULT Error: ' . Format('0x{{:X}}', res & 0xFFFFFFFF) . ' em {name}')");
            sb.AppendLine("        }");
        }

        if (isGdip && name != "GdiplusStartup")
        {
            sb.AppendLine("        if (res != 0) {");
            sb.AppendLine($"            throw Error('GDI+ Status Error: ' . res . ' em {name}')");
            sb.AppendLine("        }");
        }
                
        sb.AppendLine("        return res");
        sb.Append("    }"); 
        
        return sb.ToString();
    }

    static void InjectGdiplusManager(StringBuilder sb)
    {
        sb.AppendLine("class Win32GdiplusManager {");
        sb.AppendLine("    static _token := 0");
        sb.AppendLine("    static Start() {");
        sb.AppendLine("        if (this._token) {");
        sb.AppendLine("            return");
        sb.AppendLine("        }");
        sb.AppendLine("        si := Buffer(24, 0), NumPut('UInt', 1, si)");
        sb.AppendLine("        t := Buffer(8, 0)");
        sb.AppendLine("        DllCall('gdiplus\\GdiplusStartup', 'Ptr', t, 'Ptr', si, 'Ptr', 0)");
        sb.AppendLine("        this._token := NumGet(t, 'Ptr')");
        sb.AppendLine("    }");
        sb.AppendLine("}\n");
    }
}