echo "Testing parse.sh"                         > ./actual.sh.txt
./parse.sh                                     >> ./actual.sh.txt
./parse.sh -c                                  >> ./actual.sh.txt
./parse.sh --c                                 >> ./actual.sh.txt
./parse.sh -configuration                      >> ./actual.sh.txt
./parse.sh --configuration                     >> ./actual.sh.txt
./parse.sh -c x                                >> ./actual.sh.txt
./parse.sh --c x                               >> ./actual.sh.txt
./parse.sh -configuration x                    >> ./actual.sh.txt
./parse.sh --configuration x                   >> ./actual.sh.txt
./parse.sh -c x --c x                          >> ./actual.sh.txt
./parse.sh --c x -c x                          >> ./actual.sh.txt
./parse.sh -configuration x -configuration x   >> ./actual.sh.txt
./parse.sh --configuration x --configuration x >> ./actual.sh.txt
