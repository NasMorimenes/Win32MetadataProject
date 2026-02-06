using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// 1. Localizar o Banco de Dados
string? currentDir = AppContext.BaseDirectory;
string dbPath = "";
while (currentDir != null) {
    string potentialPath = Path.Combine(currentDir, "Win32Metadata.Indexer", "win32api.db");
    if (File.Exists(potentialPath)) { dbPath = potentialPath; break; }
    currentDir = Directory.GetParent(currentDir)?.FullName;
}

if (string.IsNullOrEmpty(dbPath)) {
    Console.WriteLine("Banco de dados não encontrado!");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

string outputFile = "Win32Lib.ahk";
StringBuilder ahk = new StringBuilder();
ahk.AppendLine("; --- Win32 Master Library for AHK v2 ---");
ahk.AppendLine($"; Generated: {DateTime.Now:yyyy-MM-dd}");

// 2. Buscar todas as funções
var allFunctions = new List<Win32FuncRow>();
var funcCmd = connection.CreateCommand();
funcCmd.CommandText = "SELECT Id, Name, Dll, ReturnType FROM Functions";

using (var reader = funcCmd.ExecuteReader()) {
    while (reader.Read()) {
        allFunctions.Add(new Win32FuncRow(
            reader.GetInt32(0), 
            reader.GetString(1), 
            reader.GetString(2), 
            reader.GetString(3)
        ));
    }
}

// 3. Agrupar e Deduplicar
var groups = allFunctions
    .GroupBy(f => f.Dll)
    .ToDictionary(
        g => g.Key, 
        g => g.GroupBy(f => {
            if ((f.Name.EndsWith("A") || f.Name.EndsWith("W")) && f.Name.Length > 1)
                return f.Name.Substring(0, f.Name.Length - 1);
            return f.Name;
        })
        .Select(group => group.OrderByDescending(f => f.Name.EndsWith("W")).First())
        .ToList()
    );

// 4. Gerar Classes AHK
foreach (var dllGroup in groups) {
    string className = "Win32" + char.ToUpper(dllGroup.Key[0]) + dllGroup.Key.Substring(1);
    ahk.AppendLine($"class {className} {{");

    foreach (var func in dllGroup.Value) {
        var pCmd = connection.CreateCommand();
        pCmd.Parameters.AddWithValue("$fid", func.Id);
        pCmd.CommandText = "SELECT Name, Type, IsPointer FROM Parameters WHERE FuncId = $fid";
        
        List<string> pNames = new();
        List<string> pCalls = new();
        
        using (var pReader = pCmd.ExecuteReader()) {
            while (pReader.Read()) {
                string pName = pReader.GetString(0);
                string pType = pReader.GetString(1);
                bool isPtr = pReader.GetInt32(2) == 1;
                
                pNames.Add(pName);
                pCalls.Add($"\"{GetAhkType(pType, isPtr)}\"");
                pCalls.Add(pName);
            }
        }

        string ahkParams = string.Join(", ", pNames);
        string ahkCall = pCalls.Count > 0 ? ", " + string.Join(", ", pCalls) : "";
        string retType = GetAhkType(func.Ret, false);

        ahk.AppendLine($"    static {func.Name}({ahkParams}) => DllCall(\"{func.Dll}\\{func.Name}\"{ahkCall}, \"{retType}\")");
    }
    ahk.AppendLine("}\n");
}

File.WriteAllText(outputFile, ahk.ToString(), Encoding.UTF8);
Console.WriteLine($"\n[Sucesso] Biblioteca '{outputFile}' gerada com sucesso!");

// AUXILIARES (Devem ficar no final do arquivo)
static string GetAhkType(string t, bool isPtr) {
    if (isPtr) return t.Contains("STR") ? "Str" : "Ptr";
    return t.ToLower() switch {
        "int32" or "bool" or "hresult" => "Int",
        "uint32" => "UInt",
        "int64" => "Int64",
        "uint64" => "UInt64",
        _ => "Ptr"
    };
}

public record Win32FuncRow(int Id, string Name, string Dll, string Ret);