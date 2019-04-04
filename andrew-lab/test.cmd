@echo off
echo Testing parse.cmd > ./actual.cmd.txt
call parse.cmd                                     >> ./actual.cmd.txt
call parse.cmd -c                                  >> ./actual.cmd.txt
call parse.cmd --c                                 >> ./actual.cmd.txt
call parse.cmd -configuration                      >> ./actual.cmd.txt
call parse.cmd --configuration                     >> ./actual.cmd.txt
call parse.cmd -c x                                >> ./actual.cmd.txt
call parse.cmd --c x                               >> ./actual.cmd.txt
call parse.cmd -configuration x                    >> ./actual.cmd.txt
call parse.cmd --configuration x                   >> ./actual.cmd.txt
call parse.cmd -c x --c x                          >> ./actual.cmd.txt
call parse.cmd --c x -c x                          >> ./actual.cmd.txt
call parse.cmd -configuration x -configuration x   >> ./actual.cmd.txt
call parse.cmd --configuration x --configuration x >> ./actual.cmd.txt
