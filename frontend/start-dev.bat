@echo off
set PATH=C:\Program Files\nodejs;%PATH%
cd /d C:\Users\kauag\Documents\GitHub\System-PVD\frontend
call npm run dev > vite-dev.log 2> vite-dev.err.log
