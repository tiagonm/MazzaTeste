#!/usr/bin/env bash
#
# Full SonarQube analysis: begin -> build -> test with coverage -> end.
#
# The build has to happen *between* begin and end, because the scanner hooks MSBuild
# to learn what was compiled. Running `dotnet build` outside that window produces an
# analysis with no C# in it at all.
set -euo pipefail

: "${SONAR_HOST_URL:?SONAR_HOST_URL is required}"
: "${SONAR_TOKEN:?SONAR_TOKEN is required}"
SONAR_PROJECT_KEY="${SONAR_PROJECT_KEY:-mazza-orders}"

COVERAGE_DIR="/tmp/coverage"
rm -rf "${COVERAGE_DIR}"

echo "==> Starting analysis of ${SONAR_PROJECT_KEY} against ${SONAR_HOST_URL}"
dotnet sonarscanner begin \
  /k:"${SONAR_PROJECT_KEY}" \
  /n:"Mazza Orders API" \
  /d:sonar.host.url="${SONAR_HOST_URL}" \
  /d:sonar.token="${SONAR_TOKEN}" \
  /d:sonar.cs.opencover.reportsPaths="${COVERAGE_DIR}/**/coverage.opencover.xml" \
  /d:sonar.scanner.scanAll=false

echo "==> Building"
dotnet build Mazza.Orders.sln --configuration Release

echo "==> Testing with coverage"
# OpenCover format specifically: it is the .NET coverage format SonarQube reads.
# Cobertura, coverlet's default, is silently ignored.
dotnet test Mazza.Orders.sln \
  --configuration Release \
  --no-build \
  --results-directory "${COVERAGE_DIR}" \
  --collect:"XPlat Code Coverage;Format=opencover"

echo "==> Uploading results"
dotnet sonarscanner end /d:sonar.token="${SONAR_TOKEN}"

echo "==> Done. Open ${SONAR_HOST_URL} to see the report."
