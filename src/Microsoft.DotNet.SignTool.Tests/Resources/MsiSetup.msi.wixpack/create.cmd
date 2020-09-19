@echo off
setlocal
set outputfolder=%1
if "%outputfolder%" NEQ "" (
  if "%outputfolder:~-1%" NEQ "\" ( 
    set outputfolder=%outputfolder%\
  )
)
light.exe -out %outputfolder%MsiSetup.msi -pdbout MsiSetup.wixpdb -cultures:null Product.wixobj
