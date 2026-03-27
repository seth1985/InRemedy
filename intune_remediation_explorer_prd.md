# Product Requirements Document (PRD)

## Product Name
**Intune Remediation Explorer**

## Document Purpose
This PRD defines the product vision, scope, functional requirements, non-functional requirements, architecture direction, data model, UX expectations, and phased implementation plan for a web-based internal application that visualizes Microsoft Intune Remediations data at scale.

The document is written for Codex to use as the primary implementation blueprint.

---

# 1. Executive Summary

Intune Remediation Explorer is an internal enterprise web application that allows administrators to ingest, analyze, and visualize the results of one or more Microsoft Intune Remediations across their managed Windows device fleet.

The app must support both **remediation-first** and **device-first** workflows:
- Admins can select one or more remediations and understand status, failure patterns, remediation effectiveness, and trends.
- Admins can search for one or more devices and view all remediation results relevant to those devices.

The core value of the product is to turn raw remediation results into an operationally useful interface with:
- rich filtering
- advanced tables/grids
- customizable columns
- saved views
- default layouts per user
- graphical summaries
- drill-down details
- trend analysis
- export capability

This is not intended to be only a dashboard. It is an investigation and operations tool.

---

# 2. Problem Statement

Microsoft Intune provides remediation execution data, but the native experience is limited for enterprise operational analysis. Administrators often struggle with the following:

- comparing multiple remediations side by side
- finding all remediation results for a specific device
- understanding failure trends over time
- visualizing status distribution across remediations
- normalizing raw script output into meaningful categories
- tailoring the UI to their operational workflow
- saving preferred data views and using them repeatedly
- separating noisy remediations from true high-impact issues

The current experience is data-rich but investigation-poor. Administrators need a purpose-built experience that supports both broad fleet monitoring and precise troubleshooting.

---

# 3. Product Goals

## Primary Goals
1. Provide a centralized interface for viewing and analyzing Intune Remediation results.
2. Support both fleet-wide monitoring and device-specific troubleshooting.
3. Deliver highly interactive graphical views that help admins identify trends, clusters, and problem areas quickly.
4. Provide enterprise-grade table/grid functionality with user-controlled column visibility, ordering, sizing, filtering, sorting, and saved views.
5. Allow users to save custom layouts and mark one saved view as their default.
6. Enable comparison across multiple remediations.
7. Improve investigation speed for support and engineering teams.
8. Create a strong foundation for future AI-assisted analysis and reporting.

## Secondary Goals
1. Normalize detection/remediation output into categorized issue types.
2. Provide assignment coverage awareness where possible.
3. Support export for ticketing, leadership summaries, and spreadsheet analysis.
4. Surface repeat offenders, recurring failures, and stale/no-run devices.

---

# 4. Non-Goals (Initial Release)

The following are explicitly out of scope for v1 unless otherwise noted:

- Editing or creating Intune remediations from inside the app
- Changing Intune assignments from inside the app
- Running remediation scripts directly from the app
- Replacing Microsoft Intune admin center
- Cross-platform endpoint support beyond Windows-focused remediation analysis
- Full mobile experience optimization beyond responsive read-only support
- Multi-tenant SaaS design
- Custom report builder with arbitrary visual design tools

These may be considered for later phases.

---

# 5. Target Users

## Primary Users
- Intune administrators
- Windows endpoint engineers
- endpoint operations teams
- workplace platform engineers
- support escalation teams

## Secondary Users
- engineering managers
- service owners
- security operations teams reviewing remediation coverage

---

# 6. Key User Scenarios

## Scenario A: Remediation-first investigation
An admin selects a remediation named “Fix Windows Update Service State” and wants to:
- see how many devices are passing, failing, remediated, or stale
- see if failures are increasing or decreasing
- filter affected devices by OS build, model, ring, or region
- inspect raw detection and remediation outputs
- export failing devices

## Scenario B: Device-first investigation
An admin searches for device `LON-W11-2042` and wants to:
- see all remediation results for that device
- identify which remediations repeatedly fail
- compare outputs across runs
- know when the last result was recorded

## Scenario C: Multi-remediation comparison
An admin selects three remediations related to Office, Windows Update, and time zone configuration and wants to:
- compare status distributions
- identify overlapping affected devices
- understand whether a specific device cohort is failing multiple remediations

