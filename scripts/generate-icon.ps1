$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $repoRoot "assets"
New-Item -ItemType Directory -Path $assets -Force | Out-Null

$pngPath = Join-Path $assets "AppIcon.png"
$icoPath = Join-Path $assets "AppIcon.ico"

$size = 256
$bitmap = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

$rect = New-Object System.Drawing.Rectangle 24, 24, 208, 208
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(37, 99, 235)), ([System.Drawing.Color]::FromArgb(20, 184, 166)), 45

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$diameter = 112
$path.AddArc($rect.Left, $rect.Top, $diameter, $diameter, 180, 90)
$path.AddArc($rect.Right - $diameter, $rect.Top, $diameter, $diameter, 270, 90)
$path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
$path.AddArc($rect.Left, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
$path.CloseFigure()
$graphics.FillPath($brush, $path)

$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), 18
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

$graphics.DrawLine($pen, 160, 67, 198, 67)
$graphics.DrawLine($pen, 198, 67, 198, 105)
$graphics.DrawLine($pen, 96, 189, 58, 189)
$graphics.DrawLine($pen, 58, 189, 58, 151)
$graphics.DrawArc($pen, 58, 58, 140, 140, 193, 188)
$graphics.DrawArc($pen, 58, 58, 140, 140, 13, 188)

$graphics.FillEllipse([System.Drawing.Brushes]::White, 103, 103, 50, 50)
$centerBrush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(37, 99, 235))
$graphics.FillEllipse($centerBrush, 116, 116, 24, 24)

$bitmap.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$pngBytes = [System.IO.File]::ReadAllBytes($pngPath)
$stream = [System.IO.File]::Create($icoPath)
$writer = New-Object System.IO.BinaryWriter $stream
try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]1)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([Byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$pngBytes.Length)
    $writer.Write([UInt32]22)
    $writer.Write($pngBytes)
}
finally {
    $writer.Dispose()
    $stream.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
    $brush.Dispose()
    $path.Dispose()
    $pen.Dispose()
    $centerBrush.Dispose()
}

Write-Host "Generated $icoPath"
