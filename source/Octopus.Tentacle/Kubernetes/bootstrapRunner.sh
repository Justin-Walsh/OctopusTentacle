﻿#! /usr/bin/bash

WORK_DIR=$1

lineNo=0

format() {
  ((++lineNo))
	now=$(date -u +"%Y-%m-%dT%H:%M:%S.%N%z")
	echo "$lineNo|$now|$1|$2"
}

logStdOut() {
  while read -r IN
  do
    format "stdout" "$IN"
  done
}

logStdErr() {
  while read -r IN
  do
    format "stderr" "$IN"
  done
}

#pass the remaining args (skipping the first which is the working directory)
shift

BOOTSTRAP_SCRIPT=$1

#This is the args for the Bootstrap script
shift

exec > >(logStdOut)
exec 2> >(logStdErr >&2)

# Change cwd to the working directory
cd "$WORK_DIR" || return

# Write a message to say the script has start
echo "##octopus[stdout-verbose]"
echo "Script started"
echo "##octopus[stdout-default]"

/bin/bash "$BOOTSTRAP_SCRIPT" "$@"

#Get the return value from the previous script
RETURN_VAL=$?

# Write a message to say the script has completed
echo "##octopus[stdout-verbose]"
echo "Script completed"
echo "##octopus[stdout-default]"

echo "EOS-075CD4F0-8C76-491D-BA76-0879D35E9CFE<<>>$RETURN_VAL"

# This ungodly hack is to stop the pod from being killed before the last log has been flushed
#sleep 0.250 #250ms

#Propagate the return value from the bootstrap script to the output host
exit "$RETURN_VAL"