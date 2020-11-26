#!  /bin/bash

b=`pwd`/wwwroot/supportFiles
d=`pwd`/static_sample

for f in `find ${b} -iname '*.dmeta' -o -iname '*.dil' -o -iname '*.dpdb'`; do
  t=`basename $f`;
  n=`echo "$t" | sed -r -e 's/^[0-9]+_//'`;
  cp $f "$d/$n"
done
