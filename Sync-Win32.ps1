Write-Host "--- Iniciando Sincronização Win32 Metadata ---" -ForegroundColor Cyan

# 1. Compilar toda a solução
Write-Host "`n[1/4] Compilando projetos..." -ForegroundColor Yellow
dotnet build Win32Metadata.slnx --configuration Release

# 2. Rodar o Indexer (Atualizar o banco SQL)
Write-Host "[2/4] Atualizando banco de dados SQL..." -ForegroundColor Yellow
cd Win32Metadata.Indexer
dotnet run --configuration Release
cd ..

# 3. Rodar o Bridge (Gerar constantes e Helpers de Automação)
Write-Host "[3/4] Gerando Bridge (Constantes e Helpers)..." -ForegroundColor Yellow
cd Win32Metadata.Win32Bridge
dotnet run --configuration Release
cd ..

# 4. Finalizar
Write-Host "`n[SUCESSO] Ambiente Win32 pronto para uso no AutoHotkey!" -ForegroundColor Green
Write-Host "Arquivos gerados:"
Write-Host " - win32api.db (Banco de Dados)"
Write-Host " - Win32Metadata.Win32Bridge/Generated_Bridge.ahk"