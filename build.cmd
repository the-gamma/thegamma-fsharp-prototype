@echo off
cd data
call build.cmd %*
cd ..\web
call build.cmd %*
cd ..
