#!/bin/bash
# Starts both the .NET Test API server and the React dev server

echo "Starting .NET API Server..."
dotnet run --project Cel.Compiled.TestApi/Cel.Compiled.TestApi.csproj &
API_PID=$!

echo "Starting React Dev Server..."
cd cel-gui-react && npm run dev &
REACT_PID=$!

function cleanup {
  echo "Shutting down servers..."
  kill $API_PID
  kill $REACT_PID
}

trap cleanup EXIT

# Wait for both processes
wait $API_PID
wait $REACT_PID
