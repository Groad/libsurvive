#!/bin/bash

#place this script in /etc/bash_completion.d/ for general use.
_script()
{
	_script_commands=$(./survive-cli -m $1 $2 $3)
	local cur prev
	COMPREPLY=()
	cur="${COMP_WORDS[COMP_CWORD]}"
	COMPREPLY=( $(compgen -W "${_script_commands}" -- ${cur}) )
	return 0
}

complete -o nospace -F _script ./calibrate
complete -o nospace -F _script ./simple_pose_test
complete -o nospace -F _script ./data_recorder
complete -o nospace -F _script ./survive-cli

