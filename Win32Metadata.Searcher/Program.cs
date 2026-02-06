using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// 1. Caminho para o banco gerado pelo Indexer
string? currentDir = AppContext.BaseDirectory;
string dbPath = "";

while (currentDir != null)
{
    string potentialPath = Path.Combine(currentDir, "Win32Metadata.Indexer", "win32api.db");
    if (File.Exists(potentialPath))
    {
        dbPath = potentialPath;
        break;
    }
    currentDir = Directory.GetParent(currentDir)?.FullName;
}

if (string.IsNullOrEmpty(dbPath)) {
    Console.WriteLine($"[Erro] Banco de dados 'win32api.db' não foi encontrado em nenhuma pasta superior.");
    return;
}

if (!File.Exists(dbPath)) {
    Console.WriteLine($"[Erro] Banco de dados não encontrado em: {dbPath}");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

Console.WriteLine("=== WIN32 API EXPLORER (SQL ENGINE) ===");
Console.WriteLine("Digite parte do nome de uma função (ex: CreateWindow) ou 'sair'");

while (true)
{
    Console.Write("\nBusca: ");
    string query = Console.ReadLine() ?? "";
    if (query.ToLower() == "sair") break;
    if (string.IsNullOrWhiteSpace(query)) continue;

    // Busca funções que combinam com o termo
    var cmd = connection.CreateCommand();
    cmd.CommandText = "SELECT Id, Name, Dll, ReturnType FROM Functions WHERE Name LIKE $name LIMIT 5";
    cmd.Parameters.AddWithValue("$name", $"%{query}%");

    using var reader = cmd.ExecuteReader();
    if (!reader.HasRows) {
        Console.WriteLine("[-] Nenhuma função encontrada.");
        continue;
    }

    while (reader.Read())
    {
        int id = reader.GetInt32(0);
        string name = reader.GetString(1);
        string dll = reader.GetString(2);
        string ret = reader.GetString(3);

        Console.WriteLine($"\n>>> {name} ({dll}.dll)");
        Console.WriteLine($"    Retorno: {ret}");

        // Busca Parâmetros
        var pCmd = connection.CreateCommand();
        pCmd.Parameters.AddWithValue("$fid", id);
        pCmd.CommandText = "SELECT Name, Type, IsPointer FROM Parameters WHERE FuncId = $fid";
        
        var typesToResolve = new HashSet<string>();
        using var pReader = pCmd.ExecuteReader();
        Console.WriteLine("    Parâmetros:");
        while (pReader.Read()) {
            string pName = pReader.GetString(0);
            string pType = pReader.GetString(1);
            Console.WriteLine($"      - {pName.PadRight(15)} : {pType}");
            typesToResolve.Add(pType.Replace("*", ""));
        }

        // Resolução de Tipos Complexos
        foreach (var tName in typesToResolve) {
            var tCmd = connection.CreateCommand();
            tCmd.Parameters.AddWithValue("$tname", tName);
            tCmd.CommandText = "SELECT Kind, JsonData FROM TypeData WHERE Name = $tname";
            using var tReader = tCmd.ExecuteReader();
            if (tReader.Read()) {
                string kind = tReader.GetString(0);
                var data = JsonSerializer.Deserialize<JsonElement>(tReader.GetString(1));
                Console.WriteLine($"\n    [Tipo: {tName} ({kind})]");
                foreach (var prop in data.EnumerateObject().Take(10)) 
                    Console.WriteLine($"      {prop.Name}: {prop.Value}");
            }
        }
        Console.WriteLine(new string('-', 50));
    }
}