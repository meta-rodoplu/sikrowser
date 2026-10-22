Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\ayber\.gemini\antigravity\scratch\light-browser\assets\sikrowser_logo.jpg"
$icoPath = "C:\Users\ayber\.gemini\antigravity\scratch\light-browser\assets\sikrowser.ico"
$pngPath = "C:\Users\ayber\.gemini\antigravity\scratch\light-browser\assets\sikrowser.png"

$srcBmp = [System.Drawing.Bitmap]::FromFile($srcPath)
$srcBmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$sizes = @(256, 128, 64, 48, 32, 16)
$pngStreams = @()

foreach ($size in $sizes) {
    $resized = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($resized)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($srcBmp, 0, 0, $size, $size)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $resized.Dispose()
    $pngStreams += $ms
}

$srcBmp.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR header (6 bytes)
$bw.Write([UInt16]0) # Reserved
$bw.Write([UInt16]1) # Type 1 = Icon
$bw.Write([UInt16]$sizes.Length) # Image count

$headerSize = 6 + ($sizes.Length * 16)
$currentOffset = $headerSize

for ($i = 0; $i -lt $sizes.Length; $i++) {
    $s = $sizes[$i]
    $dataLen = [UInt32]$pngStreams[$i].Length
    $bWidth = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $bHeight = if ($s -ge 256) { [byte]0 } else { [byte]$s }

    $bw.Write($bWidth)
    $bw.Write($bHeight)
    $bw.Write([byte]0) # Color count
    $bw.Write([byte]0) # Reserved
    $bw.Write([UInt16]1) # Planes
    $bw.Write([UInt16]32) # Bit count
    $bw.Write($dataLen)
    $bw.Write([UInt32]$currentOffset)

    $currentOffset += $dataLen
}

for ($i = 0; $i -lt $sizes.Length; $i++) {
    $bytes = $pngStreams[$i].ToArray()
    $bw.Write($bytes)
    $pngStreams[$i].Dispose()
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "SUCCESS: Generated ICO at $icoPath with size $((Get-Item $icoPath).Length) bytes"
