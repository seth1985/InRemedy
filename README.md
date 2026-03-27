# In-Remedy

In-Remedy is a local remediation analysis workspace for Intune proactive remediation exports. It is built to help you import one or more Intune CSV or ZIP exports, review remediation results in a spreadsheet-like table, filter the data, adjust visible columns, and export the final result set.

## What The App Does

- Imports Intune remediation export files in `.csv` or `.zip` format
- Processes large files in the background
- Shows remediation-focused summary cards
- Lets you show, hide, reorder, and resize columns
- Supports per-column filtering, including multi-select filters
- Exports the filtered table to CSV

## Typical User Flow

1. Start the API and frontend.
2. Open the app in the browser.
3. Drag and drop one or more Intune remediation export files into the import area, or choose them manually.
4. Click `Import`.
5. Wait for the import notification to confirm upload and processing.
6. Review the remediation summary cards.
7. Use the `Columns` panel on the left to show or hide fields relevant to your investigation.
8. Reorder columns from the table header drag handle and resize them as needed.
9. Filter the table by column values.
10. Click `Export CSV` to export the final filtered dataset.

## Supported Files

- Intune remediation export `.csv`
- Intune remediation export `.zip` containing one or more CSV files

## Main Screen Overview

### Import Area

Use the top import bar to:
- choose files
- drag and drop files
- start import
- clear the database

### Columns Panel

Use the left sidebar to:
- expand or collapse column groups
- activate or deactivate columns
- keep the main table clean and focused

### Remediation Summary

The summary cards provide a quick remediation-focused overview inspired by the Intune experience:
- detection status
- remediation status
- affected device counts

### Results Grid

The table is the main workspace. It supports:
- column filtering
- multi-select dropdown filters
- text filters
- column resize
- column reorder
- horizontal floating scrollbar
- CSV export

## Important Behavior

- The database is cleared on each API startup by design in the current build.
- After every API restart, you need to import files again.
- Notifications appear as toast messages and disappear automatically unless you hover over them.
- Hovering a notification keeps it visible so you can read or copy the text.

## Current Scope

This build is intentionally focused on file-based import and analysis.

- CSV and ZIP imports are supported
- Microsoft Graph integration is not part of this build
- The app is optimized around remediation investigation and export workflow

## Developer Info

- Bojan Crvenkovic
- Modern Workplace Engineer
- crvenkovicbojan@gmail.com
