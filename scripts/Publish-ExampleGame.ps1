[CmdletBinding()]
param(
    [string] $Runtime = 'win-x64',

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $OutputDirectory,

    [switch] $FrameworkDependent,

    [switch] $NoRestore
)

$parameters = @{
    ProjectPath = 'Example-Game\Example-Game.csproj'
    ApplicationName = 'Example-Game'
    Runtime = $Runtime
    Configuration = $Configuration
    FrameworkDependent = $FrameworkDependent
    NoRestore = $NoRestore
}
if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $parameters.OutputDirectory = $OutputDirectory
}

& "$PSScriptRoot\Publish-SingleFile.ps1" @parameters

