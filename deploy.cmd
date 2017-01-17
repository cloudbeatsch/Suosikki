@if "%SCM_TRACE_LEVEL%" NEQ "4" @echo off

:: ----------------------
:: KUDU Deployment Script
:: Version: 1.0.8
:: ----------------------

:: Prerequisites
:: -------------

:: Verify node.js installed
where node 2>nul >nul
IF %ERRORLEVEL% NEQ 0 (
  echo Missing node.js executable, please install node.js, if already installed make sure it can be reached from current environment.
  goto error
)

:: Setup
:: -----

setlocal enabledelayedexpansion

SET ARTIFACTS=%~dp0%..\artifacts

IF NOT DEFINED DEPLOYMENT_SOURCE (
  SET DEPLOYMENT_SOURCE=%~dp0%.
)

IF NOT DEFINED DEPLOYMENT_TARGET (
  SET DEPLOYMENT_TARGET=%ARTIFACTS%\wwwroot
)

IF NOT DEFINED NEXT_MANIFEST_PATH (
  SET NEXT_MANIFEST_PATH=%ARTIFACTS%\manifest

  IF NOT DEFINED PREVIOUS_MANIFEST_PATH (
    SET PREVIOUS_MANIFEST_PATH=%ARTIFACTS%\manifest
  )
)

IF NOT DEFINED KUDU_SYNC_CMD (
  :: Install kudu sync
  echo Installing Kudu Sync
  call npm install kudusync -g --silent
  IF !ERRORLEVEL! NEQ 0 goto error

  :: Locally just running "kuduSync" would also work
  SET KUDU_SYNC_CMD=%appdata%\npm\kuduSync.cmd
)
IF NOT DEFINED DEPLOYMENT_TEMP (
  SET DEPLOYMENT_TEMP=%temp%\___deployTemp%random%
  SET CLEAN_LOCAL_DEPLOYMENT_TEMP=true
)

IF DEFINED CLEAN_LOCAL_DEPLOYMENT_TEMP (
  IF EXIST "%DEPLOYMENT_TEMP%" rd /s /q "%DEPLOYMENT_TEMP%"
  mkdir "%DEPLOYMENT_TEMP%"
)

IF DEFINED MSBUILD_PATH goto MsbuildPathDefined
SET MSBUILD_PATH=%ProgramFiles(x86)%\MSBuild\14.0\Bin\MSBuild.exe
:MsbuildPathDefined

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
:: Deployment
:: ----------

echo Handling .NET Console Application deployment.

:: 1. Restore NuGet packages
IF /I "Suosikki.WebJobs.sln" NEQ "" (
  call :ExecuteCmd nuget restore "%DEPLOYMENT_SOURCE%\Suosikki.WebJobs.sln"
  IF !ERRORLEVEL! NEQ 0 goto error
)

:: 2. Build to the temporary path
call :ExecuteCmd "%MSBUILD_PATH%" "%DEPLOYMENT_SOURCE%\jobs\continuous\Suosikki.WebJobs.FeatureCreator\Suosikki.WebJobs.FeatureCreator.csproj" /nologo /verbosity:m /t:Build /p:Configuration=Release;OutputPath="%DEPLOYMENT_TEMP%\app_data\jobs\continuous\Suosikki.WebJobs.FeatureCreator";UseSharedCompilation=false /p:SolutionDir="%DEPLOYMENT_SOURCE%\.\\" %SCM_BUILD_ARGS%
IF !ERRORLEVEL! NEQ 0 goto error

call :ExecuteCmd "%MSBUILD_PATH%" "%DEPLOYMENT_SOURCE%\jobs\continuous\Suosikki.WebJobs.CognitiveServices\Suosikki.WebJobs.CognitiveServices.csproj" /nologo /verbosity:m /t:Build /p:Configuration=Release;OutputPath="%DEPLOYMENT_TEMP%\app_data\jobs\continuous\Suosikki.WebJobs.CognitiveServices";UseSharedCompilation=false /p:SolutionDir="%DEPLOYMENT_SOURCE%\.\\" %SCM_BUILD_ARGS%
IF !ERRORLEVEL! NEQ 0 goto error

call :ExecuteCmd "%MSBUILD_PATH%" "%DEPLOYMENT_SOURCE%\jobs\triggered\Suosikki.WebJobs.UsageLoader\Suosikki.WebJobs.UsageLoader.csproj" /nologo /verbosity:m /t:Build /p:Configuration=Release;OutputPath="%DEPLOYMENT_TEMP%\app_data\jobs\triggered\Suosikki.WebJobs.UsageLoader";UseSharedCompilation=false /p:SolutionDir="%DEPLOYMENT_SOURCE%\.\\" %SCM_BUILD_ARGS%
IF !ERRORLEVEL! NEQ 0 goto error

call :ExecuteCmd "%MSBUILD_PATH%" "%DEPLOYMENT_SOURCE%\jobs\triggered\Suosikki.WebJobs.PrepareCognitiveServicesUpload\Suosikki.WebJobs.PrepareCognitiveServicesUpload.csproj" /nologo /verbosity:m /t:Build /p:Configuration=Release;OutputPath="%DEPLOYMENT_TEMP%\app_data\jobs\triggered\Suosikki.WebJobs.PrepareCognitiveServicesUpload";UseSharedCompilation=false /p:SolutionDir="%DEPLOYMENT_SOURCE%\.\\" %SCM_BUILD_ARGS%
IF !ERRORLEVEL! NEQ 0 goto error


:: 3. Run web job deploy script
IF DEFINED WEBJOBS_DEPLOY_CMD (
  call :ExecuteCmd "%WEBJOBS_DEPLOY_CMD%"
)

:: 4. KuduSync
call :ExecuteCmd "%KUDU_SYNC_CMD%" -v 50 -f "%DEPLOYMENT_TEMP%" -t "%DEPLOYMENT_TARGET%" -n "%NEXT_MANIFEST_PATH%" -p "%PREVIOUS_MANIFEST_PATH%" -i ".git;.hg;.deployment;deploy.cmd"
IF !ERRORLEVEL! NEQ 0 goto error

::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::::
goto end

:: Execute command routine that will echo out when error
:ExecuteCmd
setlocal
set _CMD_=%*
call %_CMD_%
if "%ERRORLEVEL%" NEQ "0" echo Failed exitCode=%ERRORLEVEL%, command=%_CMD_%
exit /b %ERRORLEVEL%

:error
endlocal
echo An error has occurred during web site deployment.
call :exitSetErrorLevel
call :exitFromFunction 2>nul

:exitSetErrorLevel
exit /b 1

:exitFromFunction
()

:end
endlocal
echo Finished successfully.