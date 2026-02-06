using System;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

// 1. Caminhos Relativos (Ajustados para rodar via dotnet run)
string jsonDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Win32Metadata.Exporter", "output");
string dbPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "win32api.db");

if (!Directory.Exists(jsonDir))
{
    Console.WriteLine($"[Erro] Pasta de JSONs não encontrada em: {Path.GetFullPath(jsonDir)}");
    return;
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

// 2. Criação das Tabelas (O "Esquema" do Banco)
Console.WriteLine("Criando tabelas no banco de dados...");
var createTables = @"
    CREATE TABLE IF NOT EXISTS Functions (Id INTEGER PRIMARY KEY, Name TEXT, Dll TEXT, ReturnType TEXT);
    CREATE TABLE IF NOT EXISTS Parameters (Id INTEGER PRIMARY KEY, FuncId INTEGER, Name TEXT, Type TEXT, IsPointer INTEGER);
    CREATE TABLE IF NOT EXISTS TypeData (Name TEXT PRIMARY KEY, Kind TEXT, JsonData TEXT);
    CREATE INDEX IF NOT EXISTS idx_func_name ON Functions(Name);";
new SqliteCommand(createTables, connection).ExecuteNonQuery();

// Otimização para escrita rápida
new SqliteCommand("PRAGMA synchronous = OFF; PRAGMA journal_mode = MEMORY;", connection).ExecuteNonQuery();

using var transaction = connection.BeginTransaction();

// 3. Processamento dos Arquivos
string[] files = Directory.GetFiles(jsonDir, "*.json");
Console.WriteLine($"Indexando {files.Length} arquivos JSON...");

foreach (var file in files)
{
    var data = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file));
    string dllName = data.GetProperty("Dll").GetString() ?? "Unknown";

    // Indexar Tipos (Structs/Enums)
    foreach (var type in data.GetProperty("Types").EnumerateObject())
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO TypeData (Name, Kind, JsonData) VALUES ($name, $kind, $data)";
        cmd.Parameters.AddWithValue("$name", type.Name);
        cmd.Parameters.AddWithValue("$kind", type.Value.GetProperty("Kind").GetString());
        cmd.Parameters.AddWithValue("$data", type.Value.GetProperty("Data").GetRawText());
        cmd.ExecuteNonQuery();
    }

    // Indexar Funções
    foreach (var func in data.GetProperty("Functions").EnumerateArray())
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Functions (Name, Dll, ReturnType) VALUES ($name, $dll, $ret); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$name", func.GetProperty("Name").GetString());
        cmd.Parameters.AddWithValue("$dll", dllName);
        cmd.Parameters.AddWithValue("$ret", func.GetProperty("ReturnType").GetString());
        
        long funcId = (long)cmd.ExecuteScalar()!;

        // Indexar Parâmetros
        foreach (var p in func.GetProperty("Parameters").EnumerateArray())
        {
            var pCmd = connection.CreateCommand();
            pCmd.CommandText = "INSERT INTO Parameters (FuncId, Name, Type, IsPointer) VALUES ($fid, $name, $type, $ptr)";
            pCmd.Parameters.AddWithValue("$fid", funcId);
            pCmd.Parameters.AddWithValue("$name", p.GetProperty("Name").GetString());
            pCmd.Parameters.AddWithValue("$type", p.GetProperty("Type").GetString());
            pCmd.Parameters.AddWithValue("$ptr", p.GetProperty("IsPointer").GetBoolean() ? 1 : 0);
            pCmd.ExecuteNonQuery();
        }
    }
}

transaction.Commit();
Console.WriteLine($"\n[Sucesso] Banco de dados '{dbPath}' gerado com sucesso!");