## Scenario D: Personalized operational view
An admin prefers a table layout showing only:
- Device Name
- User
- Model
- OS Build
- Last Sync
- Remediation Status
- Detection Output Category
- Last Run Time

The admin wants to:
- hide other columns
- reorder columns
- resize columns
- save the layout as “My Operational View”
- set it as default so the app always opens with that layout

## Scenario E: Team-specific default layouts
A remediation engineering team wants a shared saved view for “Patch Reliability” that can be reused by multiple admins.

---

# 7. Product Scope

## Core Scope
The application must support:
- remediation inventory browsing
- device search and multi-device selection
- ingestion or synchronization of remediation results
- interactive dashboarding
- advanced filtering
- advanced tabular/grid views
- graphical summaries
- status trend analysis
- raw output inspection
- output pattern grouping
- saved views and default views
- CSV/Excel export

## Scope by Data Perspective
### A. Remediation Perspective
- View one or many remediations
- View status summaries, distributions, trends, and affected devices

### B. Device Perspective
- View one or many devices
- See all known remediation results tied to those devices

### C. Cross-Cutting Perspective
- saved views
- filters
- exports
- charts
- details panel
- time range selection

---

# 8. Information Architecture

The app should include the following primary navigation:

1. **Dashboard**
2. **Remediations**
3. **Devices**
4. **Compare**
5. **Trends**
6. **Saved Views**
7. **Settings**

Optional later addition:
8. **Administration**

---

# 9. Functional Requirements

## 9.1 Dashboard

### Objective
Provide a high-level overview of remediation health across the environment.

### Requirements
The Dashboard must show:
- total monitored remediations
- total devices with results in the selected date range
- total failed results
- total remediated results
- total pass results
- total stale/no recent run devices
- top failing remediations
- top affected models
- top affected OS builds
- recent trend of failures/remediations

### Charts
Use:
- stacked bar chart for top remediations by status
- line chart for trend over time
- ranking bar chart for top affected models/builds
- optional donut chart for overall status summary

### Interactions
- clicking a chart segment must filter the underlying results grid
- dashboard filters must apply globally within the page

---

## 9.2 Remediations Page

### Objective
Allow admins to browse, search, filter, and drill into one or more remediations.

### Requirements
The page must support:
- searchable remediation list
- selection of one remediation
- multi-select of several remediations
- remediation summary cards
- device result grid
- chart area with status breakdown and trend
- tabs or sections for Overview, Devices, Output Patterns, Trends, Versions

### Required Summary Metrics
For selected remediation(s), display:
- assigned/known device count if available
- devices with results
- pass count
- fail count
- remediated count
- unknown/no data count
- stale result count
- last data refresh time

### Output Drilldown
Users must be able to inspect:
- detection output
- remediation output
- latest run timestamp
- prior run history if available
- parsed category/tags if normalization is enabled

---

## 9.3 Devices Page

### Objective
Support a device-centric workflow.

### Requirements
The page must support:
- device search by name
- multi-device selection
- device metadata display
- matrix view of remediations vs devices
- detailed run history by selected device
- quick filters for repeated failures and stale results

### Required Views
1. **Device Summary Panel**
   - Device Name
   - Primary User if available
   - Manufacturer
   - Model
   - OS Version
   - OS Build
   - Last Intune Sync
   - Entra/Intune IDs if available

2. **Device Remediation Matrix**
   - Rows = devices or remediations depending on selected mode
   - Columns = selected remediations or devices
   - Cell color represents status
   - Clicking a cell opens detail drawer

3. **History Grid**
   - run timestamp
   - remediation name
   - status
   - detection output
   - remediation output
   - error code/category

---

## 9.4 Compare Page

### Objective
Enable side-by-side comparison of multiple remediations.

### Requirements
Users must be able to:
- select multiple remediations
- compare counts by status
- compare trends over time
- identify device overlap
- see common output categories
- export the comparison result table

### Visualization Requirements
Use:
- grouped bar charts
- stacked bars
- overlap matrix
- trend lines

Avoid relying on Venn diagrams for primary UX.

---

## 9.5 Trends Page

### Objective
Provide historical analysis and change over time.

