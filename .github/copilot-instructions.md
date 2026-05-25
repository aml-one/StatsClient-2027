# Copilot Instructions

## General Guidelines
- When implementing logic based on an existing companion project, inspect that project first instead of inferring behavior from simplified assumptions.

## Project Guidelines
- When integrating DCMViewer into StatsClient, use the source code inside the StatsClient project instead of the external AmL.DCMViewer DLL.
- For the Add scan/model picker, position the window at a fixed small non-centered position relative to OrderInfoWindow, specifically 43px lower than the current anchor, and always on the same monitor as OrderInfoWindow to avoid covering the 3D view.
- Do not use layout shifting as a workaround for WebView/Home button overlap in MainWindow; if it cannot be solved without shifting, leave it unchanged.
- For ZIP import requests from client, copy the ZIP to a server shared path from StatsDB Settings key 'ClientImportPath' before inserting OrdersToImport records.

## DCMFinder Guidelines
- For DCMFinder arch inference, safely assume from the ModelElement Items field that any number 1-16 means upper preparation and any number 17-32 means lower preparation.

## XML Comparison Guidelines
- In XML compare semantics, treat stCopy as the original baseline and XML as the new changed file.

## Labnext Auto-Login Guidelines
- Use exact StatsDB Settings keys only: LabnextEmail and LabnextPassword for credentials, and LabnextEmailSelector and LabnextPasswordSelector for website field IDs; do not use guessed/fallback keys or selectors. Use the Labnext selector values in StatsDB directly, as they already include the leading # (e.g., #email, #password).