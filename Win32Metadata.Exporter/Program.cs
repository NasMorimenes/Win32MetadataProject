using System.Text.Json;
using Win32Metadata.Core;
using Win32Metadata.Core.Models;

// Caminho do arquivo .winmd na raiz da solução
string winmdPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Windows.Win32.winmd");
string outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");

if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

Console.WriteLine($"Lendo: {winmdPath}");
using var parser = new Win32Parser(winmdPath);
var dllGroups = new Dictionary<string, List<Win32Function>>();

foreach (var method in parser.EnumerateAllMethods())
{
    var function = parser.ExtractFunction(method);
    if (function == null) continue;

    if (!dllGroups.ContainsKey(function.Dll))
        dllGroups[function.Dll] = new List<Win32Function>();

    if (!dllGroups[function.Dll].Any(f => f.Name == function.Name))
        dllGroups[function.Dll].Add(function);
}

foreach (var group in dllGroups)
{
    string fileName = Path.Combine(outputDir, $"{group.Key}.json");
    var result = new { Dll = group.Key, Functions = group.Value, Types = parser.TypeCache };
    string json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(fileName, json);
    Console.WriteLine($"Gerado: {group.Key}.json");
}