### Requirements
The page must support:
- 7/30/90/custom date ranges
- trend by remediation
- trend by status
- trend by model
- trend by OS build
- first seen / last seen indicators
- growing vs shrinking issue populations

### Optional Later Enhancement
- highlight likely regressions after script version change
- highlight likely regressions after patch Tuesday or feature update wave

---

## 9.6 Saved Views

### Objective
Allow users to persist personalized analysis layouts and return to them quickly.

### Requirements
Users must be able to:
- save current view state as a named saved view
- update an existing saved view
- duplicate a saved view
- delete a saved view
- mark one saved view as default
- optionally share a saved view with a group or team in later phases

### Saved View Scope
A saved view must capture at minimum:
- page context (Dashboard, Remediations, Devices, Compare, Trends)
- selected filters
- selected date range
- selected remediation(s)
- selected device(s) if applicable
- visible columns
- hidden columns
- column order
- column widths where supported
- sort order
- pinned columns where supported
- grouping settings if supported
- chart mode preferences where applicable

### Default View Behavior
- Each user may set exactly one default view per page, or one global default depending on implementation choice.
- On page load, the system should restore the user’s default view if one exists.
- If no default exists, load the system default layout.

### UX Requirements
- “Save View” button must be visible in major table-driven pages.
- “Set as Default” must be a simple user action.
- Users must always have a “Reset to System Default” option.

---

## 9.7 Advanced Grid / Table Requirements

This is a first-class feature, not a secondary detail.

### Requirements
All major data tables must support:
- show/hide columns
- drag-to-reorder columns
- resize columns
- multi-column sort where supported
- column pinning where supported
- filtering per column
- global search where applicable
- export current filtered rows
- row selection
- sticky headers
- virtualization or efficient rendering for large row counts

### Column Management UX
Users must be able to:
- open a column chooser panel
- toggle individual columns on/off
- search for a column by name
- restore default columns
- save column configuration inside a saved view

### Nice-to-Have Later
- column groups
- conditional formatting rules
- saved filter presets separate from saved views

---

## 9.8 Filtering

### Global Filters
Depending on page, support filtering by:
- remediation name
- remediation category
- device name
- primary user
- manufacturer
- model
- OS version
- OS build
- region
- update ring
- status
- output category
- last run time
- last sync time
- stale status
- script version if available

### Filter Behavior
- filters must be composable
- filters must be reflected in charts and tables consistently
- filters should be visible as removable chips/tags
- filters should be persisted in saved views

---

## 9.9 Search

### Requirements
The system must support:
- device search by exact or partial device name
- remediation search by exact or partial name
- optional search across output text in later phases

### Performance Expectation
Search results should feel responsive for normal enterprise usage.

---

## 9.10 Raw Output and Normalized Output

### Objective
Provide both original script outputs and structured interpretation.

### Requirements
The app must store and display raw values where available:
- detection output
- remediation output
- result/status values
- timestamp
- run context fields

The app should also support normalized classification where rules exist, such as:
- AccessDenied
- MissingRegistryValue
- ServiceStopped
- PendingReboot
- PathNotFound
- Timeout
- Unknown

### Rule-Based Categorization
The backend should support configurable mapping rules that classify outputs based on:
- exact text
- contains text
- regex pattern
- error code
- script version

---

## 9.11 Export

### Requirements
Users must be able to export:
- current filtered table results to CSV
- current filtered table results to Excel
- summary snapshots as CSV or later PDF

### Export Rules
- export must honor active filters
- export must honor visible columns where appropriate
- there should also be an option to export all columns regardless of current visibility

---

## 9.12 Refresh and Synchronization

### Requirements
The system must show:
- last successful data refresh time
- refresh status
- data staleness indication

### Modes
Support at least one of the following in v1:
1. scheduled backend sync from Graph or another source
2. manual data import
3. hybrid mode

Recommendation: build for both if practical, but ensure one working path end to end in v1.

---

# 10. Data Sources

## Primary Source
Microsoft Intune remediation result data via Microsoft Graph and/or exported source data.

## Supported Input Modes
### Mode A: Direct Graph Synchronization
The backend pulls remediation definitions, assignments if available, device metadata, and remediation run results.

### Mode B: File Import
Admins can import CSV or structured export data for offline or early-stage operation.

