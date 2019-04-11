echo "Testing parse.sh"                    >  ./actual.sh.txt
./parse.sh                                 >> ./actual.sh.txt
./parse.sh -a                              >> ./actual.sh.txt
./parse.sh --a                             >> ./actual.sh.txt
./parse.sh -parameter-a                    >> ./actual.sh.txt
./parse.sh --parameter-a                   >> ./actual.sh.txt
./parse.sh -a x                            >> ./actual.sh.txt
./parse.sh --a x                           >> ./actual.sh.txt
./parse.sh -parameter-a x                  >> ./actual.sh.txt
./parse.sh --parameter-a x                 >> ./actual.sh.txt
./parse.sh -a x --a x                      >> ./actual.sh.txt
./parse.sh --a x -a x                      >> ./actual.sh.txt
./parse.sh -parameter-a x -parameter-a x   >> ./actual.sh.txt
./parse.sh --parameter-a x --parameter-a x >> ./actual.sh.txt
