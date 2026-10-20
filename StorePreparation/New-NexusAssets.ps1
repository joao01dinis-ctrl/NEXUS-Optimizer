[CmdletBinding()]
param([Parameter(Mandatory)][string]$Destination)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetDirectory = [System.IO.Path]::GetFullPath($Destination)
[System.IO.Directory]::CreateDirectory($assetDirectory) | Out-Null
# Original geometric N, drawn locally. No competitor images or fonts are used.
foreach ($asset in @(
    @{ Name = 'Nexus44.png'; Size = 44 },
    @{ Name = 'Nexus150.png'; Size = 150 },
    @{ Name = 'StoreLogo50.png'; Size = 50 },
    @{ Name = 'StoreListing300.png'; Size = 300 }
)) {
    $scale = 4
    $canvasSize = [int]$asset.Size * $scale
    $bitmap = [System.Drawing.Bitmap]::new($canvasSize, $canvasSize)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(11, 19, 33))
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(57, 221, 185), [float]($canvasSize * 0.105))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $points = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([float]($canvasSize * 0.28), [float]($canvasSize * 0.73)),
        [System.Drawing.PointF]::new([float]($canvasSize * 0.28), [float]($canvasSize * 0.27)),
        [System.Drawing.PointF]::new([float]($canvasSize * 0.72), [float]($canvasSize * 0.73)),
        [System.Drawing.PointF]::new([float]($canvasSize * 0.72), [float]($canvasSize * 0.27))
    )
    $graphics.DrawLines($pen, $points)
    $small = [System.Drawing.Bitmap]::new([int]$asset.Size, [int]$asset.Size)
    $smallGraphics = [System.Drawing.Graphics]::FromImage($small)
    $smallGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $smallGraphics.DrawImage($bitmap, 0, 0, [int]$asset.Size, [int]$asset.Size)
    $small.Save([System.IO.Path]::Combine($assetDirectory, $asset.Name), [System.Drawing.Imaging.ImageFormat]::Png)
    $smallGraphics.Dispose(); $small.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
