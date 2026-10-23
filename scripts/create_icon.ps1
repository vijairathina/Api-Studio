param(
    [string]$sourceImagePath,
    [string]$outputDir
)

Add-Type -AssemblyName System.Drawing

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$srcBmp = [System.Drawing.Bitmap]::FromFile($sourceImagePath)

# Save high-res PNG
$pngPath = Join-Path $outputDir "app_icon.png"
$srcBmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved PNG to $pngPath"

# Sizes for ICO
$sizes = @(16, 32, 48, 64, 128, 256)
$pngBytesList = @()

foreach ($sz in $sizes) {
    $resized = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($srcBmp, 0, 0, $sz, $sz)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    $resized.Dispose()

    $pngBytesList += ,@($sz, $bytes)
}

$srcBmp.Dispose()

# Write ICO file
$icoPath = Join-Path $outputDir "app_icon.ico"
$fs = [System.IO.File]::Open($icoPath, [System.IO.FileMode]::Create)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0) # Reserved
$bw.Write([uint16]1) # Type (1=Icon)
$bw.Write([uint16]$pngBytesList.Count) # Count

$offset = 6 + (16 * $pngBytesList.Count)

for ($i = 0; $i -lt $pngBytesList.Count; $i++) {
    $sz = $pngBytesList[$i][0]
    $data = $pngBytesList[$i][1]
    
    $w = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $h = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0) # Color count
    $bw.Write([byte]0) # Reserved
    $bw.Write([uint16]1) # Color planes
    $bw.Write([uint16]32) # Bits per pixel
    $bw.Write([uint32]$data.Length) # Image size
    $bw.Write([uint32]$offset) # Image offset
    
    $offset += $data.Length
}

foreach ($item in $pngBytesList) {
    $bw.Write($item[1])
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Successfully generated ICO at $icoPath"
