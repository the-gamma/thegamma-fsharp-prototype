#!/bin/bash
cd data
./build.sh $@
cd ../web
./build.sh $@
cd ..