### Mode C: Hybrid
Direct Graph sync plus supplemental imports.

---

# 11. Data Model

The implementation should normalize data into a backend store.

## 11.1 Remediation
Fields:
- remediationId
- remediationName
- category
- description
- createdDateTime if available
- modifiedDateTime if available
- detectionScriptVersion if available
- remediationScriptVersion if available
- platform
- activeFlag

## 11.2 Device
Fields:
- deviceId
- deviceName
- primaryUser
- manufacturer
- model
- osVersion
- osBuild
- region
- updateRing
- lastSyncDateTime
- azureAdDeviceId if available
- complianceState if available

## 11.3 RemediationResult
Fields:
- resultId
- remediationId
- deviceId
- runTimestampUtc
- status
- remediationAttemptedFlag
- remediationSucceededFlag if derivable
- detectionOutputRaw
- remediationOutputRaw
- errorCode
- errorSummary
- outputCategory
- outputTags
- scriptVersion
- durationSeconds if available
- dataSource
- ingestionTimestampUtc

## 11.4 SavedView
Fields:
- savedViewId
- ownerUserId
- pageType
- name
- isDefault
- isSystemDefault
- sharedScope (future)
- viewDefinitionJson
- createdUtc
- modifiedUtc

## 11.5 OutputNormalizationRule
Fields:
- ruleId
- name
- enabled
- matchType
- pattern
- resultingCategory
- severity
- tagList
- priority

---

# 12. Status Model

The UI should normalize source values into a consistent status model.

Recommended canonical statuses:
- Pass
n- Fail
- Remediated
- Unknown
- No Data
- Stale

If the raw source uses different terminology, map it into this canonical model while preserving raw values separately.

Note: Codex should implement the canonical mapping in a dedicated status normalization layer.

---

# 13. User Experience Requirements

## 13.1 Design Principles
- operational clarity over decoration
- fast drill-down
- consistent filtering behavior
- tables and charts should reinforce each other
- raw details should never be hard to access
- personalization should be easy and persistent

## 13.2 Layout Principles
- left navigation for main sections
- top filter bar for page filters
- cards and charts above tables
- detail drawer or right-side panel for row-specific data
- sticky filter area on large pages

## 13.3 Visual Principles
Recommended status colors:
- Pass = green
- Fail = red
- Remediated = amber/orange
- Unknown/No Data/Stale = gray variants

Do not overuse pie charts. Prefer:
- stacked bars
- ranked bars
- line charts
- heatmaps
- matrix views

## 13.4 Responsiveness
Desktop-first design. Tablet support acceptable. Mobile support may be read-only and limited.

---

# 14. Required Pages in Detail

## 14.1 Dashboard Page Components
- KPI cards
- top failing remediations stacked bar chart
- remediation trend line chart
- top affected device models chart
- top affected OS builds chart
- recent results grid

## 14.2 Remediation Detail Page Components
- remediation summary header
- status cards
- trend chart
- affected devices grid
- output category distribution chart
- details drawer with raw outputs and device metadata

## 14.3 Devices Page Components
- search and selection panel
- device summary card
- remediation matrix
- result history grid
- repeated failure indicator

## 14.4 Compare Page Components
- multi-remediation selector
- side-by-side metrics
- grouped charts
- overlap matrix
- comparison result table

## 14.5 Saved Views Page Components
- user’s saved views list
- page type
- last modified
- default indicator
- actions: open, rename, duplicate, delete, set default

---

# 15. Permissions and Security

## Authentication
Use Microsoft Entra ID for user sign-in.

## Authorization
Recommended roles:
- Reader: can view data and use saved views
- Analyst: can export and create personal saved views
- Admin: can manage system settings, normalization rules, shared views, refresh operations

## Security Requirements
- all app routes require authentication
- backend APIs must enforce role checks
- audit sensitive admin actions
- do not expose secrets to frontend
- use secure service-to-service auth for Graph access

---

# 16. Performance Requirements

## Expectations
- common page load should feel responsive on enterprise data sets
- tables must handle large row counts with pagination or virtualization
- filters should update quickly enough for operational use
- charts should remain usable with high-cardinality datasets

## Recommended Techniques
- server-side filtering for large datasets
- indexed backend store
- asynchronous background sync jobs
- caching for dimension lists and summaries
- pagination for large grids

