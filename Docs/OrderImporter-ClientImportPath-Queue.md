# Order Import Queue via `ClientImportPath`

## Purpose
This document describes the **new client-driven queue flow** for importing orders into 3Shape through the server-side `OrderImporter` service.

The client app no longer points the server to a workstation-local file path.
Instead, the client copies ZIP files to a server-accessible shared folder (`ClientImportPath` in `dbo.Settings`) and then queues a request in `dbo.OrdersToImport`.

---

## Required Settings
In `StatsDB.dbo.Settings`, add or update:

- `sName = 'ClientImportPath'`
- `sValue = '\\SERVER\SomeSharedFolder\OrderImportQueue'` (example)

The `OrderImporter` service account must have read/write access to this share.

---

## Queue Table Contract
The client writes import requests to:

- `StatsDB.dbo.OrdersToImport`

Fields used by client and server:

- `OrderID` (string)
- `Path` (string, full UNC/shared ZIP path)
- `RequestingComputer` (string)

---

## Client Behavior (implemented)

### A) Import from Archived Order (OrderInfoWindow)
1. Read `ClientImportPath` from `dbo.Settings`.
2. Validate the share exists and is accessible.
3. If order already exists in 3Shape folder, ask user to confirm overwrite intent.
4. Build ZIP package of the archive order into `ClientImportPath`.
5. Insert queue request into `dbo.OrdersToImport` with shared ZIP path.

### B) Import from ZIP Drop on MainWindow 3Shape List
1. User drops one ZIP file.
2. Client reads OrderID from ZIP/XML name fallback.
3. Read/validate `ClientImportPath`.
4. If order already exists in 3Shape folder, ask overwrite confirmation.
5. Copy dropped ZIP to `ClientImportPath`.
6. Insert queue request into `dbo.OrdersToImport` using the copied shared ZIP path.

---

## Server `OrderImporter` Expected Behavior
When implementing/updating server logic:

1. Poll `dbo.OrdersToImport`.
2. For each row:
   - Read `OrderID`, `Path`, `RequestingComputer`.
   - Validate `File.Exists(Path)`.
   - Extract ZIP to temp.
   - Validate expected structure (`<temp>\<OrderID>\<OrderID>.xml`).
   - Perform all existing integrity checks (`stCopy`/manufacturing checks/version checks/etc.).
   - Inject XML data into 3Shape DB.
   - Copy order folder into 3Shape filesystem location.
   - Write history/metadata as needed.
3. Remove processed item from queue (`DELETE FROM dbo.OrdersToImport WHERE OrderID = @OrderID`).

---

## Important Safety Rules
- Do **not** rely on workstation-local paths from client drops.
- Queue path must be server-reachable (`ClientImportPath`).
- Client-side import flow should **not delete user source ZIP/folder**.
- Overwrite decisions are confirmed in client before queueing.

---

## Suggested SQL Upsert for Queueing
Client currently uses:

1. `DELETE FROM dbo.OrdersToImport WHERE OrderID = @orderId`
2. `INSERT INTO dbo.OrdersToImport (OrderID, Path, RequestingComputer) VALUES (...)`

This keeps one active queued request per order.
