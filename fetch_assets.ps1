$ErrorActionPreference = "Stop"
$outDir = "C:\Users\SHAFIQ-JKMNT\.gemini\antigravity\scratch\waktu-solat-screensaver\wallpapers"
$userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"

$mosques = @(
    @{ id = "masjid-putra"; query = "Putra Mosque Putrajaya" },
    @{ id = "masjid-selat-melaka"; query = "Malacca Straits Mosque sunset" },
    @{ id = "masjid-kristal"; query = "Crystal Mosque Terengganu" },
    @{ id = "masjid-wilayah"; query = "Federal Territory Mosque Kuala Lumpur" },
    @{ id = "masjid-shah-alam"; query = "Sultan Salahuddin Abdul Aziz Mosque Shah Alam" },
    @{ id = "masjid-ubudiah"; query = "Ubudiah Mosque Kuala Kangsar" },
    @{ id = "masjid-kk"; query = "Kota Kinabalu City Mosque" },
    @{ id = "masjid-negara"; query = "National Mosque of Malaysia Kuala Lumpur" }
)

foreach ($m in $mosques) {
    $dest = Join-Path $outDir "$($m.id).jpg"
    if ((Test-Path $dest) -and ((Get-Item $dest).Length -gt 50000)) {
        Write-Host "Already have $($m.id).jpg ($((Get-Item $dest).Length) bytes)"
        continue
    }
    $encoded = [uri]::EscapeDataString("$($m.query) filetype:bitmap")
    $apiUrl = "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch=$encoded&gsrnamespace=6&gsrlimit=15&prop=imageinfo&iiprop=url|dimensions&iiurlwidth=1920&format=json"
    Write-Host "Searching Wikimedia for $($m.id)..."
    $jsonRaw = curl.exe -s -A $userAgent $apiUrl
    $data = $jsonRaw | ConvertFrom-Json
    $pages = $data.query.pages.PSObject.Properties.Value
    $chosenUrl = $null
    foreach ($p in $pages) {
        if ($p.imageinfo -and $p.imageinfo.Count -gt 0) {
            $info = $p.imageinfo[0]
            if ($info.width -ge 2000 -and $info.width -gt $info.height -and $info.thumburl -like "https://thumb.wikimedia.org/*") {
                $chosenUrl = ($info.thumburl -split "\?")[0]
                break
            }
        }
    }
    if ($chosenUrl) {
        Write-Host "Downloading $($m.id) from $chosenUrl"
        curl.exe -s -L -A $userAgent -o $dest $chosenUrl
    }
}
Write-Host "Done!"