---

# 17. Observability and Diagnostics

The application must capture:
- sync job status
- sync errors
- API failures
- import failures
- frontend errors
- user action telemetry for key actions such as export, save view, set default, open detail, refresh

Admin logs should help answer:
- when did data last refresh successfully
- why did a sync fail
- which views are heavily used

---

# 18. Suggested Technical Architecture

## Recommended Stack
### Frontend
- React
- TypeScript
- component library such as shadcn/ui or equivalent
- chart library such as Recharts or ECharts
- advanced grid such as AG Grid or TanStack Table

### Backend
- ASP.NET Core Web API
- background hosted services for synchronization

### Database
- SQL Server for production
- SQLite acceptable for dev/prototype

### Auth
- Entra ID

### Data Access
- Microsoft Graph and/or import pipeline

## Architecture Components
1. **Frontend SPA**
2. **API Layer**
3. **Sync/Import Services**
4. **Normalization Engine**
5. **Database**
6. **Auth/Role Enforcement**

---

# 19. API Requirements

Codex should implement backend endpoints that cover at minimum:

## Remediations
- list remediations
- get remediation detail
- get remediation summaries
- get remediation results with filters

## Devices
- search devices
- get device detail
- get device remediation history

## Compare
- compare selected remediations
- get overlap matrix data

## Dashboard
- get overall KPI summary
- get top remediation chart data
- get trend chart data
- get top model/build chart data

## Saved Views
- list saved views
- create saved view
- update saved view
- duplicate saved view
- delete saved view
- set default saved view
- get default saved view for page

## Settings/Admin
- list normalization rules
- create/update/delete normalization rules
- trigger refresh if admin
- get sync status

---

# 20. Detailed Saved View Requirements

This feature is important enough to define separately.

## Functional Requirements
1. Users can save the active page state with a custom name.
2. Users can choose whether the saved view is personal only.
3. Users can mark a saved view as their default for that page.
4. Users can load saved views from a dedicated list or page-level dropdown.
5. Users can rename, duplicate, and delete their own saved views.
6. System must validate duplicate naming behavior according to design choice.
7. A system default layout must always exist as a fallback.

## View State Definition
The saved JSON state should include at minimum:
- selected page
- filters
- search text
- date range
- selected remediations
- selected devices
- chart mode selection
- visible columns array
- column order array
- column width map
- sort configuration
- grouping config
- pinned column config
- page size where applicable

## Acceptance Criteria
- A user hides 5 columns, reorders 3 columns, filters to Ring 0 and Fail status, saves the layout, refreshes the page, loads the saved view, and sees the exact same configuration restored.
- A user marks a saved view as default and the page reopens in that state on next visit.
- A user can reset back to system default without deleting the saved view.

---

# 21. Detailed Grid Requirements

## Required Capabilities by Table
Every major table must support:
- configurable visible columns
- configurable hidden columns
- reorderable columns
- resizable columns
- column search within column chooser
- save current layout
- restore saved layout
- export filtered rows
- default sort handling

## Accessibility and Usability
- columns should have clear labels
- tooltips should exist for truncated cell values where useful
- sticky first column or pinned column support is preferred for Device Name / Remediation Name

## Enterprise Practicality
The app must assume that different admins care about different fields. The product should not force a single rigid layout.

---

# 22. Import and Sync Requirements

## Sync Strategy
Codex should design the backend so the source layer can be swapped or extended.

### Minimum v1 Requirement
At least one full working ingestion path must exist end-to-end.

### Recommended v1 Preference
- scheduled sync job pulling data from Microsoft Graph if feasible
- fallback manual CSV import for testing and early validation

## Import Validation
The system should:
- validate file schema
- reject clearly malformed files
- report row-level validation errors where possible
- not allow a bad file to silently corrupt the data store

---

# 23. Error Handling Requirements

The UI must provide clear messages for:
- no data
- stale data
- insufficient permissions
- failed refresh
- malformed import
- empty filter result
- unavailable chart data

Avoid vague messages like “Something went wrong” unless accompanied by traceable context.

---

# 24. Testing Requirements

## Unit Tests
- status normalization
- output normalization rule engine
- saved view serialization/deserialization
- filter logic
- column configuration persistence

