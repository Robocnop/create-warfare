#!/bin/sh
cd "$(dirname "$0")"
exec java @user_jvm_args.txt @libraries/net/neoforged/neoforge/21.1.264/unix_args.txt nogui "$@"
