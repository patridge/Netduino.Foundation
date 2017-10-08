param([String]$path="")

write-host($path)

#$path = "C:\Users\Administrator\dev\gh-lame\Netduino.Foundation\Source\Netduino.Foundation\Properties\AssemblyInfo.cs"
$pattern = '\[assembly: AssemblyVersion\("(.*)"\)\]'

(Get-Content $path) | ForEach-Object{
    if($_ -match $pattern){
        # We have found the matching line
        # Edit the version number and put back.
        $fileVersion = [version]$matches[1]
        $newVersion = "{0}.{1}.{2}.{3}" -f $fileVersion.Major, $fileVersion.Minor, ($fileVersion.Build+1), $fileVersion.Revision
        '[assembly: AssemblyVersion("{0}")]' -f $newVersion
    } else {
        # Output line as is
        $_
    }
} | Set-Content $path