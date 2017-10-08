powershell.exe .\build\update-build-number.ps1 .\Netduino.Foundation\Properties\AssemblyInfo.cs
"C:\Program Files (x86)\MSBuild\14.0\Bin\msbuild" .\Netduino.Foundation\Netduino.Foundation.csproj
nuget pack Netduino.Foundation/Netduino.Foundation.csproj
REM nuget push Netduino.Foundation/WildernessLabs.Netduino.Foundation.0.0.3.nupkg -Source https://nuget.org -ApiKey de0f0070-0128-47b8-9bb5-cc745eee32f0