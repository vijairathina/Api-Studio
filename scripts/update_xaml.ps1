$files = @(
    "src\ApiStudio.Presentation\MainWindow.xaml",
    "src\ApiStudio.Presentation\Views\Controls\SettingsWindow.xaml"
)

$replacements = @{
    '{StaticResource GlassPanelGradient}' = '{DynamicResource GlassPanelGradient}'
    '{StaticResource GlassBorderGradient}' = '{DynamicResource GlassBorderGradient}'
    '{StaticResource GlassCardBrush}' = '{DynamicResource GlassCardBrush}'
    '{StaticResource GlassSubtleBrush}' = '{DynamicResource GlassSubtleBrush}'
    '{StaticResource GlassCardShadow}' = '{DynamicResource GlassCardShadow}'
    '{StaticResource GlassSubtleShadow}' = '{DynamicResource GlassSubtleShadow}'
    '{StaticResource PrimaryTextBrush}' = '{DynamicResource PrimaryTextBrush}'
    '{StaticResource SecondaryTextBrush}' = '{DynamicResource SecondaryTextBrush}'
    '{StaticResource HoverBackgroundBrush}' = '{DynamicResource HoverBackgroundBrush}'
    '{StaticResource BorderBrush}' = '{DynamicResource BorderBrush}'
    '{StaticResource WindowBackgroundBrush}' = '{DynamicResource WindowBackgroundBrush}'
}

foreach ($f in $files) {
    if (Test-Path $f) {
        $text = [System.IO.File]::ReadAllText($f)
        foreach ($key in $replacements.Keys) {
            $text = $text.Replace($key, $replacements[$key])
        }
        [System.IO.File]::WriteAllText($f, $text)
        Write-Host "Updated $f"
    }
}
