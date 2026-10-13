SETLOCAL ENABLEDELAYEDEXPANSION
set count=0
sb-cli documents create vibe-test "alla sånger" -o "C:\VibeTest"
sb-cli chapters add vibe-test -n chapter1 -t "Alla alla sånger"
for %%i in (Sb.Tests\TestFiles\*.txt) do (
sb-cli songs add vibe-test chapter1 "%%~ni" "%%i" 
)