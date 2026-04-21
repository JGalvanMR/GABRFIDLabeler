# ================================================
# RENOMBRADO AUTOMÁTICO - GABRFIDLabeler → GABRFIDLabeler
# ================================================

param(
    [string]$OldName = "GABRFIDLabeler",
    [string]$NewName = "GABRFIDLabeler"
)

$ErrorActionPreference = "Stop"
Write-Host "🚀 Iniciando renombrado: $OldName → $NewName" -ForegroundColor Cyan

# 1. Renombrar archivos y carpetas principales
# Agregamos un filtro para evitar carpetas de compilación innecesarias
Get-ChildItem -Recurse -Include "*$OldName*" | 
    Where-Object { $_.FullName -notmatch "(\\bin\\|\\obj\\|\\.git\\)" } | 
    Sort-Object FullName -Descending | ForEach-Object {
        $newNameFull = $_.Name -replace [regex]::Escape($OldName), $NewName
        Rename-Item -Path $_.FullName -NewName $newNameFull -Force
        Write-Host "✅ Renombrado: $($_.Name) → $newNameFull" -ForegroundColor Green
    }

# 2. Reemplazar texto dentro de los archivos
$filesToUpdate = Get-ChildItem -Recurse -Include "*.cs", "*.xaml", "*.csproj", "*.sln", "*.json", "*.xaml.cs" `
                 -Exclude "*Rename-Project.ps1*" | 
                 Where-Object { $_.FullName -notmatch "(\\bin\\|\\obj\\|\\.git\\)" }

foreach ($file in $filesToUpdate) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8

    # Diccionario corregido (SIN DUPLICADOS)
    $replacements = @{
        "clr-namespace:$OldName"                                 = "clr-namespace:$NewName"
        "xmlns:local=`"clr-namespace:$OldName`""                = "xmlns:local=`"clr-namespace:$NewName`""
        "xmlns:viewmodels=`"clr-namespace:$OldName.ViewModels`"" = "xmlns:viewmodels=`"clr-namespace:$NewName.ViewModels`""
        "<RootNamespace>$OldName</RootNamespace>"               = "<RootNamespace>$NewName</RootNamespace>"
        "com.companyname.GABRFIDLabeler"                          = "com.gab.irapuato.rfidlabeler"
        $OldName                                                = $NewName # Esta es la única instancia necesaria
    }

    foreach ($key in $replacements.Keys) {
        $content = $content -replace [regex]::Escape($key), $replacements[$key]
    }

    Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -Force
    Write-Host "🔄 Texto actualizado: $($file.Name)" -ForegroundColor Yellow
}

# 3. Actualizar ApplicationTitle y ApplicationId en el .csproj
$csproj = Get-ChildItem -Recurse -Filter "*$NewName.csproj" | Select-Object -First 1
if ($csproj) {
    $content = Get-Content $csproj.FullName -Raw
    $content = $content -replace '<ApplicationTitle>.*?</ApplicationTitle>', "<ApplicationTitle>GAB Etiquetador RFID</ApplicationTitle>"
    $content = $content -replace '<ApplicationId>.*?</ApplicationId>', '<ApplicationId>com.gab.irapuato.rfidlabeler</ApplicationId>'
    Set-Content $csproj.FullName -Value $content -Encoding UTF8
    Write-Host "📱 Metadata de App actualizada en .csproj" -ForegroundColor Cyan
}

Write-Host "`n✅ ¡Proceso finalizado!" -ForegroundColor Green