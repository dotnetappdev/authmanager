#!/usr/bin/env bash
PROJ="samples/AuthManagerSample.WebApi"
URLS=${1:-"http://localhost:5000;https://localhost:5001"}

echo "Starting AuthManager WebApi sample (project: $PROJ)"
echo "Listening on: $URLS"

dotnet run --project "$PROJ" --urls "$URLS" &
sleep 2
if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "http://localhost:5000/swagger"
elif command -v open >/dev/null 2>&1; then
  open "http://localhost:5000/swagger"
else
  echo "Open http://localhost:5000/swagger in your browser"
fi
