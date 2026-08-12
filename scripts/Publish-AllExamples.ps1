[CmdletBinding()]
param(
    [string] $Runtime,

    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $FrameworkDependent,

    [switch] $NoRestore
)

$parameters = @{
    Runtime = $Runtime
    Configuration = $Configuration
    FrameworkDependent = $FrameworkDependent
    NoRestore = $NoRestore
}

& "$PSScriptRoot\Publish-ExampleGame.ps1" @parameters
& "$PSScriptRoot\Publish-MandelbrotExplorer.ps1" @parameters
