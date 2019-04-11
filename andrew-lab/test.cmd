@echo off
echo Testing parse.cmd > ./actual.cmd.txt
call parse.cmd                                 >> ./actual.cmd.txt
call parse.cmd -a                              >> ./actual.cmd.txt
call parse.cmd --a                             >> ./actual.cmd.txt
call parse.cmd -parameter-a                    >> ./actual.cmd.txt
call parse.cmd --parameter-a                   >> ./actual.cmd.txt
call parse.cmd -a x                            >> ./actual.cmd.txt
call parse.cmd --a x                           >> ./actual.cmd.txt
call parse.cmd -parameter-a x                  >> ./actual.cmd.txt
call parse.cmd --parameter-a x                 >> ./actual.cmd.txt
call parse.cmd -a x --a x                      >> ./actual.cmd.txt
call parse.cmd --a x -a x                      >> ./actual.cmd.txt
call parse.cmd -parameter-a x -parameter-a x   >> ./actual.cmd.txt
call parse.cmd --parameter-a x --parameter-a x >> ./actual.cmd.txt