## Integration Tests
- sign-in flow
- data retrieval APIs
- saved view CRUD
- export logic
- import processing

## UI Tests
- hide/show columns
- reorder columns
- save and load saved views
- set default view
- chart-to-grid filtering interaction
- detail drawer behavior

## Performance Tests
- large result table rendering
- multi-remediation comparison queries
- dashboard summary generation on realistic enterprise data volume

---

# 25. Accessibility Requirements

At minimum:
- keyboard navigation for key controls
- accessible labels on buttons, filters, and column chooser
- adequate contrast
- charts should not rely on color alone where feasible
- table state should be understandable by screen readers to a reasonable enterprise standard

---

# 26. Rollout Strategy

## Phase 1: Foundation / MVP
Build:
- auth
- remediation list
- device search
- dashboard basics
- results grid
- column chooser
- save/load personal saved views
- default view per user
- CSV export
- one working ingestion path

## Phase 2: Advanced Investigation
Build:
- multi-remediation compare
- device-remediation matrix
- output normalization rules
- trend analysis improvements
- Excel export
- admin sync status page

## Phase 3: Operational Maturity
Build:
- shared team views
- regression detection
- assignment coverage awareness
- richer admin controls
- possible AI-generated summaries

---

# 27. MVP Definition

The MVP is successful if it allows an authenticated admin to:
1. view a list of remediations
2. search and select devices
3. filter remediation results
4. view graphical summaries
5. inspect raw outputs
6. show/hide/reorder columns
7. save a custom view
8. mark a view as default
9. export filtered results

---

# 28. Acceptance Criteria Summary

## Must-Have Acceptance Criteria
- User can analyze at least one remediation with charts and a detail grid.
- User can search for a device and view associated remediation results.
- User can select which columns are visible in the results table.
- User can reorder and resize columns.
- User can save the current table and filter configuration as a named view.
- User can set a saved view as default.
- User can reset to default layout.
- Charts and tables remain consistent with active filters.
- Raw detection/remediation output can be inspected for a selected result.
- Export reflects the active filtered result set.

## Nice-to-Have for Early Release
- multi-remediation comparison
- overlap matrix
- output category rules
- shared views

---

# 29. Risks and Mitigations

## Risk: Source data limitations
Mitigation:
- preserve flexible ingestion layer
- support manual import for prototype validation

## Risk: Very large enterprise datasets
Mitigation:
- server-side filtering
- pagination/virtualization
- summary pre-aggregation

## Risk: Saved views become inconsistent after schema changes
Mitigation:
- version saved view schema
- gracefully ignore deleted columns
- preserve backward compatibility where practical

## Risk: Raw outputs are too inconsistent to graph meaningfully
Mitigation:
- add rule-based normalization layer
- always preserve raw output

---

# 30. Future Enhancements

Potential future features:
- shared team saved views
- management summary mode
- scheduled report delivery
- anomaly detection
- AI-generated narrative summaries
- remediation assignment coverage and targeting analytics
- script version comparison panels
- tag-based remediation grouping
- patch-wave overlay on charts

---

# 31. Implementation Guidance for Codex

## Important Delivery Guidance
Codex should not attempt to build every advanced feature at once.

### Recommended Build Order
1. project scaffold with auth and layout shell
2. database schema and seed/sample data
3. remediation listing and detail page
4. results grid with filtering and column chooser
5. save/load personal saved views and default view behavior
6. dashboard charts
7. device search and device details
8. export features
9. compare page
10. normalization rules and admin pages

## Coding Expectations
- strongly typed frontend and backend models
- reusable filter/state management
- reusable grid state persistence utilities
- view state stored as JSON with schema version
- clean API contracts
- clear separation between raw source model and normalized UI model

## UX Expectations
- modern, clean, enterprise style
- fast-feeling workflow
- avoid visual clutter
- prioritize operational readability

---

# 32. Final Product Statement

Intune Remediation Explorer must become a practical operational tool for Intune administrators, not just a reporting page. The differentiator is the combination of:
- deep remediation visibility
- device-centric drilldown
- strong visualization
- advanced customizable tables
- persistent saved views with default layouts

The ability to hide columns, choose what matters, save that layout, and make it the default is a core part of the product value and must be treated as a first-class requirement throughout the design and implementation.
