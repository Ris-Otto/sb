SETLOCAL ENABLEDELAYEDEXPANSION
set count=0
sb documents create loop-test "alla sånger" -p "C:\Test"
sb chapters add loop-test -n chp1 -t "Alla alla sånger"
for %%i in (TestFiles\*) do (
set /A "count+=1"
sb songs add loop-test chp1 "%%~ni" "%%i" !count!
)