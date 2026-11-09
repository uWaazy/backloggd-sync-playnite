#!/usr/bin/env bash
# Creates the labels used by the issue templates (needs the GitHub CLI: gh auth login).
set -e
gh label create bug --color d73a4a --description "Something is not working" --force
gh label create enhancement --color a2eeef --description "New feature or improvement" --force
