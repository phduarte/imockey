#!/bin/sh
set -e

# Without a storage account (e.g. a local `docker run`) the database lives only inside the container.
if [ -z "$AZURE_STORAGE_ACCOUNT" ]; then
    exec dotnet Imockey.dll
fi

# The container disk is empty on every start: bring the database back from the replica (if there is one),
# then run the app under Litestream, which replicates every change and does a final sync on SIGTERM.
litestream restore -if-db-not-exists -if-replica-exists "$Database__Path"
exec litestream replicate -exec "dotnet Imockey.dll"
